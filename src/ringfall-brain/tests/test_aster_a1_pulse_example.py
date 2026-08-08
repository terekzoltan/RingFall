from __future__ import annotations

from copy import deepcopy
import json
import re
import sys
import unittest
from collections.abc import Iterator
from pathlib import Path
from typing import Any


ROOT = Path(__file__).resolve().parents[1]
REPO_ROOT = ROOT.parents[1]
sys.path.insert(0, str(ROOT))

from ringfall_brain.schemas.validator import validate_packet_json


CONTEXT_PATH = ROOT / "examples" / "aster-a1-context.example.json"
PULSE_PATH = ROOT / "examples" / "aster-a1-pulse.example.json"
PROMPT_PATH = ROOT / "ringfall_brain" / "prompts" / "l1_pulse_context_template.md"
AVATAR_PULSE_SCHEMA = (
    REPO_ROOT
    / "src"
    / "ringfall-contracts"
    / "schemas"
    / "packets"
    / "avatar-pulse-packet.schema.json"
)

FORBIDDEN_STRUCTURAL_KEYS = {
    "authority",
    "availability",
    "capabilities",
    "capability",
    "executionResult",
    "execution_result",
    "hiddenEffects",
    "hidden_effects",
    "immediate_risk",
    "local_status",
    "permissions",
    "stateDiff",
    "state_diff",
    "status",
    "stress",
    "supportedActions",
    "supported_actions",
    "systemRefs",
    "system_refs",
    "fatigue",
    "worldState",
    "world_state",
}

UNCERTAINTY_MARKERS = frozenset({"may", "might", "suspect", "uncertain", "unknown"})

FORBIDDEN_NARRATIVE_TERMS = (
    "thermaldebt",
    "thermal debt",
    "debtlevel",
    "debt level",
    "hidden thermal vulnerability",
    "0.41",
    "0_41",
    "query_branch_load",
    "query_heat_alarm",
    "dry_run_reroute",
    "query_asset_status",
    "query_backlog",
    "dry_run_patch",
)


def iter_keys(value: Any) -> Iterator[str]:
    if isinstance(value, dict):
        for key, child in value.items():
            yield key
            yield from iter_keys(child)
    elif isinstance(value, list):
        for child in value:
            yield from iter_keys(child)


def iter_strings(value: Any) -> Iterator[str]:
    if isinstance(value, str):
        yield value
    elif isinstance(value, dict):
        for child in value.values():
            yield from iter_strings(child)
    elif isinstance(value, list):
        for child in value:
            yield from iter_strings(child)


def unsupported_structural_keys(value: Any) -> tuple[str, ...]:
    return tuple(sorted(FORBIDDEN_STRUCTURAL_KEYS.intersection(iter_keys(value))))


def belief_contract_errors(
    pulse: dict[str, Any], expected_evidence: list[str]
) -> tuple[str, ...]:
    beliefs = pulse.get("belief_updates")
    if not isinstance(beliefs, list) or len(beliefs) != 1:
        return ("belief_updates must contain exactly one entry",)

    belief = beliefs[0]
    if not isinstance(belief, dict):
        return ("belief update must be an object",)

    errors: list[str] = []
    claim = belief.get("claim")
    if not isinstance(claim, str) or not claim.strip():
        errors.append("belief claim must be a nonblank string")
    else:
        claim_tokens = set(re.findall(r"[a-z]+", claim.casefold(), flags=re.ASCII))
        if claim_tokens.isdisjoint(UNCERTAINTY_MARKERS):
            errors.append("belief claim must include an explicit uncertainty token")

    if belief.get("source_refs") != expected_evidence:
        errors.append("belief source_refs must match the accepted observation evidence")

    return tuple(errors)


class AsterA1PulseExampleTests(unittest.TestCase):
    def load_context(self) -> dict[str, Any]:
        return json.loads(CONTEXT_PATH.read_text(encoding="utf-8"))

    def load_pulse(self) -> dict[str, Any]:
        return json.loads(PULSE_PATH.read_text(encoding="utf-8"))

    def test_pulse_validates_and_serializes_semantically(self) -> None:
        raw_pulse = PULSE_PATH.read_text(encoding="utf-8")
        pulse = validate_packet_json(raw_pulse, AVATAR_PULSE_SCHEMA)

        self.assertEqual(pulse, json.loads(json.dumps(pulse)))

        canonical = json.dumps(
            pulse, sort_keys=True, separators=(",", ":"), ensure_ascii=True
        )
        self.assertEqual(
            canonical,
            json.dumps(
                json.loads(canonical),
                sort_keys=True,
                separators=(",", ":"),
                ensure_ascii=True,
            ),
        )
        canonical.encode("ascii")

    def test_pulse_is_bound_to_the_accepted_actor_context(self) -> None:
        context = self.load_context()
        pulse = self.load_pulse()
        observation = context["observations"][0]

        self.assertEqual("A1", pulse["issuer_id"])
        self.assertEqual(pulse["issuer_id"], pulse["actor_id"])
        self.assertEqual(pulse["actor_id"], context["actorId"])
        self.assertNotIn("issued_at_turn", pulse)
        self.assertEqual([observation["signal"]], pulse["observed"])

        expected_evidence = [
            observation["observationId"],
            observation["sourceRef"],
        ]
        self.assertEqual(expected_evidence, pulse["evidence_refs"])
        self.assertEqual((), belief_contract_errors(pulse, expected_evidence))

    def test_belief_contract_rejects_empty_and_definite_variants(self) -> None:
        context = self.load_context()
        pulse = self.load_pulse()
        observation = context["observations"][0]
        expected_evidence = [
            observation["observationId"],
            observation["sourceRef"],
        ]

        without_belief = deepcopy(pulse)
        without_belief["belief_updates"] = []
        self.assertEqual(
            ("belief_updates must contain exactly one entry",),
            belief_contract_errors(without_belief, expected_evidence),
        )

        definite_belief = deepcopy(pulse)
        definite_belief["belief_updates"][0]["claim"] = (
            "Deferred maintenance caused the local alarm."
        )
        self.assertEqual(
            ("belief claim must include an explicit uncertainty token",),
            belief_contract_errors(definite_belief, expected_evidence),
        )

        substring_only_belief = deepcopy(pulse)
        substring_only_belief["belief_updates"][0]["claim"] = (
            "A mayday signal proves deferred maintenance caused the local alarm."
        )
        self.assertEqual(
            ("belief claim must include an explicit uncertainty token",),
            belief_contract_errors(substring_only_belief, expected_evidence),
        )

    def test_requested_packets_remain_drafts(self) -> None:
        pulse = self.load_pulse()

        self.assertEqual(
            {"SceneActionPacket", "ToolActionRequest", "WorkOrderRequest"},
            {item["packet_type"] for item in pulse["requested_packets"]},
        )
        for item in pulse["requested_packets"]:
            self.assertEqual({"packet_type", "draft_ref"}, set(item))
            self.assertTrue(item["draft_ref"].startswith("draft_A1_"))

    def test_structural_and_narrative_safety_are_independent(self) -> None:
        pulse = self.load_pulse()

        self.assertEqual((), unsupported_structural_keys(pulse))

        narrative = "\n".join(iter_strings(pulse)).lower()
        for forbidden in FORBIDDEN_NARRATIVE_TERMS:
            self.assertNotIn(forbidden, narrative)

        self.assertIn("authority", pulse["intent"])

    def test_schema_valid_local_status_is_rejected_by_context_guard(self) -> None:
        pulse = self.load_pulse()
        with_local_status = deepcopy(pulse)
        with_local_status["local_status"] = {
            "stress": 0.4,
            "fatigue": 0.5,
            "immediate_risk": "medium",
        }

        schema_valid_packet = validate_packet_json(
            json.dumps(with_local_status), AVATAR_PULSE_SCHEMA
        )
        self.assertEqual(
            ("fatigue", "immediate_risk", "local_status", "stress"),
            unsupported_structural_keys(schema_valid_packet),
        )

    def test_context_and_pulse_examples_are_ascii(self) -> None:
        CONTEXT_PATH.read_bytes().decode("ascii")
        PULSE_PATH.read_bytes().decode("ascii")

    def test_prompt_declares_stable_a4_d_boundaries(self) -> None:
        prompt = PROMPT_PATH.read_text(encoding="utf-8")

        for required in (
            "## A4-D A1 Context Binding",
            "trusted pre-materialized Core state",
            "issuer_id == actor_id == context.actorId == A1",
            "## A4-D Pulse And Scene Boundary",
            "src/ringfall-brain/examples/aster-a1-context.example.json",
            "src/ringfall-brain/examples/aster-a1-pulse.example.json",
            "proposed request target",
            "A4-E owns candidate emission",
            "A4-F owns Core authority validation",
            "A4-I owns vertical provenance and hidden-leak evidence",
        ):
            self.assertIn(required, prompt)


if __name__ == "__main__":
    unittest.main()
