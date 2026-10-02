"""Successor parser controls and opt-in real, pinned Headless-to-Refinery gates."""

from __future__ import annotations

import copy
import json
import os
import subprocess
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

from map_p1 import MappingFailure, sha
from run_preflight_v1 import ROOT, parse_preflight, run, strict
from test_aster_f1 import CASES, prepare


class PreflightContractTests(unittest.TestCase):
    def test_duplicate_or_nonfinite_json_is_rejected(self) -> None:
        for raw in (b'{"record_type":1,"record_type":1}', b'{"fact":NaN}', b'{"fact":1e999}'):
            with self.subTest(raw=raw), self.assertRaises(MappingFailure):
                strict(raw)

    @staticmethod
    def sample() -> tuple[dict, dict[str, bytes]]:
        packet = {"packet_id": "one", "packet_type": "WorkOrderRequest", "source_context_id": "ctx"}
        pulse = {"source_context_id": "ctx", "requested_packets": [{"draft_ref": "one", "packet_type": "WorkOrderRequest"}]}
        inputs = {"state": b"{}", "candidate": json.dumps(packet).encode(), "pulse": json.dumps(pulse).encode(), "context": b'{"actorId":"A1"}'}
        packet["issuer_id"] = "A1"
        inputs["candidate"] = json.dumps(packet).encode()
        record = {
            "record_type": "AsterF1Preflight", "contract_version": "1.0",
            "family_id": "aster-l1-action-work-order", "family_version": "0.1",
            "input_versions": {"state": "0.1", "candidate": "0.1", "pulse": "0.1", "context_format": "actor_context_projection"},
            "inputs": {**{name + "_input_sha256": sha(raw) for name, raw in inputs.items()},
                       "packet_id": "one", "packet_id_sha256": sha(b"one"),
                       "source_context_id": "ctx", "source_context_id_sha256": sha(b"ctx")},
            "initial_state_sha256": sha(b"{}"),
            "binding": {"status": "matched_projection_and_pulse", "evidence_ref_count": 1},
            "core": {"status": "allowed", "issues": []},
            "facts": {"packet_kind": "WorkOrderRequest", "issuer_id": "A1", "issuer_layer": "L1",
                      "issuer_home_sector_id": "Aster", "tool": None,
                       "work_order": {"crew_id": "crew_aster_repair_02", "crew_status": "available",
                           "assigned_actor_id": "A1", "crew_home_sector_id": "Aster", "crew_system_refs": [],
                           "actor_crew_referenced": True, "target_location": "Aster/Grid-Spine-03",
                           "task_type": "inspect_and_patch",
                           "target_crew_id": {"name": "target_crew_id", "presence": "value", "value": "crew_aster_repair_02", "coverage": "guarded_by_core_validator"},
                           "target_crew_pool_id": {"name": "target_crew_pool_id", "presence": "absent", "value": None, "coverage": "guarded_by_core_validator"},
                           "options": []}, "tool_arguments": []},
            "completeness": "complete", "unsupported_predicates": [],
            "execution": "not_run", "schema_validation": "not_run"
        }
        return record, inputs

    def test_execution_shaped_or_swapped_preflight_is_rejected_before_solver(self) -> None:
        record, inputs = self.sample()
        self.assertEqual("one", parse_preflight(json.dumps(record).encode(), inputs)["inputs"]["packet_id"])
        for field, value in (("state", {"changed": False}), ("links", {}), ("execution", "succeeded"),
                             ("schema_validation", "passed")):
            altered = copy.deepcopy(record)
            altered[field] = value
            with self.subTest(field=field), self.assertRaises(MappingFailure):
                parse_preflight(json.dumps(altered).encode(), inputs)
        altered_inputs = {**inputs, "candidate": b"{}"}
        with self.assertRaises(MappingFailure):
            parse_preflight(json.dumps(record).encode(), altered_inputs)

    def test_mapping_only_produces_instance_without_spawning_solver(self) -> None:
        record, inputs = self.sample()
        with tempfile.TemporaryDirectory(prefix="preflight-mapping-only-") as folder:
            path = Path(folder) / "instance.problem"
            with patch("run_preflight_v1.verify_sources", return_value="frozen-source-test"), patch(
                    "subprocess.Popen", side_effect=AssertionError("mapping attempted subprocess")):
                result = run(json.dumps(record).encode(), inputs, path)
            self.assertEqual([], result["diagnostics"])
            self.assertEqual("frozen-source-test", result["source_aggregate"])
            self.assertEqual(sha(path.read_bytes()), result["instance_sha256"])
            self.assertIn(b"crewAvailable(c1): true.", path.read_bytes())
            self.assertNotIn("check", result)
            self.assertNotIn("witness_closed", result)


@unittest.skipUnless(os.environ.get("RINGFALL_REAL_GATE") == "1", "opt-in pinned local Docker evidence")
class RealPreExecutionGateTests(unittest.TestCase):
    def test_bound_real_work_query_and_negative_controls(self) -> None:
        # Reuse accepted fixture recipes, but invoke only the NEW pre-execution command.
        names = ("work-order-valid", "tool-query-valid", "tool-reroute-valid",
                 "crew-unavailable", "tool-unavailable", "tool-execute",
                 "reroute-no-dry-run", "reroute-missing-fraction", "reroute-zero",
                 "reroute-negative", "reroute-over-bound", "tool-unreferenced", "unknown-argument")
        lookup = {case["name"]: case for case in CASES["cases"]}
        root = Path(tempfile.gettempdir()) / "opencode"
        root.mkdir(exist_ok=True)
        for name in names:
            with self.subTest(name=name), tempfile.TemporaryDirectory(prefix="gate-v1-test-", dir=root) as parent:
                folder = Path(parent)
                paths = prepare(lookup[name], folder)
                output = folder / "bundle"
                command = ["dotnet", "run", "--no-restore", "--project",
                           str(ROOT / "src/ringfall-core/Ringfall.Headless/Ringfall.Headless.csproj"), "--",
                           "aster-f1-gated-execute"]
                for flag, path in zip(("state", "candidate", "pulse", "context"), paths):
                    command += ["--" + flag, str(path)]
                command += ["--out", str(output)]
                result = subprocess.run(command, cwd=str(ROOT), capture_output=True, timeout=360, check=False)
                if lookup[name]["verdict"] == "valid":
                    self.assertEqual(0, result.returncode, result.stderr.decode(errors="replace"))
                    self.assertTrue(output.is_dir())
                    preflight = json.loads((output / "preflight.json").read_bytes())
                    report = json.loads((output / "solver-report.json").read_bytes())
                    bundle = json.loads((output / "manifest.json").read_bytes())
                    self.assertEqual("not_run", preflight["execution"])
                    self.assertEqual("not_run", bundle["schema_validation"])
                    self.assertEqual("valid", report["verdict"])
                    self.assertTrue(report["witness_closed"])
                    self.assertEqual("agreed", report["agreement"])
                    self.assertEqual(0, report["check"]["exit_code"])
                    self.assertEqual(0, report["generated"]["exit_code"])
                    self.assertEqual(report["check"]["instance_sha256"], report["generated"]["instance_sha256"])
                    self.assertEqual(report["check"]["invocation_id"], report["generated"]["invocation_id"])
                    if name == "work-order-valid":
                        self.assertEqual("succeeded", bundle["disposition"])
                        self.assertTrue((output / "state-diff.json").exists())
                    elif name == "tool-query-valid":
                        self.assertEqual("deferred", bundle["disposition"])
                        self.assertFalse((output / "state-diff.json").exists())
                    else:
                        self.assertIn(bundle["disposition"], ("succeeded", "deferred"))
                        self.assertFalse((output / "state-diff.json").exists())
                else:
                    self.assertNotEqual(0, result.returncode)
                    self.assertFalse(output.exists())
                    if name == "crew-unavailable":
                        self.assertIn(b"core_rejected:crew_unavailable", result.stderr)
                    elif lookup[name]["verdict"] == "unsupported":
                        self.assertIn(b"facts_or_core_issue_unmodeled", result.stderr)
                    else:
                        self.assertIn(b"core_rejected:", result.stderr)
