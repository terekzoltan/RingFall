from __future__ import annotations

import json
import sys
import tempfile
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
REPO_ROOT = ROOT.parents[1]
sys.path.insert(0, str(ROOT))

from ringfall_brain.providers.mock_provider import build_avatar_pulse_packet
from ringfall_brain.schemas.validator import validate_packet_json
from ringfall_brain.traces.artifacts import build_mock_cognition_artifacts, write_mock_cognition_artifacts


AVATAR_PULSE_SCHEMA = REPO_ROOT / "src" / "ringfall-contracts" / "schemas" / "packets" / "avatar-pulse-packet.schema.json"
COGNITION_TRACE_SCHEMA = REPO_ROOT / "src" / "ringfall-contracts" / "schemas" / "traces" / "cognition-trace.schema.json"
COST_EVENT_SCHEMA = REPO_ROOT / "src" / "ringfall-contracts" / "schemas" / "traces" / "cost-event.schema.json"


class TraceArtifactsTests(unittest.TestCase):
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


if __name__ == "__main__":
    unittest.main()
