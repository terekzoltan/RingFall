"""Strict, fixed-symbol translation of accepted Core P1 evidence into Aster F1/0.1.

This module never decodes a WorldState or makes an authority decision. Core owns
those decisions; unknown or unrepresented facts are deliberately not modeled.
"""

from __future__ import annotations

import hashlib
import json
import math
from dataclasses import dataclass
from decimal import Decimal
from pathlib import Path
from typing import Any


P1_AGGREGATE = "6bf530a8e1efda55703de8563b0435f1487baaa0fe8b0697c759aefca8f85fbc"
P1_PATHS = (
    ("Ringfall.Core/Formal/AsterF1FactExporter.cs", "2d2f9dcbb48b1def2227600c204b3c4898471d2a424cb182d2c2b539480a64cb"),
    ("Ringfall.Core/Formal/AsterF1P1Facts.cs", "77b3077b06732da71941ab85a1309e41069e590527019e526a60d91d55d714fc"),
    ("Ringfall.Core/Formal/AsterF1P1Input.cs", "00b61accc8445a5f0617985b6ed64bd3c07a4225898d417cf808ec2e5a7d3e80"),
    ("Ringfall.Core/Formal/AsterF1P1EvidenceBridge.cs", "bad43a4c5feedec1135f550a99259bac1d09ba58a8e4d9b0d6840b276e87bb08"),
    ("Ringfall.Core/AssemblyInfo.cs", "fae40a3836f95e995127bd4ce53421fb6c964a319bcd92dc8fda2ae19205b2d4"),
    ("Ringfall.Core/CoreInfo.cs", "cf194ed0ecb9920c1aa40613e7d6616f4f2501ac64bc0cc40adeb4ba3d544ceb"),
    ("Ringfall.Headless/Program.cs", "3da5ea954e1a0f015af0bdc07906f5f3801a5e8d5a2616163d581953ee6358ac"),
    ("Ringfall.Core.Tests/HeadlessCliTests.cs", "b39af2a86a3e66fc3c580601ea905a58fdccf08848c82aa82f83f5994553def9"),
    ("Ringfall.Core.Tests/AsterF1P1EvidenceTests.cs", "b9acf2511162d605c0d36007f5ec9a7b341dc3a137d881b8026a61b1008f3c97"),
)

ISSUE_PREDICATES = {
    "tool_unavailable": ("toolUnavailable", "denied"),
    "crew_unavailable": ("crewUnavailable", "denied"),
    "tool_execute_denied": ("toolExecuteDenied", "denied"),
    "tool_requires_dry_run_conflict": ("requiresDryRunConflict", "denied"),
    "tool_arguments_missing": ("toolArgumentsMissing", "denied"),
    "tool_argument_value_invalid": ("loadFractionNonPositive", "invalid"),
    "tool_macro_surface_denied": ("loadFractionTooHigh", "denied"),
}
PREDICATE_ISSUES = {predicate: issue for issue, (predicate, _) in ISSUE_PREDICATES.items()}
PREDICATES = frozenset(PREDICATE_ISSUES)
ARGUMENT_NAMES = ("branch_id", "from_branch", "to_branch", "asset_id", "load_fraction")
RULES = {
    "query_heat_alarm": ("local_grid_panel", ()),
    "query_branch_load": ("local_grid_panel", ("branch_id",)),
    "dry_run_reroute": ("local_grid_panel", ("from_branch", "to_branch", "load_fraction")),
    "query_asset_status": ("maintenance_console", ("asset_id",)),
    "query_backlog": ("maintenance_console", ()),
    "dry_run_patch": ("maintenance_console", ("asset_id",)),
}
STRING_ARGUMENTS = {
    "branch_id": ("Aster-G3", "Aster-G4"),
    "from_branch": ("Aster-G3",),
    "to_branch": ("Aster-G4",),
    "asset_id": ("Aster/Grid-Spine-03/Coupler-7",),
}
RELATIONS = ("toolRef", "crewRef", "actorHomeSector", "crewHomeSector", "supports", "assignedActor", "issuer", "targetTool", "targetCrew", "action", "targetLocation")
CLASSES = ("F1Actor", "F1Tool", "F1Crew", "F1Sector", "F1Action", "F1Location", "F1Request")


class MappingFailure(ValueError):
    """Malformed evidence or drift; never treat it as formal invalidity."""


class UnsupportedFacts(ValueError):
    """Known but unmodeled Core decision or P1 fact surface."""


@dataclass(frozen=True)
class ClosedInstance:
    text: str
    kind: str
    expected_nodes: frozenset[str]
    expected_edges: frozenset[tuple[str, str, str]]
    core_status: str
    core_issues: tuple[str, ...]
    core_disposition: str


def sha(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def verify_p1_source(repo: Path) -> str:
    lines = []
    for path, expected in P1_PATHS:
        relative = "src/ringfall-core/" + path
        try:
            actual = sha((repo / relative).read_bytes())
        except OSError as exc:
            raise MappingFailure("p1_source_unavailable") from exc
        if actual != expected:
            raise MappingFailure("p1_source_drift")
        lines.append(relative + "=" + actual)
    if sha("\n".join(lines).encode("utf-8")) != P1_AGGREGATE:
        raise MappingFailure("p1_aggregate_mismatch")
    return P1_AGGREGATE


def _unique(pairs: list[tuple[str, Any]]) -> dict[str, Any]:
    result: dict[str, Any] = {}
    for key, value in pairs:
        if key in result:
            raise MappingFailure("p1_duplicate_json_key")
        result[key] = value
    return result


def parse_p1(raw: bytes, inputs: dict[str, bytes]) -> dict[str, Any]:
    if not raw or len(raw) > 1_048_576:
        raise MappingFailure("p1_output_size")
    try:
        value = json.loads(raw.decode("utf-8"), object_pairs_hook=_unique,
                           parse_constant=lambda _: (_ for _ in ()).throw(MappingFailure("p1_nonfinite")))
    except (UnicodeError, json.JSONDecodeError, TypeError) as exc:
        raise MappingFailure("p1_malformed_json") from exc
    if not isinstance(value, dict):
        raise MappingFailure("p1_record_type")
    try:
        if (value["record_type"], value["evidence_version"], value["family_id"], value["family_version"]) != (
            "AsterF1P1Evidence", "0.1", "aster-l1-action-work-order", "0.1"
        ) or value["binding"]["status"] != "matched_projection_and_pulse":
            raise MappingFailure("p1_version_or_binding")
        if value["schema_validation"] != "not_run" or value["formal_proof"] != "not_run" or value["mutation_authorized"] is not False:
            raise MappingFailure("p1_overclaim")
        if value["input_versions"] != {"state": "0.1", "candidate": "0.1", "pulse": "0.1", "context_format": "actor_context_projection"}:
            raise MappingFailure("p1_input_versions")
        for name, key in (("state", "state_input_sha256"), ("candidate", "candidate_input_sha256"),
                          ("pulse", "pulse_input_sha256"), ("context", "context_input_sha256")):
            if value["inputs"][key] != sha(inputs[name]):
                raise MappingFailure("p1_input_drift")
        if value["state"]["changed"] not in (True, False):
            raise MappingFailure("p1_state_shape")
        if value["completeness"] not in ("complete", "unsupported"):
            raise MappingFailure("p1_completeness_shape")
    except (KeyError, TypeError, AttributeError) as exc:
        raise MappingFailure("p1_record_shape") from exc
    return value


def _field(row: Any, name: str, *, allow_absent: bool = False) -> Any:
    if not isinstance(row, dict) or row.get("name") != name or row.get("coverage") != "guarded_by_core_validator":
        raise UnsupportedFacts("unmapped_field")
    if row.get("presence") == "absent" and allow_absent and row.get("value") is None:
        return None
    if row.get("presence") != "value":
        raise UnsupportedFacts("unmapped_presence")
    return row.get("value")


def _boolean(value: Any, name: str) -> bool:
    if type(value) is not bool:
        raise UnsupportedFacts(name)
    return value


def map_p1(record: dict[str, Any], template: str) -> ClosedInstance:
    try:
        if record["completeness"] != "complete" or record["unsupported_predicates"]:
            raise UnsupportedFacts("p1_incomplete")
        core = record["core"]
        status = core["status"]
        issues = tuple(core["issues"])
        if status not in ("allowed", "denied", "invalid") or not isinstance(core["issues"], list):
            raise UnsupportedFacts("unmapped_core_status")
        if any(type(issue) is not str or issue not in ISSUE_PREDICATES for issue in issues):
            raise UnsupportedFacts("unmodeled_core_issue")
        if (status == "allowed") != (not issues):
            raise MappingFailure("p1_core_contradiction")
        if issues and status != ("invalid" if any(ISSUE_PREDICATES[issue][1] == "invalid" for issue in issues) else "denied"):
            raise MappingFailure("p1_core_status_mismatch")
        facts = record["facts"]
        if facts["issuer_id"] != "A1" or facts["issuer_layer"] != "L1" or facts["issuer_home_sector_id"] != "Aster":
            raise UnsupportedFacts("issuer_not_modeled")
        kind = facts["packet_kind"]
        if kind not in ("ToolActionRequest", "WorkOrderRequest"):
            raise UnsupportedFacts("packet_not_modeled")
        if facts["tool"] is not None and kind != "ToolActionRequest" or facts["work_order"] is not None and kind != "WorkOrderRequest":
            raise MappingFailure("p1_kind_mismatch")
        tool = kind == "ToolActionRequest"
        counts = {name: 0 for name in CLASSES}
        counts.update(F1Actor=1, F1Request=1, F1Sector=1)
        counts.update(F1Tool=int(tool), F1Action=int(tool), F1Crew=int(not tool), F1Location=int(not tool))
        names = {"a1", "r1", "s1"} | ({"t1", "ac1"} if tool else {"c1", "loc1"})
        edges: set[tuple[str, str, str]] = {("actorHomeSector", "a1", "s1"), ("issuer", "r1", "a1")}
        declarations = ["", "% One closed P1-bound intervention; no automatic nodes or supporting edges."]
        declarations.extend(f"!exists({name}::new)." for name in CLASSES)
        declarations.extend(f"default !{name}(*, *)." for name in RELATIONS)
        declarations.append("scope node = 5, " + ", ".join(f"{name} = {counts[name]}" for name in CLASSES) + ".")
        declarations.extend(("F1Actor(a1).", "F1Request(r1).", "F1Sector(s1)."))
        attrs = ["modeExecute(r1): false.", "requiresDryRunFalse(r1): false.",
                 "missingRequiredArgument(r1): false.", "fractionPresent(r1): false.",
                 "fractionOrZero(r1): 0.0."]

        if tool:
            target = facts["tool"]
            action = target["action"]
            if action not in RULES or target["tool_id"] != RULES[action][0] or target["actor_tool_referenced"] is not True:
                raise UnsupportedFacts("tool_relation_not_modeled")
            if target["status"] not in ("available", "unavailable") or action not in target["supported_actions"]:
                raise UnsupportedFacts("tool_status_or_support")
            if target["system_refs"] is None or target["mode"] not in ("dry_run", "execute"):
                raise UnsupportedFacts("tool_mode_or_refs")
            if target["arguments_presence"] not in ("absent", "value"):
                raise UnsupportedFacts("argument_container")
            row_values = facts["tool_arguments"]
            if not isinstance(row_values, list) or tuple(row.get("name") for row in row_values) != ARGUMENT_NAMES:
                raise MappingFailure("p1_argument_order")
            required = RULES[action][1]
            missing = []
            for row in row_values:
                name = row["name"]
                if name in required:
                    value = _field(row, name, allow_absent=True)
                    if value is None:
                        missing.append(name)
                    elif name == "load_fraction":
                        if type(value) not in (int, float) or not math.isfinite(value):
                            raise UnsupportedFacts("fraction_unmapped")
                    elif value not in STRING_ARGUMENTS[name]:
                        raise UnsupportedFacts("argument_unmapped")
                elif row.get("presence") != "absent" or row.get("coverage") != "guarded_by_core_validator":
                    raise UnsupportedFacts("extraneous_argument")
            if len(missing) > 1:
                raise UnsupportedFacts("multiple_missing_arguments")
            dry_run = target["requires_dry_run"]
            dry_run_value = _field(dry_run, "requires_dry_run", allow_absent=True)
            if dry_run_value is not None:
                _boolean(dry_run_value, "dry_run_flag")
            fraction = next((row["value"] for row in row_values if row["name"] == "load_fraction" and row["presence"] == "value"), None)
            if fraction is not None and action != "dry_run_reroute":
                raise UnsupportedFacts("fraction_outside_action")
            if fraction is not None:
                fraction_text = format(Decimal(str(fraction)), "f")
                if "." not in fraction_text:
                    fraction_text += ".0"
                attrs[3] = "fractionPresent(r1): true."
                attrs[4] = f"fractionOrZero(r1): {fraction_text}."
            attrs[0] = f"modeExecute(r1): {str(target['mode'] == 'execute').lower()}."
            attrs[1] = f"requiresDryRunFalse(r1): {str(dry_run_value is False).lower()}."
            attrs[2] = f"missingRequiredArgument(r1): {str(bool(missing)).lower()}."
            declarations.extend(("F1Tool(t1).", "F1Action(ac1)."))
            attrs.extend((f"toolAvailable(t1): {str(target['status'] == 'available').lower()}.",
                          f"requiresDryRun(ac1): {str(action in ('dry_run_reroute', 'dry_run_patch')).lower()}."))
            edges.update({("toolRef", "a1", "t1"), ("targetTool", "r1", "t1"),
                          ("supports", "t1", "ac1"), ("action", "r1", "ac1")})
        else:
            target = facts["work_order"]
            if target["actor_crew_referenced"] is not True or target["crew_id"] != "crew_aster_repair_02":
                raise UnsupportedFacts("crew_relation_not_modeled")
            if target["crew_status"] not in ("available", "unavailable") or target["assigned_actor_id"] != "A1" or target["crew_home_sector_id"] != "Aster":
                raise UnsupportedFacts("crew_state_not_modeled")
            if target["crew_system_refs"] is None or target["target_location"] != "Aster/Grid-Spine-03" or target["task_type"] != "inspect_and_patch":
                raise UnsupportedFacts("work_target_not_modeled")
            if _field(target["target_crew_id"], "target_crew_id") != "crew_aster_repair_02" or target["target_crew_pool_id"]["presence"] != "absent":
                raise UnsupportedFacts("work_target_presence")
            if any(row["coverage"] == "unsupported" for row in target["options"]):
                raise UnsupportedFacts("work_option_unmapped")
            declarations.extend(("F1Crew(c1).", "F1Location(loc1)."))
            attrs.append(f"crewAvailable(c1): {str(target['crew_status'] == 'available').lower()}.")
            edges.update({("crewRef", "a1", "c1"), ("targetCrew", "r1", "c1"),
                          ("assignedActor", "c1", "a1"), ("crewHomeSector", "c1", "s1"),
                          ("targetLocation", "r1", "loc1")})

        declarations.extend(f"{name}({source}, {target})." for name, source, target in sorted(edges))
        declarations.extend(attrs)
        text = template.rstrip() + "\n" + "\n".join(declarations) + "\n"
        if len(text.encode("utf-8")) > 64_000:
            raise MappingFailure("instance_too_large")
        return ClosedInstance(text, kind, frozenset(names), frozenset(edges), status, issues, core["disposition"])
    except (KeyError, TypeError, AttributeError, IndexError) as exc:
        raise MappingFailure("p1_fact_shape") from exc
