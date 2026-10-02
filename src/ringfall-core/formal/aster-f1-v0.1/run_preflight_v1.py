"""Successor pre-execution F1 gate; never invokes P1's execution-first bridge."""

from __future__ import annotations

import argparse
import json
import math
import sys
from pathlib import Path
from typing import Any

from map_p1 import MappingFailure, UnsupportedFacts, map_p1, sha
from run_refinery import IMAGE, MODEL, ROOT

HERE = Path(__file__).resolve().parent
MANIFEST = HERE / "successor-preflight-v1.json"
INPUT_NAMES = ("state", "candidate", "pulse", "context")
FIELDS = frozenset(("record_type", "contract_version", "family_id", "family_version",
                    "input_versions", "inputs", "initial_state_sha256", "binding", "core",
                    "facts", "completeness", "unsupported_predicates", "execution", "schema_validation"))
P1_PATHS = ("Ringfall.Core/Formal/AsterF1FactExporter.cs", "Ringfall.Core/Formal/AsterF1P1Facts.cs",
            "Ringfall.Core/Formal/AsterF1P1Input.cs", "Ringfall.Core/Formal/AsterF1P1EvidenceBridge.cs",
            "Ringfall.Core/AssemblyInfo.cs", "Ringfall.Core/CoreInfo.cs", "Ringfall.Headless/Program.cs",
            "Ringfall.Core.Tests/HeadlessCliTests.cs", "Ringfall.Core.Tests/AsterF1P1EvidenceTests.cs")
P2_PATHS = ("README.md", "aster-f1-v0.1.problem", "fixtures/cases.json", "map_p1.py",
            "run_refinery.py", "test_aster_f1.py")
EXTRA_PATHS = ("src/ringfall-core/Ringfall.Core/Formal/AsterF1GatedExecution.cs",
               "src/ringfall-core/Ringfall.Core/Formal/AsterF1GatedProof.cs",
               "src/ringfall-core/Ringfall.Headless/AsterF1GatedCommand.cs",
               "src/ringfall-core/Ringfall.Headless/AsterF1TrustedDocker.cs",
               "src/ringfall-core/Ringfall.Core.Tests/AsterF1GatedExecutionTests.cs",
               "src/ringfall-core/formal/aster-f1-v0.1/run_preflight_v1.py",
               "src/ringfall-core/formal/aster-f1-v0.1/test_preflight_v1.py",
               "src/ringfall-core/formal/aster-f1-v0.1/successor-preflight-v1.md")
SOURCES = tuple("src/ringfall-core/" + path for path in P1_PATHS) + tuple(
    "src/ringfall-core/formal/aster-f1-v0.1/" + path for path in P2_PATHS) + EXTRA_PATHS


def fail(code: str) -> None:
    raise MappingFailure(code)


def unique(pairs: list[tuple[str, Any]]) -> dict[str, Any]:
    result: dict[str, Any] = {}
    for key, value in pairs:
        if key in result:
            fail("duplicate_json_key")
        result[key] = value
    return result


def strict(raw: bytes) -> dict[str, Any]:
    if not raw or len(raw) > 1_048_576:
        fail("record_size")
    try:
        value = json.loads(raw.decode("utf-8"), object_pairs_hook=unique,
                           parse_constant=lambda _: fail("nonfinite_json"))
    except (UnicodeError, ValueError, RecursionError) as exc:
        raise MappingFailure("malformed_json") from exc
    if not isinstance(value, dict):
        fail("record_shape")
    def finite(item: Any) -> None:
        if type(item) is float and not math.isfinite(item):
            fail("nonfinite_json")
        if isinstance(item, dict):
            for child in item.values():
                finite(child)
        elif isinstance(item, list):
            for child in item:
                finite(child)
    finite(value)
    return value


def verify_sources() -> str:
    manifest = strict(MANIFEST.read_bytes())
    if set(manifest) != {"record_type", "version", "historical_p1_aggregate", "historical_p2_aggregate",
                         "image", "model_sha256", "source_files", "source_aggregate"}:
        fail("successor_manifest_shape")
    if (manifest["record_type"], manifest["version"], manifest["historical_p1_aggregate"],
        manifest["historical_p2_aggregate"], manifest["image"]) != (
        "AsterF1PreflightSource", "1.0", "6bf530a8e1efda55703de8563b0435f1487baaa0fe8b0697c759aefca8f85fbc",
        "652f8abfe1bb140af7dd76ad9660024c964d6e77dfae879207b6fd690815dd45", IMAGE):
        fail("successor_manifest_identity")
    files = manifest["source_files"]
    if type(files) is not dict or tuple(files) != SOURCES or manifest["model_sha256"] != sha(MODEL.read_bytes()):
        fail("successor_manifest_paths_or_model")
    lines = []
    for relative, expected in files.items():
        actual = sha((ROOT / relative).read_bytes())
        if type(expected) is not str or actual != expected:
            fail("successor_source_drift")
        lines.append(relative + "=" + expected)
    aggregate = sha("\n".join(lines).encode("utf-8"))
    if aggregate != manifest["source_aggregate"]:
        fail("successor_aggregate_mismatch")
    return aggregate


def parse_preflight(raw: bytes, inputs: dict[str, bytes]) -> dict[str, Any]:
    record = strict(raw)
    try:
        if set(record) != FIELDS or (record["record_type"], record["contract_version"],
            record["family_id"], record["family_version"], record["execution"],
            record["schema_validation"]) != (
            "AsterF1Preflight", "1.0", "aster-l1-action-work-order", "0.1", "not_run", "not_run"):
            fail("preflight_version_or_shape")
        if record["input_versions"] != {"state": "0.1", "candidate": "0.1", "pulse": "0.1",
                                         "context_format": "actor_context_projection"}:
            fail("input_version_drift")
        binding = record["binding"]
        if set(binding) != {"status", "evidence_ref_count"} or binding["status"] != "matched_projection_and_pulse" or type(binding["evidence_ref_count"]) is not int or binding["evidence_ref_count"] < 1:
            fail("preflight_binding_invalid")
        ids = record["inputs"]
        if set(ids) != {name + "_input_sha256" for name in INPUT_NAMES} | {
            "packet_id", "packet_id_sha256", "source_context_id", "source_context_id_sha256"}:
            fail("preflight_input_shape")
        if any(ids[name + "_input_sha256"] != sha(inputs[name]) for name in INPUT_NAMES):
            fail("preflight_input_drift")
        packet = strict(inputs["candidate"])
        pulse = strict(inputs["pulse"])
        context = strict(inputs["context"])
        requests = pulse["requested_packets"]
        if (type(ids["packet_id"]) is not str or not ids["packet_id"]
            or type(ids["source_context_id"]) is not str or not ids["source_context_id"]
            or packet["packet_id"] != ids["packet_id"]
            or packet["source_context_id"] != ids["source_context_id"]
            or pulse["source_context_id"] != ids["source_context_id"]
            or type(requests) is not list or sum(type(row) is dict and row.get("draft_ref") == ids["packet_id"]
                       and row.get("packet_type") == packet["packet_type"] for row in requests) != 1
            or context["actorId"] != packet["issuer_id"]
            or ids["packet_id_sha256"] != sha(ids["packet_id"].encode("utf-8"))
            or ids["source_context_id_sha256"] != sha(ids["source_context_id"].encode("utf-8"))):
            fail("preflight_identity_drift")
        if type(record["initial_state_sha256"]) is not str or len(record["initial_state_sha256"]) != 64:
            fail("preflight_state_shape")
        core = record["core"]
        if set(core) != {"status", "issues"} or type(core["issues"]) is not list or any(type(x) is not str for x in core["issues"]):
            fail("preflight_core_shape")
        if (record["completeness"] not in ("complete", "unsupported")
            or type(record["unsupported_predicates"]) is not list
            or any(type(x) is not str for x in record["unsupported_predicates"])
            or (record["completeness"] == "complete") != (not record["unsupported_predicates"] and core["status"] != "unsupported")):
            # Unknown Core issues can also make a record unsupported with no fact predicate.
            if record["completeness"] != "unsupported":
                fail("preflight_completeness")
        if set(record["facts"]) != {"packet_kind", "issuer_id", "issuer_layer", "issuer_home_sector_id",
                                    "tool", "work_order", "tool_arguments"}:
            fail("preflight_fact_shape")
        if record["facts"]["packet_kind"] != packet["packet_type"]:
            fail("preflight_kind_drift")
        facts = record["facts"]
        if facts["packet_kind"] == "ToolActionRequest":
            if facts["work_order"] is not None or type(facts["tool"]) is not dict or set(facts["tool"]) != {
                "tool_id", "status", "system_refs", "supported_actions", "actor_tool_referenced",
                "action", "mode", "arguments_presence", "requires_dry_run"}:
                fail("preflight_tool_shape")
        elif facts["packet_kind"] == "WorkOrderRequest":
            if facts["tool"] is not None or type(facts["work_order"]) is not dict or set(facts["work_order"]) != {
                "crew_id", "crew_status", "assigned_actor_id", "crew_home_sector_id", "crew_system_refs",
                "actor_crew_referenced", "target_crew_id", "target_crew_pool_id", "target_location",
                "task_type", "options"}:
                fail("preflight_work_shape")
        else:
            fail("preflight_kind_invalid")
        fields = facts["tool_arguments"] + ([facts["tool"]["requires_dry_run"]] if facts["tool"] is not None else [
            facts["work_order"]["target_crew_id"], facts["work_order"]["target_crew_pool_id"],
            *facts["work_order"]["options"]])
        if any(type(row) is not dict or set(row) != {"name", "presence", "value", "coverage"} for row in fields):
            fail("preflight_field_shape")
    except (KeyError, TypeError, AttributeError, IndexError) as exc:
        raise MappingFailure("preflight_record_shape") from exc
    return record


def run(raw: bytes, inputs: dict[str, bytes], instance_path: Path) -> dict[str, Any]:
    result: dict[str, Any] = {
        "record_type": "AsterF1PreflightMapping", "version": "1.0",
        "preflight_sha256": sha(raw), "input_sha256": {name: sha(inputs[name]) for name in INPUT_NAMES},
        "packet_id_sha256": None, "source_context_id_sha256": None,
        "source_aggregate": None, "model_sha256": None, "instance_sha256": None,
        "diagnostics": [], "schema_validation": "not_run", "mutation_authorized": False,
    }
    try:
        result["source_aggregate"] = verify_sources()
        record = parse_preflight(raw, inputs)
        result["packet_id_sha256"] = record["inputs"]["packet_id_sha256"]
        result["source_context_id_sha256"] = record["inputs"]["source_context_id_sha256"]
        # This is an ephemeral mapper view, not a P1 evidence/execution record.
        view = {"facts": record["facts"], "core": {**record["core"], "disposition": "not_executed"},
                "completeness": record["completeness"], "unsupported_predicates": record["unsupported_predicates"]}
        model = MODEL.read_bytes()
        result["model_sha256"] = sha(model)
        instance = map_p1(view, model.decode("utf-8"))
        result["instance_sha256"] = sha(instance.text.encode("utf-8"))
        instance_path.write_bytes(instance.text.encode("utf-8"))
        if sha(instance_path.read_bytes()) != result["instance_sha256"]:
            fail("instance_drift")
        if result["source_aggregate"] != verify_sources():
            fail("successor_source_drift")
    except (UnsupportedFacts, MappingFailure, OSError, UnicodeError, ValueError) as exc:
        result["diagnostics"].append("facts_or_core_issue_unmodeled" if isinstance(exc, UnsupportedFacts)
                                     else str(exc) if isinstance(exc, MappingFailure) else "local_gate_failure")
    return result


def main() -> int:
    parser = argparse.ArgumentParser(description="Bound pre-execution Aster F1 instance mapping only")
    parser.add_argument("--preflight", required=True, type=Path)
    for name in INPUT_NAMES:
        parser.add_argument("--" + name, required=True, type=Path)
    parser.add_argument("--instance", required=True, type=Path)
    args = parser.parse_args()
    # Headless selects the new private instance path; Python never starts Docker.
    if args.instance.exists() or not args.instance.parent.is_dir():
        fail("instance_path_invalid")
    inputs = {name: getattr(args, name).read_bytes() for name in INPUT_NAMES}
    result = run(args.preflight.read_bytes(), inputs, args.instance)
    print(json.dumps(result, sort_keys=True, separators=(",", ":")))
    return 0 if not result["diagnostics"] else 1


if __name__ == "__main__":
    sys.exit(main())
