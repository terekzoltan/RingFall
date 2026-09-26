"""Real local P1 + digest-pinned Refinery integration, with no simulated solver."""

from __future__ import annotations

import copy
import json
import os
import subprocess
import sys
import tempfile
import time
import unittest
from pathlib import Path
from unittest.mock import patch

from map_p1 import MappingFailure, UnsupportedFacts, P1_AGGREGATE, map_p1, parse_p1, sha, verify_p1_source
from run_refinery import (ROOT, MODEL, IMAGE, MAX_OUTPUT, Captured, RuntimeFailure, capture,
                          check_errors, cleanup_owned_container, compare, docker_check, run, witness_is_closed)


HERE = Path(__file__).parent
CASES = json.loads((HERE / "fixtures/cases.json").read_text(encoding="utf-8"))


def prepare(case: dict, directory: Path) -> tuple[Path, Path, Path, Path]:
    state = json.loads((ROOT / "src/ringfall-core/Ringfall.Core.Tests/Fixtures/aster-minimal-world-state.json").read_text(encoding="utf-8"))
    context = json.loads((ROOT / "src/ringfall-brain/examples/aster-a1-context.example.json").read_text(encoding="utf-8"))
    pulse = (ROOT / "src/ringfall-brain/examples/aster-a1-pulse.example.json")
    packet = copy.deepcopy(CASES["tool_request" if case["kind"] == "tool" else "work_request"])
    change = case["change"]
    if change in ("reroute", "no-dry-run", "missing-fraction", "fraction-zero", "fraction-negative", "fraction-high"):
        packet.update(action="dry_run_reroute", mode="dry_run", requires_dry_run=True,
                      arguments={"from_branch": "Aster-G3", "to_branch": "Aster-G4", "load_fraction": 0.20})
        if change == "no-dry-run":
            packet["requires_dry_run"] = False
        if change == "missing-fraction":
            del packet["arguments"]["load_fraction"]
        if change in ("fraction-zero", "fraction-negative", "fraction-high"):
            packet["arguments"]["load_fraction"] = {"fraction-zero": 0.0, "fraction-negative": -0.01, "fraction-high": 0.21}[change]
    elif change == "tool-unavailable":
        next(tool for tool in state["tools"] if tool["toolId"] == "local_grid_panel")["status"] = "unavailable"
    elif change == "crew-unavailable":
        next(crew for crew in state["crews"] if crew["crewId"] == "crew_aster_repair_02")["status"] = "unavailable"
    elif change == "execute":
        packet["mode"] = "execute"
    elif change == "tool-unreferenced":
        next(actor for actor in state["actors"] if actor["actorId"] == "A1")["toolRefs"].remove("local_grid_panel")
        # Core P1 independently compares this changed fixture with its projection.
        context["toolRefs"].remove("local_grid_panel")
    elif change == "unknown-argument":
        packet["arguments"] = {"unmodeled_input": "not_a_core_fact"}
    elif change != "none":
        raise AssertionError("Unknown bounded fixture recipe")
    state_path, packet_path, context_path = (directory / name for name in ("state.json", "candidate.json", "context.json"))
    state_path.write_text(json.dumps(state, separators=(",", ":")), encoding="utf-8")
    packet_path.write_text(json.dumps(packet, separators=(",", ":")), encoding="utf-8")
    context_path.write_text(json.dumps(context, separators=(",", ":")), encoding="utf-8")
    return state_path, packet_path, pulse, context_path


class AsterF1RealCliTests(unittest.TestCase):
    def test_accepted_source_identity(self) -> None:
        self.assertEqual(P1_AGGREGATE, verify_p1_source(ROOT))

    def test_real_smoke_one(self) -> None:
        with tempfile.TemporaryDirectory(prefix="ringfall P2 first witness ") as parent:
            report, raw = run(*prepare(CASES["cases"][0], Path(parent)))
            self.assertEqual("valid", report["verdict"], (report, [(r.code, r.stdout.decode(errors="replace"), r.stderr.decode(errors="replace")) for r in raw]))

    def test_real_witness_rejects_contradictory_duplicate_and_unexpected_booleans(self) -> None:
        with tempfile.TemporaryDirectory(prefix="ringfall P2 witness guards ") as parent:
            paths = prepare(CASES["cases"][0], Path(parent))
            report, raw = run(*paths)
            self.assertEqual("valid", report["verdict"], report)
            core = capture(["dotnet", "run", "--no-restore", "--project",
                            str(ROOT / "src/ringfall-core/Ringfall.Headless/Ringfall.Headless.csproj"), "--",
                            "aster-f1-evidence", "--state", str(paths[0]), "--candidate", str(paths[1]),
                            "--pulse", str(paths[2]), "--context", str(paths[3])])
            self.assertEqual(0, core.code, core.stderr)
            inputs = dict(zip(("state", "candidate", "pulse", "context"), (path.read_bytes() for path in paths)))
            instance = map_p1(parse_p1(core.stdout, inputs), MODEL.read_text(encoding="utf-8"))
            self.assertEqual(sha(instance.text.encode("utf-8")), report["instance_sha256"])
            real = raw[1].stdout
            self.assertTrue(witness_is_closed(real, instance))
            self.assertEqual(1, real.count(b"toolAvailable(t1).\n"))
            self.assertEqual(1, real.count(b"!requiresDryRun(ac1).\n"))
            altered = (
                real.replace(b"toolAvailable(t1).\n", b"!toolAvailable(t1).\n", 1),
                real.replace(b"!requiresDryRun(ac1).\n", b"requiresDryRun(ac1).\n", 1),
                real + b"!toolAvailable(t1).\n",
                real + b"toolAvailable(t1).\n",  # same-polarity duplicate
                real + b"modeExecute(a1).\n",  # modeled symbol on an unexpected node
            )
            for content in altered:
                with self.subTest(suffix=content[-48:]):
                    self.assertFalse(witness_is_closed(content, instance))

    def test_documented_relative_paths_reach_real_core_and_solver(self) -> None:
        with tempfile.TemporaryDirectory(prefix="ringfall P2 relative ") as parent:
            paths = prepare(CASES["cases"][0], Path(parent))
            relative = [os.path.relpath(path, start=HERE) for path in paths]
            self.assertTrue(all(not Path(value).is_absolute() for value in relative))
            args = [sys.executable, "-B", str(HERE / "run_refinery.py")]
            for flag, path in zip(("state", "candidate", "pulse", "context"), relative):
                args.extend(("--" + flag, path))
            completed = subprocess.run(args, cwd=str(HERE), capture_output=True, timeout=120, check=False)
            self.assertEqual(0, completed.returncode, completed.stderr)
            report = json.loads(completed.stdout)
            self.assertEqual("valid", report["verdict"], report)
            self.assertEqual([0, 0], [item["exit_code"] for item in report["solver_runs"]])
            for name, source in zip(("state", "candidate", "pulse", "context"), paths):
                self.assertEqual(sha(source.read_bytes()), report["p1_input_sha256"][name])

    def test_real_error_one(self) -> None:
        with tempfile.TemporaryDirectory(prefix="ringfall P2 first error ") as parent:
            report, raw = run(*prepare(CASES["cases"][3], Path(parent)))
            self.assertEqual("invalid", report["verdict"], (report, [(r.code, r.stdout.decode(errors="replace"), r.stderr.decode(errors="replace")) for r in raw]))

    def test_real_local_matrix(self) -> None:
        for case in CASES["cases"]:
            with self.subTest(case=case["name"]), tempfile.TemporaryDirectory(prefix="ringfall P2 fixture ") as parent:
                report, raw = run(*prepare(case, Path(parent)))
                self.assertEqual(case["verdict"], report["verdict"], (case["name"], report, [(r.code, r.stdout.decode(errors="replace"), r.stderr.decode(errors="replace")) for r in raw]))
                self.assertEqual(case["issue"], next(iter(report["core_issues"] or []), None), case["name"])
                if case["verdict"] == "valid":
                    self.assertEqual("agreed", report["agreement"])
                    self.assertEqual([0, 0], [result.code for result in raw])
                elif case["verdict"] == "invalid":
                    self.assertEqual("agreed", report["agreement"])
                    self.assertEqual(1, len(raw))
                    self.assertEqual(1, raw[0].code)
                    self.assertTrue(report["matched_errors"])
                else:
                    self.assertEqual("not_compared", report["agreement"])
                    self.assertEqual([], raw)
                for item, receipt in zip(raw, report["solver_runs"]):
                    self.assertEqual(IMAGE, item.command[-4] if item.command[-3:] == ("check", "-k", "instance.problem") else item.command[-5])
                    self.assertIn("--pull=never", item.command)
                    self.assertIn("none", item.command)
                    name = item.command[item.command.index("--name") + 1]
                    self.assertRegex(name, r"^ringfall-p2-[0-9a-f]{32}$")
                    mount = item.command[item.command.index("--mount") + 1]
                    self.assertIn("ringfall P2 ", mount)
                    self.assertTrue(mount.endswith(",target=/data,readonly"))
                    self.assertEqual(sha(item.stdout), receipt["stdout_sha256"])
                    self.assertEqual(sha(item.stderr), receipt["stderr_sha256"])
                self.assertEqual(len(raw), len({item.command[item.command.index("--name") + 1] for item in raw}))
                self.assertEqual("0.3.0", report["declared_release"])
                self.assertIn("@sha256:5d7eacdef", report["executed_image"])
                self.assertFalse(report["mutation_authorized"])
                self.assertNotIn("thermalDebt", json.dumps(report))

    def test_malformed_model_exit_one_is_not_domain_invalid(self) -> None:
        with tempfile.TemporaryDirectory(prefix="ringfall P2 syntax control ") as parent:
            directory = Path(parent)
            (directory / "instance.problem").write_text("class F1Broken { invalid syntax", encoding="utf-8")
            bad = docker_check(directory, "check", "-k", "instance.problem")
            self.assertEqual(1, bad.code)
            self.assertIsNone(check_errors(bad))
            self.assertEqual(("fallback", "not_compared", "solver_result_ambiguous"),
                             compare("allowed", (), bad, None, False))

    def test_captured_solver_disagreement_is_not_a_domain_claim(self) -> None:
        # Comparison-only test: an actual solver control is compared with a deliberately
        # different test-only Core status. This is not a real Core/domain disagreement.
        case = CASES["cases"][0]
        with tempfile.TemporaryDirectory(prefix="ringfall P2 comparison control ") as parent:
            report, actual = run(*prepare(case, Path(parent)))
            self.assertEqual("valid", report["verdict"], report)
            self.assertEqual(("fallback", "disagreed", "formal_core_disagreement"),
                             compare("denied", ("tool_execute_denied",), actual[0], actual[1], True))
            # Parser-only unexpected output control; never represented as a solver run.
            ambiguous = Captured(("check",), 0, b"unrecognized-success\n", b"")
            self.assertEqual(("fallback", "not_compared", "solver_result_ambiguous"),
                             compare("allowed", (), ambiguous, actual[1], True))

    def test_p1_evidence_drift_is_rejected(self) -> None:
        inputs = {name: b"{}" for name in ("state", "candidate", "pulse", "context")}
        with self.assertRaises(MappingFailure):
            parse_p1(b'{"record_type":"AsterF1P1Evidence","record_type":"AsterF1P1Evidence"}', inputs)

    def test_capture_limits_both_live_pipes_and_terminates_owned_child(self) -> None:
        short = capture([sys.executable, "-u", "-c",
                         'import sys;sys.stdout.buffer.write(b"a"*2048);sys.stderr.buffer.write(b"b"*2048)'])
        self.assertEqual(b"a" * 2048, short.stdout)
        self.assertEqual(b"b" * 2048, short.stderr)
        self.assertEqual(0, short.code)
        original_popen = subprocess.Popen
        children = []

        def remember(*args: object, **kwargs: object) -> subprocess.Popen:
            child = original_popen(*args, **kwargs)
            children.append(child)
            return child

        flooding = ('import sys,time\n'
                    'for _ in range(128):\n'
                    ' sys.stdout.buffer.write(b"o"*1024);sys.stdout.flush()\n'
                    ' sys.stderr.buffer.write(b"e"*1024);sys.stderr.flush()\n'
                    'time.sleep(30)\n')
        with patch("run_refinery.subprocess.Popen", side_effect=remember):
            start = time.monotonic()
            with self.assertRaises(RuntimeFailure) as overflow:
                capture([sys.executable, "-u", "-c", flooding], timeout=5)
        self.assertEqual("output_limit", str(overflow.exception))
        self.assertLessEqual(overflow.exception.retained_bytes, MAX_OUTPUT)
        self.assertLess(time.monotonic() - start, 5)
        self.assertIsNotNone(children[-1].poll())

        with patch("run_refinery.subprocess.Popen", side_effect=remember):
            with self.assertRaises(RuntimeFailure) as timed_out:
                capture([sys.executable, "-u", "-c",
                         'import sys,time;sys.stdout.write("ready\\n");sys.stdout.flush();time.sleep(30)'],
                        timeout=0.3)
        self.assertEqual("process_unavailable_or_timed_out", str(timed_out.exception))
        self.assertLessEqual(timed_out.exception.retained_bytes, MAX_OUTPUT)
        self.assertIsNotNone(children[-1].poll())

    def test_termination_errors_still_settle_child_and_attempt_owned_cleanup(self) -> None:
        # These are subprocess failure controls, not substitute Refinery evidence.
        original_popen = subprocess.Popen
        for failure, cleanup_fails in (("terminate", False), ("kill", False), ("kill", True)):
            with self.subTest(failure=failure, cleanup_fails=cleanup_fails):
                name = "ringfall-p2-" + ("a" if failure == "terminate" else "b") * 32
                commands = []
                children = []
                calls = []

                class FaultyChild:
                    def __init__(self, child: subprocess.Popen) -> None:
                        self.child = child
                        self.stdout = child.stdout
                        self.stderr = child.stderr

                    def poll(self) -> int | None:
                        return self.child.poll()

                    def terminate(self) -> None:
                        calls.append("terminate")
                        if failure == "terminate":
                            raise OSError("injected terminate error")
                        # Leave the child live to exercise the bounded escalation.

                    def kill(self) -> None:
                        calls.append("kill")
                        if failure == "kill":
                            raise OSError("injected kill error")
                        self.child.kill()

                    def wait(self, timeout: float) -> int:
                        calls.append("wait")
                        if failure == "kill":
                            raise subprocess.TimeoutExpired(self.child.args, timeout)
                        return self.child.wait(timeout=timeout)

                def spawn(*args: object, **kwargs: object) -> FaultyChild:
                    child = original_popen(*args, **kwargs)
                    children.append(child)
                    return FaultyChild(child)

                def docker_cleanup(argv: list[str], **kwargs: object) -> subprocess.CompletedProcess:
                    commands.append((tuple(argv), kwargs["timeout"]))
                    return subprocess.CompletedProcess(argv, 1 if argv[1] == "inspect" else 0)

                try:
                    with patch("run_refinery.subprocess.Popen", side_effect=spawn), \
                         patch("run_refinery.subprocess.run", side_effect=docker_cleanup):
                        if cleanup_fails:
                            with patch("run_refinery.cleanup_owned_container",
                                       side_effect=RuntimeFailure("container_cleanup_incomplete")) as cleanup:
                                with self.assertRaises(RuntimeFailure) as error:
                                    capture([sys.executable, "-u", "-c", "import time;time.sleep(30)"],
                                            timeout=0.1, owned_container=name)
                            cleanup.assert_called_once_with(name)
                        else:
                            with self.assertRaises(RuntimeFailure) as error:
                                capture([sys.executable, "-u", "-c", "import time;time.sleep(30)"],
                                        timeout=0.1, owned_container=name)
                    self.assertEqual("container_cleanup_incomplete" if cleanup_fails else
                                     "process_cleanup_incomplete" if failure == "kill" else
                                     "process_unavailable_or_timed_out", str(error.exception))
                    self.assertEqual(1, calls.count("kill"))
                    if failure == "terminate":
                        self.assertIsNotNone(children[0].poll())
                    else:
                        self.assertGreaterEqual(calls.count("wait"), 2)
                    if not cleanup_fails:
                        self.assertEqual([("docker", "rm", "--force", name),
                                          ("docker", "inspect", "--type", "container", name),
                                          ("docker", "version", "--format", "{{.Server.Version}}")],
                                         [argv for argv, _ in commands])
                        self.assertTrue(all(bound <= 8 for _, bound in commands))
                finally:
                    # Even the injected kill-error case must not leave a test child running.
                    for child in children:
                        if child.poll() is None:
                            child.kill()
                        child.wait(timeout=5)
                        for stream in (child.stdout, child.stderr):
                            if stream is not None:
                                stream.close()

    def test_docker_interruption_cleanup_is_owned_bounded_and_fail_closed(self) -> None:
        name = "ringfall-p2-" + "a" * 32
        commands = []

        def successful_cleanup(argv: list[str], **kwargs: object) -> subprocess.CompletedProcess:
            commands.append((tuple(argv), kwargs))
            return subprocess.CompletedProcess(argv, 1 if argv[1] == "inspect" else 0)

        # Capture/cleanup boundary control only: no fake solver outcome.
        with patch("run_refinery.subprocess.run", side_effect=successful_cleanup):
            cleanup_owned_container(name)
        self.assertEqual(("docker", "rm", "--force", name), commands[0][0])
        self.assertEqual(("docker", "inspect", "--type", "container", name), commands[1][0])
        self.assertTrue(all(argv[0] == "docker" and kwargs["timeout"] <= 8 for argv, kwargs in commands))
        self.assertFalse(any("--force" in argv and argv[-1] != name for argv, _ in commands))
        commands.clear()
        with patch("run_refinery.subprocess.run", side_effect=successful_cleanup):
            with self.assertRaises(RuntimeFailure) as bad_name:
                cleanup_owned_container("not-an-owned-container")
        self.assertEqual("container_cleanup_unattributable", str(bad_name.exception))
        self.assertEqual([], commands)

        def incomplete_cleanup(argv: list[str], **kwargs: object) -> subprocess.CompletedProcess:
            commands.append((tuple(argv), kwargs))
            return subprocess.CompletedProcess(argv, 0)

        with patch("run_refinery.subprocess.run", side_effect=incomplete_cleanup):
            with self.assertRaises(RuntimeFailure) as incomplete:
                cleanup_owned_container(name)
        self.assertEqual("container_cleanup_incomplete", str(incomplete.exception))
        self.assertEqual(name, commands[0][0][-1])

        with patch("run_refinery.inspect_digest", return_value="28.0.1 linux/amd64"), \
             patch("run_refinery.capture", side_effect=RuntimeFailure("output_limit")) as interrupted:
            with self.assertRaises(RuntimeFailure):
                docker_check(Path(tempfile.gettempdir()), "check", "-k", "instance.problem")
            argv = interrupted.call_args.args[0]
            self.assertEqual(argv[argv.index("--name") + 1], interrupted.call_args.kwargs["owned_container"])
            self.assertRegex(interrupted.call_args.kwargs["owned_container"], r"^ringfall-p2-[0-9a-f]{32}$")

        with tempfile.TemporaryDirectory(prefix="ringfall P2 cleanup result ") as parent:
            paths = prepare(CASES["cases"][0], Path(parent))
            with patch("run_refinery.inspect_digest", return_value="28.0.1 linux/amd64"), \
                 patch("run_refinery.docker_check", side_effect=RuntimeFailure("container_cleanup_incomplete")):
                report, runs = run(*paths)
            self.assertEqual("fallback", report["verdict"])
            self.assertIn("container_cleanup_incomplete", report["diagnostics"])
            self.assertEqual([], runs)

    def test_deterministic_mapper_uses_only_bound_core_facts(self) -> None:
        case = CASES["cases"][0]
        with tempfile.TemporaryDirectory(prefix="ringfall P2 mapper only ") as parent:
            state, candidate, pulse, context = prepare(case, Path(parent))
            packet = json.loads(candidate.read_text(encoding="utf-8"))
            packet["rationale"] = "thermalDebt hidden-marker that must not enter the model"
            candidate.write_text(json.dumps(packet), encoding="utf-8")
            captured = capture(["dotnet", "run", "--no-restore", "--project",
                                str(ROOT / "src/ringfall-core/Ringfall.Headless/Ringfall.Headless.csproj"), "--",
                                "aster-f1-evidence", "--state", str(state), "--candidate", str(candidate),
                                "--pulse", str(pulse), "--context", str(context)])
            self.assertEqual(0, captured.code, captured.stderr)
            inputs = {name: path.read_bytes() for name, path in (("state", state), ("candidate", candidate),
                      ("pulse", pulse), ("context", context))}
            record = parse_p1(captured.stdout, inputs)
            template = MODEL.read_text(encoding="utf-8")
            first = map_p1(record, template)
            self.assertEqual(first.text, map_p1(record, template).text)
            self.assertEqual(5, len(first.expected_nodes))
            self.assertNotIn("thermalDebt", first.text)
            self.assertNotIn("hidden-marker", first.text)
            altered = copy.deepcopy(record)
            altered["core"]["status"] = "denied"
            altered["core"]["issues"] = ["issuer_layer_denied"]
            with self.assertRaises(UnsupportedFacts):
                map_p1(altered, template)


if __name__ == "__main__":
    unittest.main()
