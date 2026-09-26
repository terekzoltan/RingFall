"""Independent Step 7 evidence-integrity controls; real solver is the CLI matrix."""

from __future__ import annotations

import copy
import json
from pathlib import Path
import tempfile
import unittest
from unittest import mock

import aster_f1_step7_eval as step7


class Step7EvidenceTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.temp = tempfile.TemporaryDirectory(prefix="ringfall-step7-tests-")
        cls.root = Path(cls.temp.name)
        cls.bundle = cls.root / "a4h"
        cls.graph = step7.generate(cls.bundle)
        cls.mapper, cls.runner = step7.supplier()

    @classmethod
    def tearDownClass(cls) -> None:
        cls.temp.cleanup()

    def test_six_files_and_two_dev_mock_chains(self) -> None:
        self.assertEqual(6, len(self.graph["files"]))
        self.assertEqual("observability_only", self.graph["coverage"]["trace_cost"])
        self.assertEqual(self.graph, step7.validate_bundle(self.bundle))

    def test_crossed_ref_schema_valid_but_graph_rejected(self) -> None:
        with tempfile.TemporaryDirectory(dir=self.root) as temp:
            target = Path(temp)
            for filename in self.graph["files"]:
                (target / filename).write_bytes((self.bundle / filename).read_bytes())
            trace = target / "tool-action-cognition-trace.json"
            value = step7.load(trace)
            value["cost_event_ref"]["ref_id"] = "cost_A1_t000_work_order_heat_alarm_inspection"
            import json
            trace.write_text(json.dumps(value), encoding="utf-8")
            self.assertTrue(step7.schema_valid(value, "traces/cognition-trace.schema.json"))
            with self.assertRaisesRegex(step7.EvidenceError, "graph_reference_mismatch"):
                step7.validate_bundle(target)

    def test_schema_valid_mock_cost_retry_is_not_accepted_provenance(self) -> None:
        with tempfile.TemporaryDirectory(dir=self.root) as temp:
            target = Path(temp)
            for filename in self.graph["files"]:
                (target / filename).write_bytes((self.bundle / filename).read_bytes())
            cost = target / "tool-action-cost-event.json"
            value = step7.load(cost)
            value["retry_count"] = 1
            import json
            cost.write_text(json.dumps(value), encoding="utf-8")
            self.assertTrue(step7.schema_valid(value, "traces/cost-event.schema.json"))
            with self.assertRaisesRegex(step7.EvidenceError, "mock_provenance_mismatch"):
                step7.validate_bundle(target)

    def test_cognition_and_complete_context_prompt_refs_are_required(self) -> None:
        for filename, field, component, replacement in (
            ("tool-action-cost-event.json", "cognition_id", None, "cog_other"),
            ("work-order-cost-event.json", "context_ref", "artifact_uri", "fixtures://contexts/other.json"),
            ("tool-action-cost-event.json", "context_ref", "ref_id", "ctx_other"),
            ("tool-action-cognition-trace.json", "context_ref", "ref_type", "prompt"),
            ("work-order-cognition-trace.json", "prompt_ref", "ref_id", "prompt-other"),
            ("tool-action-cognition-trace.json", "prompt_ref", "ref_type", "context"),
            ("tool-action-cognition-trace.json", "prompt_ref", "artifact_uri", "fixtures://prompts/other.md"),
        ):
            with self.subTest(filename=filename, field=field, component=component):
                with tempfile.TemporaryDirectory(dir=self.root) as temp:
                    target = Path(temp)
                    for source in self.graph["files"]:
                        (target / source).write_bytes((self.bundle / source).read_bytes())
                    path = target / filename
                    value = step7.load(path)
                    if component is None:
                        value[field] = replacement
                    else:
                        value[field][component] = replacement
                    schema = "traces/cost-event.schema.json" if "cost-event" in filename else "traces/cognition-trace.schema.json"
                    self.assertTrue(step7.schema_valid(value, schema))
                    path.write_text(json.dumps(value), encoding="utf-8")
                    with self.assertRaisesRegex(step7.EvidenceError, "graph_(identity|context)_mismatch"):
                        step7.validate_bundle(target)

    def test_hidden_text_and_mutation_claim_block_but_numeric_values_do_not(self) -> None:
        step7.no_actor_leak({"confidence": 0.41, "cost": 0.46})
        packet = step7.load(self.bundle / "tool-action-request.json")
        for phrase in ("hidden metric 0.41", "Hidden-Metric: 0.41", "hidden_metric 0.41"):
            with self.subTest(phrase=phrase):
                altered = dict(packet, rationale=phrase)
                self.assertTrue(step7.schema_valid(altered, "packets/tool-action-request.schema.json"))
                with self.assertRaisesRegex(step7.EvidenceError, "actor_hidden_leak"):
                    step7.no_actor_leak(altered)
        with self.assertRaisesRegex(step7.EvidenceError, "nonfinite_json"):
            step7.strict_object(b'{"fraction":1e999}')
        for value in ({"rationale": "thermalDebt 0.41"}, {"rationale": "thermal-debt"},
                      {"thermal_debt": 0.41},
                      {"state_diff": {"changes": []}}):
            with self.subTest(value=value), self.assertRaises(step7.EvidenceError):
                step7.no_actor_leak(value)

    def test_each_case_name_is_pinned_before_generation(self) -> None:
        original = step7.load(step7.CASES)
        self.assertEqual(original["cases"], step7.required_cases(original))
        for field, replacement in (("change", "crew-unavailable"), ("stage", "visibility"),
                                   ("verdict", "valid"), ("issues", ["crew_unavailable"])):
            suite = copy.deepcopy(original)
            target = next(row for row in suite["cases"] if row["name"] == "tool-unavailable")
            target[field] = replacement
            self.assertEqual(11, len({row["name"] for row in suite["cases"]}))
            with self.subTest(field=field), tempfile.TemporaryDirectory(dir=self.root) as temp:
                path = Path(temp) / "cases.json"
                path.write_text(json.dumps(suite), encoding="utf-8")
                with mock.patch.object(step7, "CASES", path), mock.patch.object(
                    step7, "generate", side_effect=AssertionError("generation was reached")):
                    with self.assertRaisesRegex(step7.EvidenceError, "case_recipe_mismatch"):
                        step7.evaluate(step7.source_snapshot()["aggregate"])

    def test_private_solver_bytes_and_report_fail_closed_on_retention_loss(self) -> None:
        capture = self.runner.Captured(("docker", self.runner.IMAGE, "check"), 1,
                                       b"synthetic-retention-control\n", b"")
        source = step7.source_snapshot()
        case = {"name": "tool-unavailable", "expected_stage": "differential", "expected_verdict": "invalid",
                "hard_pass": False, "input_sha256": {"state": "a" * 64}, "core_evidence_sha256": "b" * 64,
                "solver_report_sha256": "c" * 64, "model_sha256": step7.sha(self.runner.MODEL.read_bytes()),
                "instance_sha256": "d" * 64, "solver_runs": [capture.receipt()]}
        report = {"source": source, "cases": [case]}
        for fault in ("missing", "truncated", "altered", "unreadable", "report_missing",
                      "report_truncated", "report_altered", "report_unreadable", "source_drift"):
            with self.subTest(fault=fault), tempfile.TemporaryDirectory(
                prefix="ringfall-step7-tests-", dir=step7.PRIVATE_ROOT) as temp:
                directory = Path(temp)
                proof = step7.write_private_evidence(directory, report, {case["name"]: [capture]}, self.runner)
                self.assertEqual(source["aggregate"], proof["source_aggregate"])
                self.assertEqual(2, proof["capture_files"])
                if fault == "source_drift":
                    with mock.patch.object(step7, "source_snapshot", return_value={"aggregate": "0" * 64}):
                        with self.assertRaisesRegex(step7.EvidenceError, "private_report_or_source_mismatch"):
                            step7.verify_private_evidence(directory)
                    continue
                file = directory / ("report.json" if fault.startswith("report_") else "tool-unavailable-0.stdout")
                if fault in ("missing", "report_missing"):
                    file.unlink()
                elif fault in ("truncated", "report_truncated"):
                    file.write_bytes(file.read_bytes()[:-1])
                elif fault in ("altered", "report_altered"):
                    raw = file.read_bytes()
                    file.write_bytes(raw[:1] + bytes([raw[1] ^ 1]) + raw[2:])
                else:
                    original = Path.read_bytes
                    def unreadable(path: Path) -> bytes:
                        if path == file:
                            raise PermissionError("unreadable proof")
                        return original(path)
                    with mock.patch.object(Path, "read_bytes", unreadable):
                        with self.assertRaisesRegex(step7.EvidenceError, "private_evidence_unreadable"):
                            step7.verify_private_evidence(directory)
                    continue
                with self.assertRaises(step7.EvidenceError):
                    step7.verify_private_evidence(directory)

    def test_schema_context_and_hidden_negative_controls_do_not_run_solver(self) -> None:
        for name, diagnostic in (("missing-mode", "schema_invalid"),
                                 ("context-mismatch", "context_mismatch"),
                                 ("actor-hidden-leak", "actor_hidden_leak")):
            case = next(item for item in step7.load(step7.CASES)["cases"] if item["name"] == name)
            with self.subTest(case=name):
                evidence = step7.evaluate_case(case, self.bundle, self.root, self.mapper, self.runner)
                self.assertFalse(evidence["hard_pass"])
                self.assertEqual(diagnostic, evidence["diagnostic"])
                self.assertNotIn("solver_runs", evidence)

    def test_fresh_core_and_effect_link_tamper_fail_closed(self) -> None:
        case = next(item for item in step7.load(step7.CASES)["cases"] if item["name"] == "work-order")
        with tempfile.TemporaryDirectory(dir=self.root) as temp:
            paths = step7.prepare(case, self.bundle, Path(temp))
            response = step7.core_call(paths, self.runner)
            self.assertEqual(0, response.code, response.stderr)
            inputs = dict(zip(("state", "candidate", "pulse", "context"),
                              (path.read_bytes() for path in paths), strict=True))
            record = self.mapper.parse_p1(response.stdout, inputs)
            packet = step7.load(paths[1])
            step7.verify_effects(record, packet, case)
            for change in (lambda row: row["links"]["diff"].update(id_sha256="0" * 64),
                           lambda row: row["core"].update(status="denied"),
                           lambda row: row["state"].update(changed=False),
                           lambda row: row["links"]["trace"].update(state_diff_ref_sha256=None)):
                altered = copy.deepcopy(record)
                change(altered)
                with self.subTest(altered=altered["state"]), self.assertRaises(step7.EvidenceError):
                    step7.verify_effects(altered, packet, case)

    def test_synthetic_valid_missing_and_tampered_receipts_are_not_proof(self) -> None:
        case = next(item for item in step7.load(step7.CASES)["cases"] if item["name"] == "tool-query")
        with tempfile.TemporaryDirectory(dir=self.root) as temp:
            paths = step7.prepare(case, self.bundle, Path(temp))
            response = step7.core_call(paths, self.runner)
            inputs = dict(zip(("state", "candidate", "pulse", "context"),
                              (path.read_bytes() for path in paths), strict=True))
            core = self.mapper.parse_p1(response.stdout, inputs)
            packet = step7.load(paths[1])
            # A supplied verdict without an actual runner/capture must never pass.
            forged = {"record_type": "AsterF1P2SolverReport", "version": "0.1", "verdict": "valid"}
            with self.assertRaises(step7.EvidenceError):
                step7.verify_solver(forged, [], core, response.stdout, packet, paths, case,
                                    self.mapper, self.runner)
            fake = self.runner.Captured(("check",), 0, b"Model is consistent\n", b"")
            forged = {"record_type": "AsterF1P2SolverReport", "version": "0.1",
                      "p1_aggregate": step7.P1_EXPECTED, "p1_evidence_sha256": step7.sha(response.stdout),
                      "p1_input_sha256": {name: step7.sha(raw) for name, raw in inputs.items()},
                      "core_status": "allowed", "core_issues": [], "core_disposition": "deferred",
                      "model_sha256": step7.sha(self.runner.MODEL.read_bytes()),
                      "executed_image": self.runner.IMAGE, "inspected_platform": self.runner.PLATFORM,
                      "declared_release": self.runner.RELEASE, "mutation_authorized": False,
                      "verdict": "valid", "solver_runs": [dict(fake.receipt(), stdout_sha256="0" * 64)]}
            with self.assertRaisesRegex(step7.EvidenceError, "solver_capture_tampered"):
                step7.verify_solver(forged, [fake], core, response.stdout, packet, paths, case,
                                    self.mapper, self.runner)

    def test_fallback_and_disagreement_are_blocking_comparison_controls(self) -> None:
        case = next(item for item in step7.load(step7.CASES)["cases"] if item["name"] == "tool-query")
        with tempfile.TemporaryDirectory(dir=self.root) as temp:
            paths = step7.prepare(case, self.bundle, Path(temp))
            report, real = self.runner.run(*paths)
            self.assertEqual("valid", report["verdict"], report)
            self.assertEqual(2, len(real))
            self.assertEqual(("fallback", "disagreed", "formal_core_disagreement"),
                             self.runner.compare("denied", ("tool_execute_denied",), real[0], real[1], True))
            ambiguous = self.runner.Captured(real[0].command, 1, b"Unrecognized solver error\n", b"")
            self.assertEqual(("fallback", "not_compared", "solver_result_ambiguous"),
                             self.runner.compare("allowed", (), ambiguous, None, False))


if __name__ == "__main__":
    unittest.main()
