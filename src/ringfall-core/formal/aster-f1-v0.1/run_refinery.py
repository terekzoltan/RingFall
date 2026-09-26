"""Local, offline Aster F1/0.1 evidence runner; never authorizes Core mutation."""

from __future__ import annotations

import argparse
import json
import re
import subprocess
import sys
import tempfile
import threading
import time
import uuid
from dataclasses import dataclass
from pathlib import Path
from typing import Any

from map_p1 import (
    CLASSES, RELATIONS, ClosedInstance, MappingFailure, P1_AGGREGATE, PREDICATE_ISSUES, PREDICATES,
    UnsupportedFacts, map_p1, parse_p1, sha, verify_p1_source,
)


IMAGE = "ghcr.io/graphs4value/refinery-cli@sha256:5d7eacdef0ddfb98e264cad405badf96753c68203e498a12d91331a0aa96ad57"
RELEASE = "0.3.0"  # Provenance label, not a locally installed tag alias.
PLATFORM = "linux/amd64"
MAX_OUTPUT = 65_536
BOOLEAN_SYMBOLS = frozenset(("toolAvailable", "crewAvailable", "requiresDryRun", "modeExecute",
                             "requiresDryRunFalse", "missingRequiredArgument", "fractionPresent"))
MODEL = Path(__file__).with_name("aster-f1-v0.1.problem")
ROOT = Path(__file__).resolve().parents[4]


class RuntimeFailure(RuntimeError):
    def __init__(self, code: str, *, retained_bytes: int = 0) -> None:
        super().__init__(code)
        self.retained_bytes = retained_bytes


@dataclass(frozen=True)
class Captured:
    command: tuple[str, ...]
    code: int
    stdout: bytes
    stderr: bytes

    def receipt(self) -> dict[str, Any]:
        return {
            "command": list(self.command), "exit_code": self.code,
            "stdout_sha256": sha(self.stdout), "stderr_sha256": sha(self.stderr),
            "stdout_bytes": len(self.stdout), "stderr_bytes": len(self.stderr),
        }


def cleanup_owned_container(name: str) -> None:
    """Best-effort removal and verified absence of one invocation-owned container."""
    if re.fullmatch(r"ringfall-p2-[0-9a-f]{32}", name) is None:
        raise RuntimeFailure("container_cleanup_unattributable")

    def bounded(args: list[str]) -> int:
        try:
            return subprocess.run(args, stdin=subprocess.DEVNULL, stdout=subprocess.DEVNULL,
                                  stderr=subprocess.DEVNULL, timeout=8, cwd=str(ROOT), check=False).returncode
        except (OSError, subprocess.TimeoutExpired) as exc:
            raise RuntimeFailure("container_cleanup_incomplete") from exc

    # An --rm container may have disappeared when its client was terminated.
    # rm returning 1 is harmless ONLY if inspect confirms absence and the daemon is alive.
    bounded(["docker", "rm", "--force", name])
    present = bounded(["docker", "inspect", "--type", "container", name])
    healthy = bounded(["docker", "version", "--format", "{{.Server.Version}}"])
    if present != 1 or healthy != 0:
        raise RuntimeFailure("container_cleanup_incomplete")


def capture(args: list[str], *, timeout: float = 90, owned_container: str | None = None) -> Captured:
    if owned_container is not None and re.fullmatch(r"ringfall-p2-[0-9a-f]{32}", owned_container) is None:
        raise RuntimeFailure("container_cleanup_unattributable")
    try:
        process = subprocess.Popen(args, stdout=subprocess.PIPE, stderr=subprocess.PIPE,
                                   stdin=subprocess.DEVNULL, bufsize=0, shell=False, cwd=str(ROOT))
    except OSError as exc:
        raise RuntimeFailure("process_unavailable_or_timed_out") from exc
    streams = (process.stdout, process.stderr)
    assert streams[0] is not None and streams[1] is not None
    buffers = [bytearray(), bytearray()]
    lock = threading.Lock()
    overflow = threading.Event()
    reader_failure = threading.Event()
    retained = 0

    def drain(index: int) -> None:
        nonlocal retained
        try:
            while True:
                chunk = streams[index].read(4096)
                if not chunk:
                    return
                with lock:
                    amount = min(len(chunk), MAX_OUTPUT - retained)
                    buffers[index].extend(chunk[:amount])
                    retained += amount
                    if amount != len(chunk):
                        overflow.set()
                        return
        except (OSError, ValueError):
            reader_failure.set()

    readers = [threading.Thread(target=drain, args=(index,), daemon=True) for index in (0, 1)]
    for reader in readers:
        reader.start()
    deadline = time.monotonic() + timeout
    timed_out = False
    while process.poll() is None and not overflow.is_set() and not reader_failure.is_set():
        if time.monotonic() >= deadline:
            timed_out = True
            break
        time.sleep(0.02)
    interrupted = timed_out or overflow.is_set() or reader_failure.is_set()
    incomplete = False
    if interrupted and process.poll() is None:
        try:
            process.terminate()
        except OSError:
            pass  # A failed terminate must still allow a force-kill of this child.
        else:
            try:
                process.wait(timeout=2)
            except (subprocess.TimeoutExpired, OSError):
                pass
        if process.poll() is None:
            try:
                process.kill()
            except OSError:
                pass  # Still wait and attempt this invocation's container cleanup.
            try:
                process.wait(timeout=2)
            except (subprocess.TimeoutExpired, OSError):
                pass
        if process.poll() is None:
            incomplete = True
    for reader in readers:
        reader.join(timeout=2)
        if reader.is_alive():
            incomplete = True
    if not incomplete:
        for stream in streams:
            stream.close()
    # A child that exits while producing output may overflow during final drain.
    interrupted = interrupted or overflow.is_set() or reader_failure.is_set()
    if interrupted or incomplete:
        if owned_container is not None:
            cleanup_owned_container(owned_container)
        if incomplete or reader_failure.is_set():
            raise RuntimeFailure("process_cleanup_incomplete", retained_bytes=retained)
        raise RuntimeFailure("process_unavailable_or_timed_out" if timed_out else "output_limit",
                             retained_bytes=retained)
    return Captured(tuple(args), process.returncode, bytes(buffers[0]), bytes(buffers[1]))


def inspect_digest() -> str:
    version = capture(["docker", "version", "--format", "{{.Server.Version}} {{.Server.Os}}/{{.Server.Arch}}"], timeout=15)
    if version.code != 0 or not re.fullmatch(rb"28\.0\.1 linux/amd64\s*", version.stdout):
        raise RuntimeFailure("docker_engine_mismatch")
    inspect = capture(["docker", "image", "inspect", IMAGE,
                       "--format", "{{.Os}}/{{.Architecture}} {{json .RepoDigests}}"], timeout=15)
    if inspect.code != 0:
        raise RuntimeFailure("digest_image_unavailable")
    try:
        platform, digests = inspect.stdout.decode("utf-8").strip().split(" ", 1)
        if platform != PLATFORM or IMAGE not in json.loads(digests):
            raise RuntimeFailure("digest_or_platform_mismatch")
    except (ValueError, UnicodeError, TypeError) as exc:
        raise RuntimeFailure("image_inspect_malformed") from exc
    return version.stdout.decode("utf-8").strip()


def docker_check(directory: Path, *subcommand: str) -> Captured:
    engine = inspect_digest()  # Recheck for each invocation; the tag is never inspected.
    if engine != "28.0.1 linux/amd64":
        raise RuntimeFailure("docker_engine_mismatch")
    name = "ringfall-p2-" + uuid.uuid4().hex
    mount = "type=bind,source=" + str(directory.resolve()) + ",target=/data,readonly"
    args = ["docker", "run", "--pull=never", "--platform", PLATFORM, "--rm", "--network", "none",
            "--cpus", "2", "--memory", "2g", "--pids-limit", "128", "--name", name, "--mount", mount,
            IMAGE, *subcommand]
    return capture(args, owned_container=name)


def check_errors(run: Captured) -> frozenset[str] | None:
    """Only positive identification of known 0.3.0 predicate errors is invalid.

    None means unknown/syntax/propagation error; exit 1 by itself is not proof.
    """
    if run.code != 1 or run.stderr:
        return None
    try:
        text = run.stdout.decode("utf-8")
    except UnicodeError:
        return None
    if not re.fullmatch(r"Inconsistencies found in model:\n\n(?:\t[A-Za-z]\w*\(r1\): error\.\n)+\n?", text):
        return None
    names = re.findall(r"^\t([A-Za-z]\w*)\(r1\): error\.$", text, re.MULTILINE)
    return frozenset(names) if names and len(names) == len(set(names)) and set(names) <= PREDICATES else None


def witness_is_closed(raw: bytes, instance: ClosedInstance) -> bool:
    """Parse actual 0.3.0 witness declarations, types and edges; extras fail."""
    try:
        text = raw.decode("utf-8")
    except UnicodeError:
        return False
    if not text.strip() or "unknown" in text.lower() or "?exists" in text:
        return False
    declared = re.findall(r"^declare ([\w, ]+)\.$", text, re.MULTILINE)
    if len(declared) != 1 or frozenset(part.strip() for part in declared[0].split(",")) != instance.expected_nodes:
        return False
    if any(f"!exists({name}::new)." not in text for name in CLASSES):
        return False
    if any(f"default !{name}(*, *)." not in text for name in RELATIONS):
        return False
    edges = frozenset((name, source, target) for name, source, target in re.findall(
        r"^([A-Za-z]\w*)\((\w+), (\w+)\)\.$", text, re.MULTILINE) if name in RELATIONS)
    if edges != instance.expected_edges:
        return False
    kinds = {"F1Actor": {"a1"}, "F1Request": {"r1"}, "F1Sector": {"s1"},
             "F1Tool": {"t1"} if instance.kind == "ToolActionRequest" else set(),
             "F1Action": {"ac1"} if instance.kind == "ToolActionRequest" else set(),
             "F1Crew": {"c1"} if instance.kind == "WorkOrderRequest" else set(),
             "F1Location": {"loc1"} if instance.kind == "WorkOrderRequest" else set()}
    for name, expected in kinds.items():
        found = set(re.findall(r"^" + name + r"\((\w+)\)\.$", text, re.MULTILINE))
        if found != expected:
            return False
    # Refinery concretizes Boolean attributes as full positive/negative lines.
    required = re.findall(r"^(\w+)\((\w+)\): (true|false)\.$", instance.text, re.MULTILINE)
    expected = {(name, node): truth == "true" for name, node, truth in required}
    if len(expected) != len(required) or any(name not in BOOLEAN_SYMBOLS for name, _, _ in required):
        return False
    assertions = [(name, node, not negative) for negative, name, node in re.findall(
        r"^(!?)([A-Za-z]\w*)\((\w+)\)\.$", text, re.MULTILINE) if name in BOOLEAN_SYMBOLS]
    if len(assertions) != len(expected):
        return False
    seen: set[tuple[str, str]] = set()
    for name, node, truth in assertions:
        key = (name, node)
        if key in seen or expected.get(key) is not truth:
            return False
        seen.add(key)
    if seen != expected.keys():
        return False
    expected_number = re.search(r"^fractionOrZero\(r1\): ([\d.-]+)\.$", instance.text, re.MULTILINE)
    actual_number = re.search(r"^fractionOrZero\(r1\): ([\d.-]+)\.$", text, re.MULTILINE)
    return expected_number is not None and actual_number is not None and expected_number.group(1) == actual_number.group(1)


def compare(core_status: str, core_issues: tuple[str, ...], check: Captured,
            generated: Captured | None, closed: bool) -> tuple[str, str, str | None]:
    """Compares an actual captured solver result to a separately produced Core decision."""
    if check.code == 0:
        if check.stdout != b"Model is consistent\n" or check.stderr:
            return "fallback", "not_compared", "solver_result_ambiguous"
        if generated is None or generated.code != 0 or not closed:
            return "fallback", "not_compared", "witness_not_closed"
        if core_status == "allowed" and not core_issues:
            return "valid", "agreed", None
        return "fallback", "disagreed", "formal_core_disagreement"
    matched = check_errors(check)
    if matched is None:
        return "fallback", "not_compared", "solver_result_ambiguous"
    actual_issues = tuple(sorted(PREDICATE_ISSUES[name] for name in matched))
    if actual_issues == tuple(sorted(core_issues)) and core_status == (
        "invalid" if "tool_argument_value_invalid" in actual_issues else "denied"
    ):
        return "invalid", "agreed", None
    return "fallback", "disagreed", "formal_core_disagreement"


def run(state: Path, candidate: Path, pulse: Path, context: Path) -> tuple[dict[str, Any], list[Captured]]:
    """Run one bounded, fresh P1 request and only then the pinned real solver."""
    result: dict[str, Any] = {
        "record_type": "AsterF1P2SolverReport", "version": "0.1",
        "p1_aggregate": P1_AGGREGATE, "p1_evidence_sha256": None,
        "p1_input_sha256": None, "model_family": "aster-l1-action-work-order",
        "model_version": "0.1", "model_sha256": None, "instance_sha256": None,
        "declared_release": RELEASE, "executed_image": IMAGE, "inspected_platform": PLATFORM,
        "engine": None, "solver_runs": [], "core_status": None, "core_issues": None,
        "core_disposition": None, "matched_errors": [], "verdict": "fallback",
        "agreement": "not_compared", "diagnostics": [], "mutation_authorized": False,
    }
    captures: list[Captured] = []
    try:
        verify_p1_source(ROOT)
        caller = Path.cwd().resolve()
        state, candidate, pulse, context = tuple(
            (path if path.is_absolute() else caller / path).resolve()
            for path in (state, candidate, pulse, context)
        )
        inputs = {name: path.read_bytes() for name, path in (("state", state), ("candidate", candidate),
                    ("pulse", pulse), ("context", context))}
        core = capture(["dotnet", "run", "--no-restore", "--project",
                        str(ROOT / "src/ringfall-core/Ringfall.Headless/Ringfall.Headless.csproj"), "--",
                        "aster-f1-evidence", "--state", str(state), "--candidate", str(candidate),
                        "--pulse", str(pulse), "--context", str(context)])
        if core.code != 0 or core.stderr.strip():
            raise MappingFailure("p1_call_failed")
        verify_p1_source(ROOT)
        record = parse_p1(core.stdout, inputs)
        result["p1_input_sha256"] = {name: sha(raw) for name, raw in inputs.items()}
        result["p1_evidence_sha256"] = sha(core.stdout)
        result["core_status"] = record["core"]["status"]
        result["core_issues"] = record["core"]["issues"]
        result["core_disposition"] = record["core"]["disposition"]
        model = MODEL.read_bytes()
        result["model_sha256"] = sha(model)
        instance = map_p1(record, model.decode("utf-8"))
        result["instance_sha256"] = sha(instance.text.encode("utf-8"))
        with tempfile.TemporaryDirectory(prefix="ringfall P2 ") as path:
            directory = Path(path)
            instance_path = directory / "instance.problem"
            instance_path.write_bytes(instance.text.encode("utf-8"))
            if sha(instance_path.read_bytes()) != result["instance_sha256"]:
                raise MappingFailure("instance_drift")
            result["engine"] = inspect_digest()
            check = docker_check(directory, "check", "-k", "instance.problem")
            captures.append(check)
            if sha(instance_path.read_bytes()) != result["instance_sha256"]:
                raise MappingFailure("instance_drift")
            generated = None
            closed = False
            if check.code == 0 and check.stdout == b"Model is consistent\n" and not check.stderr:
                generated = docker_check(directory, "generate", "-o", "-", "instance.problem")
                captures.append(generated)
                closed = generated.code == 0 and witness_is_closed(generated.stdout, instance)
                if sha(instance_path.read_bytes()) != result["instance_sha256"]:
                    raise MappingFailure("instance_drift")
            verdict, agreement, diagnostic = compare(instance.core_status, instance.core_issues,
                                                      check, generated, closed)
            result["matched_errors"] = sorted(check_errors(check) or ())
            result["verdict"] = verdict
            result["agreement"] = agreement
            if diagnostic:
                result["diagnostics"].append(diagnostic)
            verify_p1_source(ROOT)
    except UnsupportedFacts:
        result["verdict"] = "unsupported"
        result["diagnostics"].append("facts_or_core_issue_unmodeled")
    except (MappingFailure, RuntimeFailure, OSError, UnicodeError, ValueError) as exc:
        result["diagnostics"].append(str(exc) if isinstance(exc, (MappingFailure, RuntimeFailure)) else "local_evidence_failure")
    result["solver_runs"] = [item.receipt() for item in captures]
    return result, captures


def main() -> int:
    parser = argparse.ArgumentParser(description="Local digest-pinned Aster F1/0.1 evidence")
    for field in ("state", "candidate", "pulse", "context"):
        parser.add_argument("--" + field, required=True, type=Path)
    args = parser.parse_args()
    report, _ = run(args.state, args.candidate, args.pulse, args.context)
    print(json.dumps(report, sort_keys=True, separators=(",", ":")))
    return 0 if report["verdict"] in ("valid", "invalid", "unsupported") and not report["diagnostics"][:1] == ["formal_core_disagreement"] else 1


if __name__ == "__main__":
    sys.exit(main())
