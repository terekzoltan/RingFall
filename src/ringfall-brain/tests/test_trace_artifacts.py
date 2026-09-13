from __future__ import annotations

import json
import sys
import tempfile
import unittest
from copy import deepcopy
from pathlib import Path
from unittest.mock import patch

ROOT = Path(__file__).resolve().parents[1]
REPO_ROOT = ROOT.parents[1]
sys.path.insert(0, str(ROOT))

from ringfall_brain import artifact_transaction as transaction
from ringfall_brain.cognition import aster_action_emitter as emitter
from ringfall_brain.providers.mock_provider import build_avatar_pulse_packet
from ringfall_brain.schemas.validator import BrainValidationError, validate_packet_json
from ringfall_brain.traces import artifacts as trace_artifacts
from ringfall_brain.traces.artifacts import build_mock_cognition_artifacts, write_mock_cognition_artifacts


AVATAR_PULSE_SCHEMA = REPO_ROOT / "src" / "ringfall-contracts" / "schemas" / "packets" / "avatar-pulse-packet.schema.json"
COGNITION_TRACE_SCHEMA = REPO_ROOT / "src" / "ringfall-contracts" / "schemas" / "traces" / "cognition-trace.schema.json"
COST_EVENT_SCHEMA = REPO_ROOT / "src" / "ringfall-contracts" / "schemas" / "traces" / "cost-event.schema.json"
TOOL_ACTION_SCHEMA = REPO_ROOT / "src" / "ringfall-contracts" / "schemas" / "packets" / "tool-action-request.schema.json"
WORK_ORDER_SCHEMA = REPO_ROOT / "src" / "ringfall-contracts" / "schemas" / "packets" / "work-order-request.schema.json"
ASTER_CONTEXT = ROOT / "examples" / "aster-a1-context.example.json"
ASTER_PULSE = ROOT / "examples" / "aster-a1-pulse.example.json"


class TraceArtifactsTests(unittest.TestCase):
    def build_aster_candidates(self) -> dict[str, dict[str, object]]:
        return emitter.build_aster_action_candidates(
            json.loads(ASTER_CONTEXT.read_text(encoding="utf-8")),
            json.loads(ASTER_PULSE.read_text(encoding="utf-8")),
            AVATAR_PULSE_SCHEMA,
            TOOL_ACTION_SCHEMA,
            WORK_ORDER_SCHEMA,
        )

    def build_aster_artifacts(self) -> dict[str, dict[str, object]]:
        return trace_artifacts.build_aster_cognition_artifacts(
            self.build_aster_candidates(),
            TOOL_ACTION_SCHEMA,
            WORK_ORDER_SCHEMA,
            COGNITION_TRACE_SCHEMA,
            COST_EVENT_SCHEMA,
        )

    def test_mock_cognition_artifacts_validate_against_contract_schemas(self) -> None:
        artifacts = build_mock_cognition_artifacts(
            build_avatar_pulse_packet(),
            AVATAR_PULSE_SCHEMA,
            COGNITION_TRACE_SCHEMA,
            COST_EVENT_SCHEMA,
        )

        validate_packet_json(json.dumps(artifacts["cognition_trace"]), COGNITION_TRACE_SCHEMA)
        validate_packet_json(json.dumps(artifacts["cost_event"]), COST_EVENT_SCHEMA)
        self.assertTrue(artifacts["cognition_trace"]["schema_valid"])
        self.assertTrue(artifacts["cost_event"]["schema_valid"])

    def test_trace_and_cost_refs_are_consistent(self) -> None:
        artifacts = build_mock_cognition_artifacts(
            build_avatar_pulse_packet(),
            AVATAR_PULSE_SCHEMA,
            COGNITION_TRACE_SCHEMA,
            COST_EVENT_SCHEMA,
        )
        trace = artifacts["cognition_trace"]
        cost = artifacts["cost_event"]

        self.assertEqual("dev", trace["model_evidence"]["run_mode"])
        self.assertEqual("dev", cost["run_mode"])
        self.assertEqual(trace["cost_event_ref"]["ref_id"], cost["cost_event_id"])
        self.assertEqual(trace["cognition_id"], cost["cognition_id"])
        self.assertEqual(trace["cognition_id"], cost["cognition_trace_ref"]["ref_id"])
        self.assertEqual(0, cost["estimated_cost_usd"])
        self.assertEqual("free_or_mock_zero", cost["cost_estimate_status"])

    def test_write_mock_cognition_artifacts_writes_expected_files_only(self) -> None:
        artifacts = build_mock_cognition_artifacts(
            build_avatar_pulse_packet(),
            AVATAR_PULSE_SCHEMA,
            COGNITION_TRACE_SCHEMA,
            COST_EVENT_SCHEMA,
        )
        with tempfile.TemporaryDirectory() as temp_dir:
            written = write_mock_cognition_artifacts(artifacts, Path(temp_dir))
            names = sorted(path.name for path in Path(temp_dir).iterdir())

        self.assertEqual(["avatar-pulse-packet.json", "cognition-trace.json", "cost-event.json"], names)
        self.assertEqual(names, sorted(path.name for path in written))

    def test_aster_artifacts_validate_and_match_the_literal_reference_graph(self) -> None:
        artifacts = self.build_aster_artifacts()

        self.assertEqual(
            [
                emitter.TOOL_ACTION_KEY,
                emitter.WORK_ORDER_KEY,
                trace_artifacts.TOOL_COGNITION_TRACE_KEY,
                trace_artifacts.WORK_ORDER_COGNITION_TRACE_KEY,
                trace_artifacts.TOOL_COST_EVENT_KEY,
                trace_artifacts.WORK_ORDER_COST_EVENT_KEY,
            ],
            list(artifacts),
        )

        expected_context_ref = {
            "ref_id": "ctx_A1_aster_heat_t000",
            "ref_type": "context",
            "artifact_uri": "fixtures://contexts/aster-a1-context.example.json",
        }
        expected_prompt_ref = {
            "ref_id": "prompt-l1-pulse-template",
            "ref_type": "prompt",
            "artifact_uri": "fixtures://prompts/l1_pulse_context_template.md",
        }
        cases = (
            {
                "candidate_key": emitter.TOOL_ACTION_KEY,
                "trace_key": trace_artifacts.TOOL_COGNITION_TRACE_KEY,
                "cost_key": trace_artifacts.TOOL_COST_EVENT_KEY,
                "packet_id": "draft_A1_tool_heat_alarm_check",
                "cognition_id": "cog_A1_t000_tool_heat_alarm_check",
                "cost_id": "cost_A1_t000_tool_heat_alarm_check",
                "lane": "l1_tool_action",
                "candidate_uri": "fixtures://aster-a4-h/tool-action-request.json",
                "trace_uri": "fixtures://aster-a4-h/tool-action-cognition-trace.json",
                "cost_uri": "fixtures://aster-a4-h/tool-action-cost-event.json",
                "raw_id": "raw_draft_A1_tool_heat_alarm_check",
            },
            {
                "candidate_key": emitter.WORK_ORDER_KEY,
                "trace_key": trace_artifacts.WORK_ORDER_COGNITION_TRACE_KEY,
                "cost_key": trace_artifacts.WORK_ORDER_COST_EVENT_KEY,
                "packet_id": "draft_A1_work_order_heat_alarm_inspection",
                "cognition_id": "cog_A1_t000_work_order_heat_alarm_inspection",
                "cost_id": "cost_A1_t000_work_order_heat_alarm_inspection",
                "lane": "l1_work_order",
                "candidate_uri": "fixtures://aster-a4-h/work-order-request.json",
                "trace_uri": "fixtures://aster-a4-h/work-order-cognition-trace.json",
                "cost_uri": "fixtures://aster-a4-h/work-order-cost-event.json",
                "raw_id": "raw_draft_A1_work_order_heat_alarm_inspection",
            },
        )

        for case in cases:
            with self.subTest(candidate=case["candidate_key"]):
                trace = artifacts[case["trace_key"]]
                cost = artifacts[case["cost_key"]]
                candidate = artifacts[case["candidate_key"]]
                self.assertEqual(case["packet_id"], candidate["packet_id"])
                self.assertEqual(case["cognition_id"], trace["cognition_id"])
                self.assertEqual(case["cost_id"], cost["cost_event_id"])
                self.assertEqual("turn_A1_t000_aster_actions", trace["turn_ref"])
                self.assertEqual("turn_A1_t000_aster_actions", cost["turn_ref"])
                self.assertEqual(0, trace["tick"])
                self.assertEqual(0, cost["tick"])
                self.assertEqual(case["lane"], trace["lane"])
                self.assertEqual(case["lane"], cost["lane"])
                self.assertEqual("A1", trace["target_id"])
                self.assertEqual("A1", cost["target_id"])
                self.assertEqual("run_dev_Aster_t000", cost["run_id"])
                self.assertEqual(case["cognition_id"], cost["cognition_id"])
                self.assertEqual(expected_context_ref, trace["context_ref"])
                self.assertEqual(expected_context_ref, cost["context_ref"])
                self.assertEqual(expected_prompt_ref, trace["prompt_ref"])
                self.assertEqual(
                    {
                        "ref_id": case["raw_id"],
                        "ref_type": "raw_output",
                        "artifact_uri": case["candidate_uri"],
                    },
                    trace["raw_output_ref"],
                )
                expected_packet_ref = {
                    "ref_id": case["packet_id"],
                    "ref_type": "packet",
                    "artifact_uri": case["candidate_uri"],
                }
                self.assertEqual(expected_packet_ref, trace["parsed_packet_ref"])
                self.assertEqual(expected_packet_ref, cost["request_ref"])
                self.assertEqual(
                    {
                        "ref_id": case["cost_id"],
                        "ref_type": "cost_event",
                        "artifact_uri": case["cost_uri"],
                    },
                    trace["cost_event_ref"],
                )
                self.assertEqual(
                    {
                        "ref_id": case["cognition_id"],
                        "ref_type": "cognition_trace",
                        "artifact_uri": case["trace_uri"],
                    },
                    cost["cognition_trace_ref"],
                )
                self.assertEqual(
                    {
                        "provider": "deterministic_fixture",
                        "model_id": "fixture/aster-a4-e",
                        "run_mode": "dev",
                        "temperature": 0,
                        "max_output_tokens": 1,
                    },
                    trace["model_evidence"],
                )
                self.assertEqual(0, trace["retry_count"])
                self.assertFalse(trace["fallback_used"])
                self.assertEqual("deterministic_fixture", cost["provider"])
                self.assertEqual("fixture/aster-a4-e", cost["model_id"])
                self.assertEqual("dev", cost["run_mode"])
                self.assertEqual("free_or_mock_zero", cost["cost_estimate_status"])
                self.assertTrue(cost["free_model_used"])
                self.assertFalse(cost["fallback_used"])
                self.assertEqual("not_applicable", cost["budget_status"])
                for zero_field in (
                    "input_tokens",
                    "output_tokens",
                    "estimated_cost_usd",
                    "latency_ms",
                    "retry_count",
                    "fallback_count",
                ):
                    self.assertEqual(0, cost[zero_field])
                self.assertTrue(trace["schema_valid"])
                self.assertTrue(cost["schema_valid"])
                validate_packet_json(json.dumps(trace), COGNITION_TRACE_SCHEMA)
                validate_packet_json(json.dumps(cost), COST_EVENT_SCHEMA)

        serialized = json.dumps(artifacts, sort_keys=True).casefold()
        for forbidden in (
            "actiontrace",
            "executionresult",
            "state_diff",
            "state_patch",
            "world_state",
            "hidden thermal vulnerability",
            "thermal debt",
        ):
            self.assertNotIn(forbidden, serialized)

    def test_aster_traces_and_costs_validate_false_before_true(self) -> None:
        observed: list[tuple[str, bool]] = []
        real_validate = trace_artifacts.validate_packet_json

        def record_validation(raw: str, schema_path: Path) -> dict[str, object]:
            payload = json.loads(raw)
            if schema_path in {COGNITION_TRACE_SCHEMA, COST_EVENT_SCHEMA}:
                observed.append((schema_path.name, payload["schema_valid"]))
            return real_validate(raw, schema_path)

        with patch.object(trace_artifacts, "validate_packet_json", side_effect=record_validation):
            self.build_aster_artifacts()

        self.assertEqual(
            [
                (COGNITION_TRACE_SCHEMA.name, False),
                (COST_EVENT_SCHEMA.name, False),
                (COGNITION_TRACE_SCHEMA.name, True),
                (COST_EVENT_SCHEMA.name, True),
                (COGNITION_TRACE_SCHEMA.name, False),
                (COST_EVENT_SCHEMA.name, False),
                (COGNITION_TRACE_SCHEMA.name, True),
                (COST_EVENT_SCHEMA.name, True),
            ],
            observed,
        )

    def test_aster_artifact_builder_rejects_candidate_shape_drift(self) -> None:
        artifacts = self.build_aster_artifacts()
        candidate = deepcopy(artifacts[emitter.TOOL_ACTION_KEY])

        with self.assertRaisesRegex(BrainValidationError, "exactly"):
            trace_artifacts.build_aster_cognition_artifacts(
                {emitter.TOOL_ACTION_KEY: candidate},
                TOOL_ACTION_SCHEMA,
                WORK_ORDER_SCHEMA,
                COGNITION_TRACE_SCHEMA,
                COST_EVENT_SCHEMA,
            )

    def test_aster_builder_rejects_non_exact_a4_e_payloads_A4H_SR_001(self) -> None:
        hidden_marker = "hidden thermal debt must not be echoed"
        cases: list[tuple[str, dict[str, dict[str, object]]]] = []

        def add_case(name: str, key: str, changes: dict[str, object]) -> None:
            candidates = deepcopy(self.build_aster_candidates())
            candidates[key].update(changes)
            cases.append((name, candidates))

        add_case(
            "tool execute mode and hidden rationale",
            emitter.TOOL_ACTION_KEY,
            {"mode": "execute", "rationale": hidden_marker},
        )
        add_case(
            "work-order target drift",
            emitter.WORK_ORDER_KEY,
            {"target_location": "Aster/Hidden-Spine-99"},
        )
        add_case(
            "authority and visibility addition",
            emitter.WORK_ORDER_KEY,
            {"authority": "self_authorized", "visibility_intent": "private"},
        )
        add_case(
            "evidence drift",
            emitter.TOOL_ACTION_KEY,
            {"evidence_refs": ["unaccepted-observation"]},
        )
        add_case("additional field", emitter.TOOL_ACTION_KEY, {"confidence": 0.5})
        add_case("JSON scalar type drift", emitter.TOOL_ACTION_KEY, {"issued_at_tick": 0.0})
        for name, value in (
            ("NaN", float("nan")),
            ("positive infinity", float("inf")),
            ("negative infinity", float("-inf")),
        ):
            add_case(name, emitter.TOOL_ACTION_KEY, {"confidence": value})

        missing_field = deepcopy(self.build_aster_candidates())
        del missing_field[emitter.TOOL_ACTION_KEY]["action"]
        cases.append(("missing field", missing_field))

        for name, candidates in cases:
            with self.subTest(name=name):
                # Prior code accepted schema-valid drift when selected IDs/provenance still matched.
                with self.assertRaises(BrainValidationError) as raised:
                    trace_artifacts.build_aster_cognition_artifacts(
                        candidates,
                        TOOL_ACTION_SCHEMA,
                        WORK_ORDER_SCHEMA,
                        COGNITION_TRACE_SCHEMA,
                        COST_EVENT_SCHEMA,
                    )
                self.assertNotIn(hidden_marker, str(raised.exception))

    def test_aster_builder_compares_both_requests_before_validation_A4H_SR_001(self) -> None:
        tool_marker = "hidden-tool-enum-marker-a4h-sr-001"
        work_property_marker = "hidden-work-order-property-marker-a4h-sr-001"
        work_value_marker = "hidden-work-order-value-marker-a4h-sr-001"
        cases: list[tuple[str, dict[str, dict[str, object]], str, tuple[str, ...]]] = []

        tool_mismatch = self.build_aster_candidates()
        tool_mismatch[emitter.TOOL_ACTION_KEY]["mode"] = tool_marker
        cases.append(
            (
                "tool invalid enum",
                tool_mismatch,
                "tool_action_request candidate does not match the exact accepted A4-E payload",
                (tool_marker,),
            )
        )

        work_mismatch = self.build_aster_candidates()
        work_mismatch[emitter.WORK_ORDER_KEY][work_property_marker] = work_value_marker
        cases.append(
            (
                "work-order additional property",
                work_mismatch,
                "work_order_request candidate does not match the exact accepted A4-E payload",
                (work_property_marker, work_value_marker),
            )
        )

        for name, candidates, expected_error, markers in cases:
            with self.subTest(name=name):
                candidate_json = json.dumps(candidates, sort_keys=True, separators=(",", ":"))
                with patch.object(
                    trace_artifacts, "validate_packet_json", wraps=validate_packet_json
                ) as packet_validator:
                    with self.assertRaises(BrainValidationError) as raised:
                        trace_artifacts.build_aster_cognition_artifacts(
                            candidates,
                            TOOL_ACTION_SCHEMA,
                            WORK_ORDER_SCHEMA,
                            COGNITION_TRACE_SCHEMA,
                            COST_EVENT_SCHEMA,
                        )

                self.assertEqual(str(raised.exception), expected_error)
                packet_validator.assert_not_called()
                self.assertNotIn("candidate packet failed schema validation", str(raised.exception))
                self.assertNotIn(candidate_json, str(raised.exception))
                for marker in markers:
                    self.assertNotIn(marker, str(raised.exception))

    def test_aster_exact_requests_still_use_contract_schemas_A4H_SR_001(self) -> None:
        with patch.object(
            trace_artifacts, "validate_packet_json", wraps=validate_packet_json
        ) as packet_validator:
            trace_artifacts.build_aster_cognition_artifacts(
                self.build_aster_candidates(),
                TOOL_ACTION_SCHEMA,
                WORK_ORDER_SCHEMA,
                COGNITION_TRACE_SCHEMA,
                COST_EVENT_SCHEMA,
            )

        schema_paths = [call.args[1] for call in packet_validator.call_args_list]
        self.assertEqual(schema_paths.count(TOOL_ACTION_SCHEMA), 1)
        self.assertEqual(schema_paths.count(WORK_ORDER_SCHEMA), 1)

    def test_aster_writer_rejects_request_drift_before_transaction_A4H_SR_001(self) -> None:
        artifacts = self.build_aster_artifacts()
        artifacts[emitter.TOOL_ACTION_KEY]["mode"] = "execute"
        artifacts[emitter.TOOL_ACTION_KEY]["rationale"] = "hidden thermal debt"

        with tempfile.TemporaryDirectory() as temp_dir:
            output_dir = Path(temp_dir) / "artifacts"
            # Only transaction entry is intercepted; transaction behavior is covered by emitter tests.
            with patch.object(trace_artifacts, "write_verified_artifacts") as write_transaction:
                with self.assertRaises(BrainValidationError):
                    trace_artifacts.write_aster_cognition_artifacts(
                        artifacts,
                        output_dir,
                        TOOL_ACTION_SCHEMA,
                        WORK_ORDER_SCHEMA,
                        COGNITION_TRACE_SCHEMA,
                        COST_EVENT_SCHEMA,
                    )

            write_transaction.assert_not_called()
            self.assertFalse(output_dir.exists())

    def test_aster_writer_rejects_hidden_schema_drift_before_validation_A4H_SR_001(self) -> None:
        property_marker = "hidden-work-order-property-marker-a4h-sr-001"
        value_marker = "hidden-work-order-value-marker-a4h-sr-001"
        artifacts = self.build_aster_artifacts()
        artifacts[emitter.WORK_ORDER_KEY][property_marker] = value_marker

        with tempfile.TemporaryDirectory() as temp_dir:
            output_dir = Path(temp_dir) / "artifacts"
            with (
                patch.object(
                    trace_artifacts, "validate_packet_json", wraps=validate_packet_json
                ) as packet_validator,
                patch.object(trace_artifacts, "write_verified_artifacts") as write_transaction,
            ):
                with self.assertRaises(BrainValidationError) as raised:
                    trace_artifacts.write_aster_cognition_artifacts(
                        artifacts,
                        output_dir,
                        TOOL_ACTION_SCHEMA,
                        WORK_ORDER_SCHEMA,
                        COGNITION_TRACE_SCHEMA,
                        COST_EVENT_SCHEMA,
                    )

            self.assertEqual(
                str(raised.exception),
                "work_order_request candidate does not match the exact accepted A4-E payload",
            )
            self.assertNotIn(property_marker, str(raised.exception))
            self.assertNotIn(value_marker, str(raised.exception))
            packet_validator.assert_not_called()
            write_transaction.assert_not_called()
            self.assertFalse(output_dir.exists())

    def test_write_aster_artifacts_is_exact_deterministic_and_no_clobber(self) -> None:
        artifacts = self.build_aster_artifacts()
        with tempfile.TemporaryDirectory() as temp_dir:
            root = Path(temp_dir)
            first = root / "first"
            second = root / "second"
            first_written = trace_artifacts.write_aster_cognition_artifacts(
                artifacts,
                first,
                TOOL_ACTION_SCHEMA,
                WORK_ORDER_SCHEMA,
                COGNITION_TRACE_SCHEMA,
                COST_EVENT_SCHEMA,
            )
            second_written = trace_artifacts.write_aster_cognition_artifacts(
                artifacts,
                second,
                TOOL_ACTION_SCHEMA,
                WORK_ORDER_SCHEMA,
                COGNITION_TRACE_SCHEMA,
                COST_EVENT_SCHEMA,
            )

            self.assertEqual(list(trace_artifacts.ASTER_ARTIFACT_FILENAMES), [path.name for path in first_written])
            self.assertEqual(list(trace_artifacts.ASTER_ARTIFACT_FILENAMES), [path.name for path in second_written])
            for filename in trace_artifacts.ASTER_ARTIFACT_FILENAMES:
                self.assertEqual((first / filename).read_bytes(), (second / filename).read_bytes())
                self.assertTrue((first / filename).read_bytes().endswith(b"\n"))

            with self.assertRaisesRegex(BrainValidationError, "already exists"):
                trace_artifacts.write_aster_cognition_artifacts(
                    artifacts,
                    first,
                    TOOL_ACTION_SCHEMA,
                    WORK_ORDER_SCHEMA,
                    COGNITION_TRACE_SCHEMA,
                    COST_EVENT_SCHEMA,
                )

            self.assertEqual(list(trace_artifacts.ASTER_ARTIFACT_FILENAMES), [path.name for path in first_written])

    def test_write_aster_artifacts_rolls_back_the_whole_owned_bundle(self) -> None:
        artifacts = self.build_aster_artifacts()
        real_write = transaction._write_owned_payload
        call_count = 0

        def fail_fourth_write(record: transaction._OwnedOutput, payload: bytes) -> None:
            nonlocal call_count
            call_count += 1
            if call_count == 4:
                raise OSError("injected A4-H bundle write failure")
            real_write(record, payload)

        with tempfile.TemporaryDirectory() as temp_dir:
            output_dir = Path(temp_dir) / "artifacts"
            output_dir.mkdir()
            unrelated = output_dir / "unrelated.txt"
            unrelated.write_bytes(b"preserve-me\n")

            with patch.object(transaction, "_write_owned_payload", side_effect=fail_fourth_write):
                with self.assertRaisesRegex(OSError, "injected A4-H bundle write failure"):
                    trace_artifacts.write_aster_cognition_artifacts(
                        artifacts,
                        output_dir,
                        TOOL_ACTION_SCHEMA,
                        WORK_ORDER_SCHEMA,
                        COGNITION_TRACE_SCHEMA,
                        COST_EVENT_SCHEMA,
                    )

            self.assertEqual([unrelated], list(output_dir.iterdir()))
            self.assertEqual(b"preserve-me\n", unrelated.read_bytes())


if __name__ == "__main__":
    unittest.main()
