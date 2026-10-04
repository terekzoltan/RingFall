from __future__ import annotations

import copy
import hashlib
import json
from contextlib import ExitStack
from dataclasses import replace
from decimal import Decimal
from pathlib import Path
import sys
import tempfile
import threading
import unittest
from unittest.mock import patch

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT))

from ringfall_brain.providers.aster_trial import (
    EndpointProof, HarnessAttestation, OfflineTrialClient, TrialConfig, TrialError, TrialLedger,
    _digest, _request_bound, prepare_aster_cases,
)
from ringfall_brain.providers import aster_trial
from ringfall_brain.schemas.validator import validate_packet_json

REPO = ROOT.parents[1]
SCHEMAS = REPO / "src" / "ringfall-contracts" / "schemas"


class FakeHarness:
    def __init__(self):
        self.expected = None
        self.observed = None
        self.override = None
        self.on_pin = None
        self.on_attest = None

    def pin(self, expected):
        if self.on_pin:
            self.on_pin()
        self.expected = expected
        self.observed = None

    def observe(self, request, *, model=None, slug=None, effort=None):
        pinned = self.expected
        if pinned is None:
            return
        self.observed = replace(pinned, model_id=model or request["model"],
                                request_hash=_digest(request),
                                endpoint_slug=slug or request["provider"]["only"][0],
                                reasoning_effort=effort or request["reasoning"]["effort"])

    def attest(self, attempt_id, request_hash):
        if self.on_attest:
            self.on_attest()
        if isinstance(self.override, Exception):
            raise self.override
        return self.override if self.override is not None else self.observed


class FakeTransport:
    enforces_deadline = True

    def __init__(self, reply, harness):
        self.reply = reply
        self.harness = harness
        self.calls = []
        self.observed_slug = None
        self.observed_effort = None
        self.on_send = None

    def send(self, request, *, timeout):
        self.calls.append((copy.deepcopy(request), timeout))
        self.harness.observe(request, slug=self.observed_slug, effort=self.observed_effort)
        if self.on_send:
            self.on_send()
        if isinstance(self.reply, Exception):
            raise self.reply
        return copy.deepcopy(self.reply)


class AsterSourcePortabilityTests(unittest.TestCase):
    """Real copied inputs: no source-reader mocks or replacement runtime pins."""

    SOURCES = (
        (ROOT / "examples" / "aster-a1-context.example.json",
         "4797a7deeef596352795fcad95b9de61c1a1f49438431a37196bfb033a9094cc"),
        (ROOT / "examples" / "aster-a1-pulse.example.json",
         "eed67e719697de57527d643af738c3517aabb8bfe0d5c139c74c5cbd8e940970"),
        (SCHEMAS / "packets" / "avatar-pulse-packet.schema.json",
         "55ded4589b228b8ad9ede5b5213b9bcb2f4f33f3a9292f7a36748268ad93d7d0"),
        (SCHEMAS / "packets" / "tool-action-request.schema.json",
         "50c4d66000ddb7ed01f6ad15a87c20e9975dd4fdf2daa3dd71b99264bd23b10f"),
        (SCHEMAS / "packets" / "work-order-request.schema.json",
         "58fcf506778301afb6c8cc64d741a7179b16a75bdd02e2ccaa314cfbc7c4fdec"),
    )
    MUTATIONS = ("content", "whitespace", "duplicate_key", "string_escape",
                 "standalone_cr", "control", "bom", "final_newline")

    def setUp(self):
        stack = ExitStack()
        self.addCleanup(stack.close)
        directory = Path(stack.enter_context(tempfile.TemporaryDirectory(prefix="aster-source-")))
        self.paths = tuple(directory / source.name for source, _ in self.SOURCES)
        self.raw = tuple(source.read_bytes().replace(b"\r\n", b"\n") for source, _ in self.SOURCES)
        for raw, (_, expected) in zip(self.raw, self.SOURCES):
            self.assertNotIn(b"\r", raw)
            self.assertEqual(expected, hashlib.sha256(raw).hexdigest())
        self.write_representations(("lf",) * 5)
        # Only redirect paths to isolated real files. Readers, validators and pins
        # remain production code; full clean-export checks do not even redirect paths.
        stack.enter_context(patch.object(aster_trial, "_CONTEXT_FILE", self.paths[0]))
        stack.enter_context(patch.object(aster_trial, "_PULSE_FILE", self.paths[1]))
        stack.enter_context(patch.object(aster_trial, "_SCHEMAS", directory))
        self.context, self.pulse = (json.loads(raw) for raw in self.raw[:2])

    def write_representations(self, forms):
        for path, raw, form in zip(self.paths, self.raw, forms):
            if form == "crlf":
                raw = raw.replace(b"\n", b"\r\n")
            elif form == "mixed":
                raw = b"".join(line.replace(b"\n", b"\r\n") if index % 2 else line
                               for index, line in enumerate(raw.splitlines(keepends=True)))
                self.assertIn(b"\r\n", raw)
                self.assertIn(b"\n", raw.replace(b"\r\n", b""))
            self.assertEqual(raw.replace(b"\r\n", b"\n"), self.raw[self.paths.index(path)])
            path.write_bytes(raw)

    def prepare(self):
        return prepare_aster_cases(self.context, self.pulse, pulse_schema=self.paths[2],
                                   tool_schema=self.paths[3], work_order_schema=self.paths[4],
                                   prompt_version="a4-d-v1", profile_version="a1-v1")

    def client(self, cases, ledger, harness, transport):
        proof = EndpointProof("openai/gpt-6-luna", "openai/default", frozenset({"low"}), True, True,
                              Decimal("0.10"), Decimal("0.50"), "offline-proof-v1")
        return OfflineTrialClient(TrialConfig(proof.model_id, "low", "portable_run"), cases,
                                  transport=transport, proof_for_call=lambda: proof,
                                  count_input_tokens=lambda _: 2000, harness=harness,
                                  ledger=ledger, clock=lambda: 0.0)

    def assert_portable_dispatch(self, forms, expected):
        self.write_representations(forms)
        cases = self.prepare()
        self.assertEqual(expected, cases)
        self.assertEqual(self.SOURCES[4][1], cases[0].schema_hash)
        self.assertEqual(self.SOURCES[3][1], cases[1].schema_hash)
        self.assertIsNone(cases[2].schema_hash)
        ledger, harness = TrialLedger(), FakeHarness()
        fake = FakeTransport(None, harness)
        client = self.client(cases, ledger, harness, fake)
        for case in cases:
            fake.reply = {
                "id": "portable_" + case.case_id,
                "model": client.config.model_id,
                "usage": {"prompt_tokens": 100, "completion_tokens": 80, "cost": 0.00005,
                          "completion_tokens_details": {"reasoning_tokens": 20}},
                "choices": [{"finish_reason": "stop", "message": {"content": json.dumps(case.baseline)}}],
            }
            result = client.run_case(case.case_id)
            self.assertEqual((), result.differences)
            self.assertEqual(case.baseline if case.schema_path else None, result.candidate)
            self.assertEqual(case.messages, tuple(fake.calls[-1][0]["messages"]))
        self.assertEqual(3, len(fake.calls))
        self.assertEqual(["completed"] * 3, [attempt.status for attempt in ledger.attempts])
        return tuple(_digest(request) for request, _ in fake.calls)

    def test_global_lf_crlf_mixed_preserve_preparation_and_ordered_dispatch(self):
        # Regression W4-PRE8-D-PORTABILITY: raw CRLF-only pins rejected Git-LF inputs.
        expected = self.prepare()
        requests = self.assert_portable_dispatch(("lf",) * 5, expected)
        for form in ("crlf", "mixed"):
            with self.subTest(form=form):
                self.assertEqual(requests, self.assert_portable_dispatch((form,) * 5, expected))

    def test_each_input_can_change_only_line_endings_after_preparation(self):
        expected = self.prepare()
        for index in range(5):
            for form in ("crlf", "mixed"):
                with self.subTest(input=self.paths[index].name, form=form):
                    self.write_representations(("lf",) * 5)
                    ledger, harness = TrialLedger(), FakeHarness()
                    fake = FakeTransport(None, harness)
                    client = self.client(expected, ledger, harness, fake)
                    forms = ["lf"] * 5
                    forms[index] = form
                    self.write_representations(forms)
                    # Revalidation must accept a permitted representation change,
                    # not merely cases initially prepared from that representation.
                    fake.reply = {"id": "portable_input", "model": client.config.model_id,
                                  "usage": {"prompt_tokens": 100,
                                  "completion_tokens": 80, "cost": 0.00005},
                                  "choices": [{"finish_reason": "stop", "message": {
                                      "content": json.dumps(expected[0].baseline)}}]}
                    self.assertEqual(expected[0].baseline, client.run_case("work_order").candidate)
                    self.assertEqual(expected, self.prepare())
                    self.assertEqual(1, len(fake.calls))
                    self.assertEqual(["completed"], [attempt.status for attempt in ledger.attempts])

    def mutate(self, index, kind):
        raw = self.raw[index]
        if kind == "content":
            changed = raw.replace(b"{", b'{"portability_tamper":"sentinel",', 1)
        elif kind == "whitespace":
            changed = b" " + raw
        elif kind == "duplicate_key":
            value = json.loads(raw)
            key = next(iter(value))
            member = json.dumps({key: value[key]}, ensure_ascii=True)[1:-1].encode("utf-8")
            changed = raw.replace(b"{", b"{" + member + b",", 1)
        elif kind == "string_escape":
            # Escape one ASCII key character: identical parsed object, different bytes.
            position = raw.index(b'"') + 1
            changed = raw[:position] + ("\\u%04x" % raw[position]).encode("ascii") + raw[position + 1:]
        elif kind == "standalone_cr":
            changed = raw.replace(b"\n", b"\r", 1)
        elif kind == "control":
            changed = b"\x00" + raw
        elif kind == "bom":
            changed = b"\xef\xbb\xbf" + raw
        else:
            self.assertEqual("final_newline", kind)
            changed = raw[:-1] if raw.endswith(b"\n") else raw + b"\n"
        self.assertNotEqual(raw, changed)
        self.assertNotEqual(raw, changed.replace(b"\r\n", b"\n"))
        if kind in ("whitespace", "duplicate_key", "string_escape", "standalone_cr", "bom", "final_newline"):
            self.assertEqual(json.loads(raw), json.loads(changed))
        self.paths[index].write_bytes(changed)

    def assert_tamper_matrix(self, after_preparation):
        for index in range(5):
            for kind in self.MUTATIONS:
                with self.subTest(input=self.paths[index].name, mutation=kind,
                                  after_preparation=after_preparation):
                    self.write_representations(("lf",) * 5)
                    ledger, harness = TrialLedger(), FakeHarness()
                    fake = FakeTransport(None, harness)
                    if after_preparation:
                        client = self.client(self.prepare(), ledger, harness, fake)
                    self.mutate(index, kind)
                    with self.assertRaises(TrialError) as caught:
                        if after_preparation:
                            client.run_case("work_order")
                        else:
                            self.client(self.prepare(), ledger, harness, fake)
                    self.assertIn(str(caught.exception), ("accepted A1 fixture is unavailable",
                                                        "prepared case provenance is unverified"))
                    self.assertNotIn("sentinel", str(caught.exception))
                    self.assertEqual([], ledger.attempts)
                    self.assertEqual([], fake.calls)
                    self.assertIsNone(harness.expected)
                    self.assertIsNone(ledger.started)

    def test_each_input_eight_tampers_rejected_during_preparation(self):
        self.assert_tamper_matrix(after_preparation=False)

    def test_each_input_eight_tampers_rejected_after_preparation_before_reservation(self):
        self.assert_tamper_matrix(after_preparation=True)


class AsterTrialTests(unittest.TestCase):
    def setUp(self):
        self.context = json.loads((ROOT / "examples" / "aster-a1-context.example.json").read_text(encoding="utf-8"))
        self.pulse = json.loads((ROOT / "examples" / "aster-a1-pulse.example.json").read_text(encoding="utf-8"))
        self.tool_schema = SCHEMAS / "packets" / "tool-action-request.schema.json"
        self.work_schema = SCHEMAS / "packets" / "work-order-request.schema.json"
        self.cases = prepare_aster_cases(
            self.context, self.pulse, pulse_schema=SCHEMAS / "packets" / "avatar-pulse-packet.schema.json",
            tool_schema=self.tool_schema, work_order_schema=self.work_schema,
            prompt_version="a4-d-v1", profile_version="a1-v1",
        )
        self.proof = EndpointProof("openai/gpt-6-luna", "openai/default", frozenset({"low"}), True, True,
                                   Decimal("0.10"), Decimal("0.50"), "offline-proof-v1")
        self.config = TrialConfig("openai/gpt-6-luna", "low", "offline_run_1")
        self.ledger = TrialLedger()
        self.harness = FakeHarness()

    def reply(self, case=None, **updates):
        case = case or self.cases[0]
        response = {
            "id": "gen_1", "model": self.config.model_id, "provider_slug": self.proof.endpoint_slug,
            "applied_reasoning_effort": self.config.reasoning_effort,
            "usage": {"prompt_tokens": 100, "completion_tokens": 80, "cost": 0.00005,
                      "completion_tokens_details": {"reasoning_tokens": 20}},
            "choices": [{"finish_reason": "stop", "message": {"content": json.dumps(case.baseline)}}],
        }
        response.update(updates)
        return response

    def client(self, reply=None, proof=None, counter=None, clock=None):
        fake = FakeTransport(self.reply() if reply is None else reply, self.harness)
        kwargs = {"transport": fake, "proof_for_call": lambda: self.proof if proof is None else proof,
                  "count_input_tokens": counter or (lambda messages: 2000), "ledger": self.ledger,
                  "harness": self.harness}
        if clock:
            kwargs["clock"] = clock
        return OfflineTrialClient(self.config, self.cases, **kwargs), fake

    def test_three_case_messages_are_distinct_complete_and_actor_local(self):
        self.assertEqual(["work_order", "tool_query", "authority_trap"], [case.case_id for case in self.cases])
        self.assertEqual(3, len({case.messages_hash for case in self.cases}))
        for case in self.cases:
            rendered = str(case.messages)
            self.assertIn(self.context["observations"][0]["signal"], rendered)
            self.assertIn("pulse_A1_t000", rendered)
            self.assertNotIn("hidden thermal debt sentinel", rendered)
        self.assertEqual({"decision": "no_candidate"}, self.cases[2].baseline)
        self.assertIsNone(self.cases[2].schema_path)

    def test_extra_hidden_context_or_drifted_pulse_rejected_before_mock(self):
        for target, key in ((self.context, "privateTruth"), (self.pulse, "hiddenDebt")):
            changed = {**target, key: "hidden thermal debt sentinel"}
            with self.assertRaises(TrialError) as caught:
                prepare_aster_cases(changed if target is self.context else self.context,
                                    changed if target is self.pulse else self.pulse,
                                    pulse_schema=SCHEMAS / "packets" / "avatar-pulse-packet.schema.json",
                                    tool_schema=self.tool_schema, work_order_schema=self.work_schema,
                                     prompt_version="a4-d-v1", profile_version="a1-v1")
            self.assertNotIn("sentinel", str(caught.exception))

    def test_success_records_no_secret_and_schema_valid_dev_cost_event(self):
        client, fake = self.client()
        result = client.run_case("work_order")
        self.assertEqual(self.cases[0].baseline, result.candidate)
        self.assertEqual((), result.differences)
        request, timeout = fake.calls[0]
        self.assertEqual(30, timeout)
        self.assertEqual(512, request["max_completion_tokens"])
        self.assertEqual(["openai/default"], request["provider"]["only"])
        self.assertEqual(["openai/default"], request["provider"]["order"])
        self.assertFalse(request["provider"]["allow_fallbacks"])
        self.assertTrue(request["provider"]["require_parameters"])
        self.assertNotIn("models", request)
        self.assertNotIn("Authorization", str(request))
        self.assertEqual("completed", self.ledger.attempts[0].status)
        self.assertNotIn(self.context["observations"][0]["signal"], str(self.ledger))
        self.assertEqual(Decimal("0.00005"), Decimal(self.ledger.attempts[0].billed_usd))
        self.assertEqual(20, self.ledger.attempts[0].reasoning_tokens)
        self.assertTrue(result.cost_event["schema_valid"])
        self.assertEqual(0, result.cost_event["retry_count"])
        validate_packet_json(json.dumps(result.cost_event), SCHEMAS / "traces" / "cost-event.schema.json")

    def test_completion_has_exact_matched_baseline_and_refusal_is_not_packet(self):
        for case in self.cases:
            with self.subTest(case=case.case_id):
                self.ledger = TrialLedger()
                client, fake = self.client()
                for earlier in self.cases[:self.cases.index(case)]:
                    fake.reply = self.reply(earlier)
                    client.run_case(earlier.case_id)
                fake.reply = self.reply(case)
                result = client.run_case(case.case_id)
                self.assertEqual((), result.differences)
                self.assertEqual(case.baseline if case.schema_path else None, result.candidate)
                self.assertEqual(case.schema_path is not None, result.cost_event["schema_valid"])

    def test_no_dispatch_on_missing_or_unsupported_endpoint_proof(self):
        for proof in (None, EndpointProof("other/model", "openai/default", frozenset({"low"}), True, True,
                                          Decimal("0.10"), Decimal("0.50"), "proof"),
                      EndpointProof("openai/gpt-6-luna", "openai/default", frozenset({"high"}), True, True,
                                    Decimal("0.10"), Decimal("0.50"), "proof"),
                      EndpointProof("openai/gpt-6-luna", "openai/default", frozenset({"low"}), False, True,
                                    Decimal("0.10"), Decimal("0.50"), "proof")):
            with self.subTest(proof=proof):
                self.ledger = TrialLedger()
                client, fake = self.client(proof=proof if proof is not None else object())
                with self.assertRaises(TrialError):
                    client.run_case("work_order")
                self.assertFalse(fake.calls)
                self.assertEqual([], self.ledger.attempts)

    def test_budget_and_input_count_refuse_before_transport(self):
        for price, count in ((Decimal("5"), 100), (Decimal("0.10"), 2001), (Decimal("0.10"), -1)):
            with self.subTest(price=price, count=count):
                self.ledger = TrialLedger()
                proof = EndpointProof(self.proof.model_id, self.proof.endpoint_slug, self.proof.supported_efforts,
                                      True, True, price, Decimal("0.50"), "offline-proof-v1")
                client, fake = self.client(proof=proof, counter=lambda _: count)
                with self.assertRaises(TrialError):
                    client.run_case("work_order")
                self.assertFalse(fake.calls)

    def test_tampered_rendered_messages_mock_or_schema_never_dispatch(self):
        for mutation in ("messages", "baseline", "schema"):
            with self.subTest(mutation=mutation):
                self.ledger = TrialLedger()
                case = copy.deepcopy(self.cases[0])
                from dataclasses import replace
                if mutation == "messages":
                    case = replace(case, messages=({"role": "user", "content": "different"},))
                elif mutation == "baseline":
                    case = replace(case, baseline={"decision": "no_candidate"})
                else:
                    case = replace(case, schema_hash="bad")
                client, fake = self.client()
                client.cases = (case, *self.cases[1:])
                with self.assertRaises(TrialError):
                    client.run_case("work_order")
                self.assertFalse(fake.calls)

    def test_ignored_effort_and_provider_substitution_stop_after_one(self):
        for change in ("effort", "provider", "model"):
            with self.subTest(change=change):
                self.ledger = TrialLedger()
                client, fake = self.client(reply=self.reply(model="other/model") if change == "model" else None)
                if change == "effort":
                    fake.observed_effort = "high"
                if change == "provider":
                    fake.observed_slug = "other"
                with self.assertRaises(TrialError):
                    client.run_case("work_order")
                self.assertEqual(1, len(fake.calls))
                self.assertEqual("unresolved", self.ledger.attempts[0].status)
                with self.assertRaises(TrialError):
                    client.run_case("tool_query")

    def test_transport_timeout_is_uncertain_billing_and_not_retried(self):
        client, fake = self.client(reply=TimeoutError("fake confidential response"))
        with self.assertRaisesRegex(TrialError, "callback failed") as caught:
            client.run_case("work_order")
        self.assertNotIn("confidential", str(caught.exception))
        self.assertEqual(1, len(fake.calls))
        self.assertEqual("unresolved", self.ledger.attempts[0].status)
        self.assertIsNone(self.ledger.attempts[0].billed_usd)
        with self.assertRaises(TrialError):
            client.run_case("tool_query")
        self.assertEqual(1, len(fake.calls))

    def test_elapsed_timeout_and_unpriced_usage_lock_ledger(self):
        ticks = iter([0, 0, 31])
        client, fake = self.client(clock=lambda: next(ticks))
        with self.assertRaisesRegex(TrialError, "timed out"):
            client.run_case("work_order")
        self.assertTrue(self.ledger.stopped)
        self.ledger = TrialLedger()
        response = self.reply()
        response["usage"].pop("cost")
        client, fake = self.client(reply=response)
        with self.assertRaisesRegex(TrialError, "cost evidence"):
            client.run_case("work_order")
        self.assertTrue(self.ledger.stopped)

    def test_negative_unrecognized_or_unsafe_output_fails_closed(self):
        for raw in ('{"decision":"no_candidate","extra":1}', '{"decision":"no_candidate"} prose',
                    '{"decision":"no_candidate","decision":"no_candidate"}', 'not-json',
                    json.dumps({**self.cases[0].baseline, "issuer_layer": "L3"}),
                    json.dumps({**self.cases[0].baseline, "rationale": "hidden thermal debt sentinel"})):
            with self.subTest(raw=raw[:24]):
                self.ledger = TrialLedger()
                response = self.reply()
                response["choices"][0]["message"]["content"] = raw
                client, fake = self.client(reply=response)
                with self.assertRaises(TrialError) as caught:
                    client.run_case("work_order")
                self.assertNotIn("sentinel", str(caught.exception))
                self.assertTrue(self.ledger.stopped)

    def test_packet_id_difference_recorded_but_source_visibility_preserved(self):
        response = self.reply()
        candidate = {**self.cases[0].baseline, "packet_id": "draft_A1_trial_a7"}
        response["choices"][0]["message"]["content"] = json.dumps(candidate)
        client, _ = self.client(reply=response)
        result = client.run_case("work_order")
        self.assertEqual(("packet_id",), result.differences)
        self.assertEqual("draft_A1_trial_a7", result.candidate["packet_id"])

    def test_schema_valid_but_authority_invalid_tool_and_hidden_id_rejected(self):
        self.ledger = TrialLedger()
        response = self.reply(self.cases[1])
        tool = {**self.cases[1].baseline, "mode": "execute"}
        response["choices"][0]["message"]["content"] = json.dumps(tool)
        client, fake = self.client()
        fake.reply = self.reply(self.cases[0])
        client.run_case("work_order")
        fake.reply = response
        with self.assertRaises(TrialError):
            client.run_case("tool_query")
        self.assertEqual("unresolved", self.ledger.attempts[-1].status)
        self.ledger = TrialLedger()
        response = self.reply()
        response["choices"][0]["message"]["content"] = json.dumps(
            {**self.cases[0].baseline, "packet_id": "hidden_thermal_debt"})
        client, _ = self.client(reply=response)
        with self.assertRaises(TrialError):
            client.run_case("work_order")

    def test_unknown_prior_attempt_or_case_order_refuses_send(self):
        client, fake = self.client()
        with self.assertRaises(TrialError):
            client.run_case("tool_query")
        self.assertFalse(fake.calls)
        from ringfall_brain.providers.aster_trial import Attempt
        self.ledger.attempts.append(Attempt("work_order", "id", "input", "request", "baseline", self.config.model_id,
                                             self.proof.endpoint_slug, "low", "proof", "p1", "v1", 100, "0.001"))
        with self.assertRaises(TrialError):
            client.run_case("tool_query")
        self.assertFalse(fake.calls)

    def test_pro_and_batch_variants_are_not_trial_models(self):
        for suffix in ("-pro", ":pro", "-batch", ":batch"):
            with self.subTest(suffix=suffix):
                _, fake = self.client()
                with self.assertRaises(TrialError):
                    OfflineTrialClient(TrialConfig(self.config.model_id + suffix, "low", "offline_run_1"),
                                        self.cases, transport=fake, proof_for_call=lambda: self.proof,
                                        count_input_tokens=lambda _: 2000, ledger=TrialLedger(), harness=self.harness)
                self.assertFalse(fake.calls)

    def test_all_sent_fields_are_pinned_even_if_schema_allows_hidden_claims(self):
        for field in ("rationale", "intent", "belief_updates", "risk_flags"):
            with self.subTest(field=field):
                mutated = copy.deepcopy(self.pulse)
                if field == "belief_updates":
                    mutated[field][0]["claim"] = "hidden thermal debt sentinel"
                elif field == "risk_flags":
                    mutated[field].append("hidden thermal debt sentinel")
                else:
                    mutated[field] = "hidden thermal debt sentinel"
                with self.assertRaises(TrialError) as caught:
                    prepare_aster_cases(self.context, mutated,
                                        pulse_schema=SCHEMAS / "packets" / "avatar-pulse-packet.schema.json",
                                        tool_schema=self.tool_schema, work_order_schema=self.work_schema,
                                        prompt_version="a4-d-v1", profile_version="a1-v1")
                self.assertNotIn("sentinel", str(caught.exception))
        with self.assertRaises(TrialError):
            prepare_aster_cases(self.context, self.pulse,
                pulse_schema=SCHEMAS / "packets" / "avatar-pulse-packet.schema.json",
                tool_schema=self.tool_schema, work_order_schema=self.work_schema,
                prompt_version="forged", profile_version="a1-v1")
        client, fake = self.client()
        forged = replace(self.cases[0], messages=({"role": "user", "content": "hidden thermal debt sentinel"},))
        from ringfall_brain.providers.aster_trial import _digest
        forged = replace(forged, messages_hash=_digest(forged.messages))
        client.cases = (forged, *self.cases[1:])
        with self.assertRaises(TrialError):
            client.run_case("work_order")
        self.assertFalse(fake.calls)
        baseline = replace(self.cases[0], baseline={"decision": "no_candidate"})
        baseline = replace(baseline, baseline_hash=_digest(baseline.baseline))
        with self.assertRaises(TrialError):
            OfflineTrialClient(self.config, (baseline, *self.cases[1:]), transport=fake,
                proof_for_call=lambda: self.proof, count_input_tokens=lambda _: 2000,
                harness=self.harness, ledger=TrialLedger())

    def test_mock_actually_consumes_exact_task_and_rendered_input(self):
        from ringfall_brain.providers.aster_trial import _task_mock
        for case in self.cases:
            with self.subTest(case=case.case_id):
                self.assertEqual(case.baseline, _task_mock(case.messages, case.schema_path))
                payload = json.loads(case.messages[1]["content"])
                payload["task"] = "different task"
                changed = (case.messages[0], {"role": "user", "content": json.dumps(payload)})
                with self.assertRaises(TrialError):
                    _task_mock(changed, case.schema_path)

    def test_mock_rejects_unaccounted_actor_visible_field_for_all_three_cases(self):
        from ringfall_brain.providers.aster_trial import _task_mock
        for case in self.cases:
            with self.subTest(case=case.case_id):
                self.assertEqual(case.baseline, _task_mock(case.messages, case.schema_path))
                payload = json.loads(case.messages[1]["content"])
                payload["context"]["toolRefs"] = ["other_actor_tool"]
                altered = (case.messages[0], {"role": "user", "content": json.dumps(payload)})
                with self.assertRaises(TrialError):
                    _task_mock(altered, case.schema_path)

    def test_callback_mutated_prepared_case_refuses_before_reservation_even_with_safe_request(self):
        original_cases = self.cases
        for source in ("proof_message", "proof_baseline", "count", "pin",
                       "send_accessor", "final_clock"):
            with self.subTest(source=source):
                self.cases = copy.deepcopy(original_cases)
                self.ledger, self.harness = TrialLedger(), FakeHarness()
                times = {"calls": 0}
                fake = FakeTransport(self.reply(), self.harness)

                def mutate():
                    if source == "proof_baseline":
                        self.cases[0].baseline["task_type"] = "hidden thermal debt sentinel"
                    else:
                        self.cases[0].messages[1]["content"] += "hidden thermal debt sentinel"

                def proof():
                    if source.startswith("proof"):
                        mutate()
                    return self.proof

                def clock():
                    times["calls"] += 1
                    if source == "final_clock" and times["calls"] == 2:
                        mutate()
                    return 0.0

                def counter(_):
                    if source == "count":
                        mutate()
                    return 2000

                if source == "pin":
                    self.harness.on_pin = mutate
                if source == "send_accessor":
                    class AccessorTransport(FakeTransport):
                        @property
                        def send(inner):
                            mutate()
                            return FakeTransport.send.__get__(inner, FakeTransport)
                    fake = AccessorTransport(self.reply(), self.harness)
                client = OfflineTrialClient(self.config, self.cases, transport=fake,
                    proof_for_call=proof, count_input_tokens=counter,
                    harness=self.harness, ledger=self.ledger, clock=clock)
                with self.assertRaises(TrialError) as caught:
                    client.run_case("work_order")
                self.assertNotIn("sentinel", str(caught.exception))
                self.assertEqual([], fake.calls)
                self.assertEqual([], self.ledger.attempts)
                self.assertTrue(self.ledger.stopped)

    def test_pre_send_tool_baseline_execute_mutation_has_no_second_reservation(self):
        proof_calls = {"count": 0}

        def proof():
            proof_calls["count"] += 1
            if proof_calls["count"] == 2:
                self.cases[1].baseline["mode"] = "execute"
            return self.proof

        fake = FakeTransport(self.reply(), self.harness)
        client = OfflineTrialClient(self.config, self.cases, transport=fake,
            proof_for_call=proof, count_input_tokens=lambda _: 2000,
            harness=self.harness, ledger=self.ledger)
        client.run_case("work_order")
        fake.reply = self.reply(self.cases[1])
        with self.assertRaises(TrialError):
            client.run_case("tool_query")
        self.assertEqual(1, len(fake.calls))
        self.assertEqual(["completed"], [a.status for a in self.ledger.attempts])
        self.assertTrue(self.ledger.stopped)

    def test_mutated_baseline_during_tool_send_never_completes_execute(self):
        client, fake = self.client()
        client.run_case("work_order")
        fake.reply = self.reply(self.cases[1])
        fake.reply["choices"][0]["message"]["content"] = json.dumps(
            {**self.cases[1].baseline, "mode": "execute"})
        fake.on_send = lambda: self.cases[1].baseline.__setitem__("mode", "execute")
        with self.assertRaises(TrialError):
            client.run_case("tool_query")
        self.assertEqual(2, len(fake.calls))
        self.assertEqual(["completed", "unresolved"], [a.status for a in self.ledger.attempts])

    def test_slow_send_and_harness_accessors_refuse_before_dispatch(self):
        for source in ("send", "attest"):
            with self.subTest(source=source):
                self.ledger, self.harness = TrialLedger(), FakeHarness()
                time_state = {"now": 0.0}

                class SlowTransport(FakeTransport):
                    @property
                    def send(inner):
                        if source == "send":
                            time_state["now"] = 120.0
                        return FakeTransport.send.__get__(inner, FakeTransport)

                class SlowHarness(FakeHarness):
                    @property
                    def attest(inner):
                        if source == "attest":
                            time_state["now"] = 120.0
                        return FakeHarness.attest.__get__(inner, FakeHarness)

                harness = SlowHarness()
                fake = SlowTransport(self.reply(), harness)
                client = OfflineTrialClient(self.config, self.cases, transport=fake,
                    proof_for_call=lambda: self.proof, count_input_tokens=lambda _: 2000,
                    harness=harness, ledger=self.ledger, clock=lambda: time_state["now"])
                with self.assertRaisesRegex(TrialError, "time cap"):
                    client.run_case("work_order")
                self.assertEqual([], fake.calls)
                self.assertEqual([], self.ledger.attempts)

    def test_append_hook_cannot_mutate_case_or_advance_clock_before_send(self):
        original_cases = self.cases
        for mode in ("case", "clock"):
            with self.subTest(mode=mode):
                self.cases = copy.deepcopy(original_cases)
                self.ledger, self.harness = TrialLedger(), FakeHarness()
                state = {"now": 0.0, "append_calls": 0}

                class InjectedList(list):
                    def append(inner, value):
                        state["append_calls"] += 1
                        if mode == "case":
                            self.cases[0].messages[1]["content"] += "hidden thermal debt sentinel"
                        else:
                            state["now"] = 120.0
                        return super().append(value)

                injected = InjectedList()
                self.ledger.attempts = injected
                fake = FakeTransport(self.reply(), self.harness)
                with self.assertRaises(TrialError):
                    client = OfflineTrialClient(self.config, self.cases, transport=fake,
                        proof_for_call=lambda: self.proof, count_input_tokens=lambda _: 2000,
                        harness=self.harness, ledger=self.ledger, clock=lambda: state["now"])
                    client.run_case("work_order")
                self.assertEqual(0, state["append_calls"])
                self.assertEqual([], injected)
                self.assertEqual([], fake.calls)

    def test_callback_swapped_attempts_list_and_guard_refuse_and_release_original(self):
        for source in ("list", "list_subclass", "guard", "guard_final_clock"):
            with self.subTest(source=source):
                self.ledger, self.harness = TrialLedger(), FakeHarness()
                original_list, original_guard = self.ledger.attempts, self.ledger.guard
                replacement_guard = threading.Lock()
                append_calls = []

                class InjectedList(list):
                    def append(inner, value):
                        append_calls.append(value)
                        return super().append(value)

                fake = FakeTransport(self.reply(), self.harness)
                client = OfflineTrialClient(self.config, self.cases, transport=fake,
                    proof_for_call=lambda: self.proof, count_input_tokens=lambda _: 2000,
                    harness=self.harness, ledger=self.ledger)

                def swap():
                    if source in ("guard", "guard_final_clock"):
                        self.ledger.guard = replacement_guard
                    else:
                        self.ledger.attempts = [] if source == "list" else InjectedList()

                if source == "guard_final_clock":
                    calls = {"count": 0}
                    def clock():
                        calls["count"] += 1
                        if calls["count"] == 2:
                            swap()
                        return 0.0
                    client.clock = clock
                else:
                    self.harness.on_pin = swap
                with self.assertRaises(TrialError):
                    client.run_case("work_order")
                self.assertEqual([], original_list)
                self.assertEqual([], self.ledger.attempts)
                self.assertEqual([], append_calls)
                self.assertEqual([], fake.calls)
                self.assertTrue(original_guard.acquire(blocking=False))
                original_guard.release()
                if source in ("guard", "guard_final_clock"):
                    self.assertTrue(replacement_guard.acquire(blocking=False))
                    replacement_guard.release()

    def test_ledger_subclass_getter_proxy_fields_and_fake_lock_never_run(self):
        getter_calls = []

        class GetterLedger(TrialLedger):
            def __getattribute__(self, name):
                if name in ("guard", "attempts", "started"):
                    getter_calls.append(name)
                    raise TrialError("hidden thermal debt sentinel")
                return super().__getattribute__(name)

        class ProxyStr(str):
            def __eq__(self, other):
                getter_calls.append("comparison")
                raise TrialError("hidden thermal debt sentinel")

        class FakeLock:
            def acquire(self, **kwargs):
                getter_calls.append("acquire")
                return True

            def release(self):
                getter_calls.append("release")

        for kind in ("subclass", "run_id", "stopped", "lock"):
            with self.subTest(kind=kind):
                getter_calls.clear()
                self.ledger = GetterLedger() if kind == "subclass" else TrialLedger()
                if kind == "run_id":
                    self.ledger.run_id = ProxyStr(self.config.run_id)
                elif kind == "stopped":
                    self.ledger.stopped = ProxyStr("")
                elif kind == "lock":
                    self.ledger.guard = FakeLock()
                self.harness = FakeHarness()
                fake = FakeTransport(self.reply(), self.harness)
                with self.assertRaises(TrialError):
                    client = OfflineTrialClient(self.config, self.cases, transport=fake,
                        proof_for_call=lambda: self.proof, count_input_tokens=lambda _: 2000,
                        harness=self.harness, ledger=self.ledger)
                    client.run_case("work_order")
                self.assertEqual([], getter_calls)
                self.assertEqual([], fake.calls)

    def test_prior_attempt_proxy_scalar_is_rejected_before_next_callback(self):
        client, fake = self.client()
        client.run_case("work_order")
        invoked = []

        class ProxyStr(str):
            def __str__(self):
                invoked.append("str")
                raise TrialError("hidden thermal debt sentinel")

        self.ledger.attempts[0].billed_usd = ProxyStr("0.00005")
        fake.reply = self.reply(self.cases[1])
        with self.assertRaises(TrialError):
            client.run_case("tool_query")
        self.assertEqual([], invoked)
        self.assertEqual(1, len(fake.calls))
        self.assertEqual(1, len(self.ledger.attempts))

    def test_prior_attempt_subclass_getter_is_rejected_without_access(self):
        from ringfall_brain.providers.aster_trial import Attempt

        client, fake = self.client()
        client.run_case("work_order")
        invoked = []

        class GetterAttempt(Attempt):
            def __getattribute__(self, name):
                if name == "status":
                    invoked.append(name)
                    raise TrialError("hidden thermal debt sentinel")
                return super().__getattribute__(name)

        prior = self.ledger.attempts[0]
        self.ledger.attempts[0] = GetterAttempt(**vars(prior))
        fake.reply = self.reply(self.cases[1])
        with self.assertRaises(TrialError):
            client.run_case("tool_query")
        self.assertEqual([], invoked)
        self.assertEqual(1, len(fake.calls))
        self.assertEqual(1, len(self.ledger.attempts))

    def test_callback_proxy_on_prior_cost_is_never_used_for_budget(self):
        client, fake = self.client()
        client.run_case("work_order")
        invoked = []

        class ProxyStr(str):
            def __bool__(self):
                invoked.append("bool")
                raise TrialError("hidden thermal debt sentinel")

        def counter(_):
            self.ledger.attempts[0].billed_usd = ProxyStr("0.00005")
            return 2000

        client.count_input_tokens = counter
        fake.reply = self.reply(self.cases[1])
        with self.assertRaises(TrialError) as caught:
            client.run_case("tool_query")
        self.assertEqual([], invoked)
        self.assertNotIn("sentinel", str(caught.exception))
        self.assertEqual(1, len(fake.calls))
        self.assertEqual(1, len(self.ledger.attempts))

    def test_final_clock_proxy_scalar_refuses_without_overloaded_operator(self):
        invoked = []

        class ProxyFloat(float):
            def __rsub__(self, other):
                invoked.append("subtraction")
                raise TrialError("hidden thermal debt sentinel")

        def clock():
            if self.ledger.started is not None:
                self.ledger.started = ProxyFloat(0.0)
            return 0.0

        client, fake = self.client(clock=clock)
        with self.assertRaises(TrialError):
            client.run_case("work_order")
        self.assertEqual([], invoked)
        self.assertEqual([], self.ledger.attempts)
        self.assertEqual([], fake.calls)
        self.assertTrue(self.ledger.guard.acquire(blocking=False))
        self.ledger.guard.release()

    def test_clock_result_proxy_cannot_run_comparisons_before_send(self):
        invoked = []

        class ProxyFloat(float):
            def __lt__(self, other):
                invoked.append("comparison")
                raise TrialError("hidden thermal debt sentinel")

        client, fake = self.client(clock=lambda: ProxyFloat(0.0))
        with self.assertRaises(TrialError):
            client.run_case("work_order")
        self.assertEqual([], invoked)
        self.assertEqual([], fake.calls)
        self.assertEqual([], self.ledger.attempts)

    def test_dispatch_mutation_retains_actual_shared_reservation_and_stops(self):
        from ringfall_brain.providers.aster_trial import Attempt

        for mode in ("clear", "replace_list", "replace_guard", "replace_ledger_binding",
                     "replace_reservation", "mutate_reservation", "replace_prior", "mutate_prior"):
            with self.subTest(mode=mode):
                self.ledger, self.harness = TrialLedger(), FakeHarness()
                client, fake = self.client()
                prior = None
                if mode.endswith("prior"):
                    client.run_case("work_order")
                    prior = self.ledger.attempts[0]
                    fake.reply = self.reply(self.cases[1])
                peer = OfflineTrialClient(self.config, self.cases, transport=fake,
                    proof_for_call=lambda: self.proof, count_input_tokens=lambda _: 2000,
                    harness=self.harness, ledger=self.ledger)
                original_list, original_guard = self.ledger.attempts, self.ledger.guard
                sent = []

                def mutate():
                    actual = original_list[-1]
                    sent.append((actual, actual.usd_reserved))
                    if mode == "clear":
                        original_list.clear()
                    elif mode == "replace_list":
                        self.ledger.attempts = []
                    elif mode == "replace_guard":
                        self.ledger.guard = threading.Lock()
                    elif mode == "replace_ledger_binding":
                        client.ledger = TrialLedger()
                    elif mode == "replace_reservation":
                        original_list[-1] = Attempt(**vars(actual))
                    elif mode == "mutate_reservation":
                        actual.usd_reserved = "0"
                        actual.status = "completed"
                    elif mode == "replace_prior":
                        original_list[0] = Attempt(**vars(prior))
                    else:
                        prior.billed_usd = "0"
                        prior.status = "pending"

                fake.on_send = mutate
                case_id = "tool_query" if prior is not None else "work_order"
                before_sends = len(fake.calls)
                with self.assertRaises(TrialError):
                    client.run_case(case_id)
                self.assertEqual(before_sends + 1, len(fake.calls))
                self.assertIs(self.ledger, client.ledger)
                self.assertIs(original_list, self.ledger.attempts)
                self.assertIs(original_guard, self.ledger.guard)
                self.assertTrue(self.ledger.stopped)
                self.assertEqual(before_sends + 1, len(original_list))
                self.assertIs(original_list[-1], sent[0][0])
                self.assertEqual(sent[0][1], original_list[-1].usd_reserved)
                self.assertEqual("unresolved", original_list[-1].status)
                if prior is not None:
                    self.assertIs(prior, original_list[0])
                    self.assertEqual("completed", prior.status)
                    self.assertEqual("0.00005", prior.billed_usd)
                for other in (client, peer):
                    with self.assertRaises(TrialError):
                        other.run_case(case_id)
                    self.assertEqual(before_sends + 1, len(fake.calls))
                self.assertTrue(original_guard.acquire(blocking=False))
                original_guard.release()

    def test_late_callback_mutation_and_exception_cannot_complete(self):
        for mode in ("send_error", "attest", "clock", "response", "response_error"):
            with self.subTest(mode=mode):
                self.ledger, self.harness = TrialLedger(), FakeHarness()
                client, fake = self.client()
                original_list = self.ledger.attempts
                sent = []

                def erase():
                    sent.append((original_list[-1], original_list[-1].usd_reserved))
                    original_list.clear()

                if mode == "send_error":
                    def on_send():
                        erase()
                        raise RuntimeError("hidden thermal debt sentinel")
                    fake.on_send = on_send
                elif mode == "attest":
                    self.harness.on_attest = erase
                elif mode == "clock":
                    calls = 0
                    def clock():
                        nonlocal calls
                        calls += 1
                        if calls == 3:
                            erase()
                        return 0.0
                    client.clock = clock
                else:
                    class LateMessage(dict):
                        def __deepcopy__(inner, memo):
                            return inner

                        def get(inner, key, default=None):
                            if key == "content":
                                erase()
                                if mode == "response_error":
                                    raise TrialError("hidden thermal debt sentinel")
                            return super().get(key, default)

                    fake.reply = self.reply()
                    original_message = fake.reply["choices"][0]["message"]
                    fake.reply["choices"][0]["message"] = LateMessage(original_message)

                with self.assertRaises(TrialError) as caught:
                    client.run_case("work_order")
                self.assertNotIn("sentinel", str(caught.exception))
                self.assertIsNone(caught.exception.__context__)
                self.assertEqual(1, len(fake.calls))
                self.assertIs(self.ledger.attempts, original_list)
                self.assertEqual(1, len(original_list))
                self.assertIs(sent[0][0], original_list[0])
                self.assertEqual(sent[0][1], original_list[0].usd_reserved)
                self.assertEqual("unresolved", original_list[0].status)
                self.assertTrue(self.ledger.stopped)
                with self.assertRaises(TrialError):
                    client.run_case("work_order")
                self.assertEqual(1, len(fake.calls))

    def test_fresh_ledger_same_run_cannot_replay_old_attestation(self):
        first, fake = self.client()
        first.run_case("work_order")
        old = self.harness.observed
        old_id = self.ledger.attempts[0].attempt_id
        self.ledger = TrialLedger()
        second, substituted = self.client()
        substituted.observed_slug = "substitute"
        self.harness.override = old
        with self.assertRaises(TrialError):
            second.run_case("work_order")
        self.assertEqual(1, len(substituted.calls))
        self.assertNotEqual(old_id, self.ledger.attempts[0].attempt_id)
        self.assertEqual("unresolved", self.ledger.attempts[0].status)

    def test_duplicate_or_failed_dispatch_identity_cannot_send(self):
        for identity in ("a" * 32, "not-an-id", RuntimeError("hidden thermal debt sentinel")):
            with self.subTest(identity=str(type(identity))):
                self.ledger, self.harness = TrialLedger(), FakeHarness()
                client, fake = self.client()
                with patch("ringfall_brain.providers.aster_trial.secrets.token_hex") as make_id:
                    make_id.side_effect = identity if isinstance(identity, Exception) else None
                    make_id.return_value = None if isinstance(identity, Exception) else identity
                    if identity == "a" * 32:
                        # First use succeeds, even with a fixed fake generator.
                        client.run_case("work_order")
                        self.assertEqual(1, len(fake.calls))
                        self.ledger, self.harness = TrialLedger(), FakeHarness()
                        client, fake = self.client()
                    with self.assertRaises(TrialError) as caught:
                        client.run_case("work_order")
                self.assertNotIn("sentinel", str(caught.exception))
                self.assertEqual([], fake.calls)
                self.assertEqual([], self.ledger.attempts)

    def test_secret_bearing_proof_subclass_and_proxy_fields_never_escape(self):
        secret = "hidden thermal debt sentinel"

        class GetterProof(EndpointProof):
            def __getattribute__(self, name):
                if name == "endpoint_slug":
                    raise TrialError(secret)
                return super().__getattribute__(name)

        class ProxyStr(str):
            def __str__(self):
                raise TrialError(secret)

        for proof in (GetterProof(self.proof.model_id, self.proof.endpoint_slug,
                                  self.proof.supported_efforts, True, True,
                                  Decimal("0.10"), Decimal("0.50"), "proof"),
                      replace(self.proof, endpoint_slug=ProxyStr("openai/default")),
                      replace(self.proof, supported_efforts=frozenset({ProxyStr("low")}))):
            with self.subTest(kind=type(proof).__name__):
                self.ledger, self.harness = TrialLedger(), FakeHarness()
                client, fake = self.client(proof=proof)
                with self.assertRaises(TrialError) as caught:
                    client.run_case("work_order")
                self.assertNotIn(secret, str(caught.exception))
                self.assertIsNone(caught.exception.__cause__)
                self.assertIsNone(caught.exception.__context__)
                self.assertNotIn(secret, str(self.ledger))
                self.assertEqual([], fake.calls)
                self.assertEqual([], self.ledger.attempts)

    def test_secret_bearing_pre_send_accessors_are_non_echoing(self):
        secret = "hidden thermal debt sentinel"
        for source in ("send", "pin", "attest", "deadline"):
            with self.subTest(source=source):
                self.ledger = TrialLedger()

                class AccessorTransport(FakeTransport):
                    @property
                    def send(inner):
                        if source == "send":
                            raise TrialError(secret)
                        return FakeTransport.send.__get__(inner, FakeTransport)

                    @property
                    def enforces_deadline(inner):
                        if source == "deadline":
                            raise RuntimeError(secret)
                        return True

                class AccessorHarness(FakeHarness):
                    @property
                    def pin(inner):
                        if source == "pin":
                            raise TrialError(secret)
                        return FakeHarness.pin.__get__(inner, FakeHarness)

                    @property
                    def attest(inner):
                        if source == "attest":
                            raise RuntimeError(secret)
                        return FakeHarness.attest.__get__(inner, FakeHarness)

                harness = AccessorHarness()
                fake = AccessorTransport(self.reply(), harness)
                client = OfflineTrialClient(self.config, self.cases, transport=fake,
                    proof_for_call=lambda: self.proof, count_input_tokens=lambda _: 2000,
                    harness=harness, ledger=self.ledger)
                with self.assertRaises(TrialError) as caught:
                    client.run_case("work_order")
                self.assertNotIn(secret, str(caught.exception))
                self.assertIsNone(caught.exception.__cause__)
                self.assertIsNone(caught.exception.__context__)
                self.assertNotIn(secret, str(self.ledger))
                self.assertEqual([], fake.calls)
                self.assertEqual([], self.ledger.attempts)

    def test_complete_request_bound_is_not_caller_underestimate(self):
        client, fake = self.client(counter=lambda _: 100)
        with self.assertRaisesRegex(TrialError, "understates"):
            client.run_case("work_order")
        self.assertEqual([], fake.calls)
        self.ledger = TrialLedger()
        client, fake = self.client()
        client.run_case("work_order")
        request = fake.calls[0][0]
        self.assertLessEqual(_request_bound(request), 2000)
        self.assertGreater(_request_bound(request), sum(len(m["content"]) for m in request["messages"]))
        synthetic = copy.deepcopy(request)
        synthetic["messages"][1]["content"] += "é\\\"" * 10
        self.assertGreater(_request_bound(synthetic), _request_bound(request))
        for boundary in (2000, 2001):
            synthetic = copy.deepcopy(request)
            synthetic["messages"][1]["content"] += "x" * (boundary - _request_bound(request))
            self.assertEqual(boundary, _request_bound(synthetic))

    def test_reentry_from_callbacks_and_two_clients_one_ledger(self):
        for source in ("proof", "count", "clock", "transport", "pin"):
            with self.subTest(source=source):
                self.ledger, self.harness = TrialLedger(), FakeHarness()
                rejections = []
                holder = {}
                def reenter():
                    try:
                        holder["client"].run_case("work_order")
                    except TrialError as exc:
                        rejections.append(str(exc))
                proof = lambda: (reenter(), self.proof)[1] if source == "proof" else self.proof
                counter = (lambda _: (reenter(), 2000)[1]) if source == "count" else (lambda _: 2000)
                ticks = 0
                def clock():
                    nonlocal ticks
                    ticks += 1
                    if source == "clock" and ticks == 1:
                        reenter()
                    return 0.0
                fake = FakeTransport(self.reply(), self.harness)
                if source == "transport":
                    fake.on_send = reenter
                if source == "pin":
                    self.harness.on_pin = reenter
                holder["client"] = OfflineTrialClient(self.config, self.cases, transport=fake,
                    proof_for_call=proof, count_input_tokens=counter, harness=self.harness,
                    ledger=self.ledger, clock=clock)
                holder["client"].run_case("work_order")
                self.assertEqual(1, len(fake.calls))
                self.assertEqual(1, len(self.ledger.attempts))
                self.assertEqual(["trial is already in flight"], rejections)

        self.ledger, self.harness = TrialLedger(), FakeHarness()
        entered, release = threading.Event(), threading.Event()
        client1, fake = self.client()
        client2 = OfflineTrialClient(self.config, self.cases, transport=fake,
            proof_for_call=lambda: self.proof, count_input_tokens=lambda _: 2000,
            harness=self.harness, ledger=self.ledger)
        def hold_send():
            entered.set()
            if not release.wait(3):
                raise RuntimeError("fake wait exceeded")
        fake.on_send = hold_send
        errors = []
        thread = threading.Thread(target=lambda: self._run_in_thread(client1, errors))
        thread.start()
        try:
            self.assertTrue(entered.wait(3))
            with self.assertRaisesRegex(TrialError, "in flight"):
                client2.run_case("work_order")
        finally:
            release.set()
            thread.join(3)
        self.assertFalse(thread.is_alive())
        self.assertEqual([], errors)
        self.assertEqual(1, len(fake.calls))
        self.assertEqual(1, len(self.ledger.attempts))

    def test_remaining_run_wall_time_shortens_attempt_deadline(self):
        self.ledger, self.harness = TrialLedger(), FakeHarness()
        ticks = iter((0.0, 111.0, 112.0))
        fake = FakeTransport(self.reply(), self.harness)
        client = OfflineTrialClient(self.config, self.cases, transport=fake,
            proof_for_call=lambda: self.proof, count_input_tokens=lambda _: 2000,
            harness=self.harness, ledger=self.ledger, clock=lambda: next(ticks))
        client.run_case("work_order")
        self.assertEqual(9, fake.calls[0][1])
        self.assertEqual(1, len(self.ledger.attempts))

    @staticmethod
    def _run_in_thread(client, errors):
        try:
            client.run_case("work_order")
        except Exception as exc:
            errors.append(str(exc))

    def test_cooperative_deadline_and_slow_preflight_never_send(self):
        for source in ("proof", "count", "pin"):
            with self.subTest(source=source):
                self.ledger, self.harness = TrialLedger(), FakeHarness()
                time_state = {"now": 0.0}
                fake = FakeTransport(self.reply(), self.harness)
                def slow_proof():
                    if source == "proof":
                        time_state["now"] = 120.0
                    return self.proof
                def slow_count(_):
                    if source == "count":
                        time_state["now"] = 120.0
                    return 2000
                if source == "pin":
                    self.harness.on_pin = lambda: time_state.update(now=120.0)
                client = OfflineTrialClient(self.config, self.cases, transport=fake,
                    proof_for_call=slow_proof, count_input_tokens=slow_count,
                    harness=self.harness, ledger=self.ledger, clock=lambda: time_state["now"])
                with self.assertRaisesRegex(TrialError, "time cap"):
                    client.run_case("work_order")
                self.assertFalse(fake.calls)
                self.assertFalse(self.ledger.attempts)
        self.ledger, self.harness = TrialLedger(), FakeHarness()
        fake = FakeTransport(self.reply(), self.harness)
        fake.enforces_deadline = False
        client = OfflineTrialClient(self.config, self.cases, transport=fake,
            proof_for_call=lambda: self.proof, count_input_tokens=lambda _: 2000,
            harness=self.harness, ledger=self.ledger)
        with self.assertRaisesRegex(TrialError, "deadline"):
            client.run_case("work_order")
        self.assertFalse(fake.calls)
        self.ledger, self.harness = TrialLedger(), FakeHarness()
        time_state = {"now": 0.0}
        fake = FakeTransport(self.reply(), self.harness)
        def cooperative_wait():
            time_state["now"] = 30.0
            raise TimeoutError("hidden thermal debt sentinel")
        fake.on_send = cooperative_wait
        client = OfflineTrialClient(self.config, self.cases, transport=fake,
            proof_for_call=lambda: self.proof, count_input_tokens=lambda _: 2000,
            harness=self.harness, ledger=self.ledger, clock=lambda: time_state["now"])
        with self.assertRaises(TrialError) as caught:
            client.run_case("work_order")
        self.assertNotIn("sentinel", str(caught.exception))
        self.assertEqual("unresolved", self.ledger.attempts[0].status)
        with self.assertRaises(TrialError):
            client.run_case("tool_query")
        self.assertEqual(1, len(fake.calls))

    def test_offline_harness_evidence_cannot_be_raw_self_echo_or_stale(self):
        for mode in ("missing", "forged", "stale", "wrong_request", "wrong_route", "secret_callback"):
            with self.subTest(mode=mode):
                self.ledger, self.harness = TrialLedger(), FakeHarness()
                client, fake = self.client()
                if mode == "missing":
                    self.harness.override = "missing"
                elif mode == "forged":
                    self.harness.override = HarnessAttestation("forged", "hash", "run", "case", "model", "slug", "effort", "ref")
                elif mode == "stale":
                    fake.on_send = lambda: setattr(self.harness, "override", replace(self.harness.expected, attempt_id="old"))
                elif mode == "wrong_request":
                    fake.on_send = lambda: setattr(self.harness, "override", replace(self.harness.expected, request_hash="other"))
                elif mode == "wrong_route":
                    fake.observed_slug = "substitute"
                else:
                    self.harness.override = TrialError("hidden thermal debt sentinel")
                with self.assertRaises(TrialError) as caught:
                    client.run_case("work_order")
                self.assertNotIn("sentinel", str(caught.exception))
                self.assertEqual(1, len(fake.calls))
                self.assertEqual("unresolved", self.ledger.attempts[0].status)

    def test_every_injected_callback_exception_is_non_echoing(self):
        for source in ("proof", "count", "clock", "pin", "send", "attest"):
            for error_type in (RuntimeError, TrialError):
                with self.subTest(source=source, kind=error_type.__name__):
                    self.ledger, self.harness = TrialLedger(), FakeHarness()
                    secret = "hidden thermal debt sentinel"
                    error = error_type(secret)
                    def fail(*args, **kwargs):
                        raise error
                    client, fake = self.client()
                    if source == "proof":
                        client.proof_for_call = fail
                    elif source == "count":
                        client.count_input_tokens = fail
                    elif source == "clock":
                        client.clock = fail
                    elif source == "pin":
                        self.harness.on_pin = fail
                    elif source == "send":
                        fake.on_send = fail
                    else:
                        self.harness.on_attest = fail
                    with self.assertRaises(TrialError) as caught:
                        client.run_case("work_order")
                    import traceback
                    self.assertNotIn(secret, str(caught.exception))
                    self.assertNotIn(secret, "".join(traceback.format_exception_only(type(caught.exception), caught.exception)))
                    self.assertIsNone(caught.exception.__cause__)
                    self.assertIsNone(caught.exception.__context__)
                    self.assertNotIn(secret, str(self.ledger))
                    if source in ("send", "attest"):
                        self.assertEqual("unresolved", self.ledger.attempts[0].status)
                        self.assertEqual(1, len(fake.calls))
                    else:
                        self.assertFalse(fake.calls)
                        self.assertFalse(self.ledger.attempts)


if __name__ == "__main__":
    unittest.main()
