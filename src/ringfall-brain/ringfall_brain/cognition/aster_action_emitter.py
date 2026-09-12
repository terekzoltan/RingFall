"""Deterministic Aster L1 action candidates for Wave 4 A4-E."""

from __future__ import annotations

import json
from collections.abc import Mapping
from pathlib import Path
from typing import Any

from ringfall_brain.artifact_transaction import write_verified_artifacts
from ringfall_brain.schemas.validator import BrainValidationError, validate_packet_json


TOOL_ACTION_KEY = "tool_action_request"
WORK_ORDER_KEY = "work_order_request"
TOOL_ACTION_FILENAME = "tool-action-request.json"
WORK_ORDER_FILENAME = "work-order-request.json"

_EXPECTED_CONTEXT_KEYS = {
    "actorId",
    "displayName",
    "role",
    "layer",
    "observations",
    "crewRefs",
    "toolRefs",
}
_EXPECTED_OBSERVATION = {
    "observationId": "obs-a1-t000-aster-r2-heat-alarm",
    "kind": "alarm",
    "signal": "Local R2 heat alarm is active.",
    "sourceRef": "event:event-t000-aster-r2-heat-alarm",
}
_EXPECTED_DRAFTS = {
    "SceneActionPacket": "draft_A1_scene_heat_alarm",
    "ToolActionRequest": "draft_A1_tool_heat_alarm_check",
    "WorkOrderRequest": "draft_A1_work_order_heat_alarm_inspection",
}
def build_aster_action_candidates(
    context: Mapping[str, object],
    pulse: Mapping[str, object],
    pulse_schema: Path,
    tool_schema: Path,
    work_order_schema: Path,
) -> dict[str, dict[str, Any]]:
    """Build and schema-validate the two bounded Aster action proposals."""
    context_data = _require_json_object(context, "A4-D context")
    pulse_data = _validate_packet_with_schema(
        _dump_json(pulse, "A4-D pulse"),
        pulse_schema,
        "pulse schema",
    )

    _validate_context(context_data)
    _validate_pulse(context_data, pulse_data)

    evidence_refs = [
        _EXPECTED_OBSERVATION["observationId"],
        _EXPECTED_OBSERVATION["sourceRef"],
    ]
    common = {
        "schema_version": "0.1",
        "issuer_id": "A1",
        "issuer_layer": "L1",
        "issued_at_tick": 0,
        "source_context_id": "ctx_A1_aster_heat_t000",
        "evidence_refs": evidence_refs,
    }
    tool_action = {
        **common,
        "packet_id": _EXPECTED_DRAFTS["ToolActionRequest"],
        "packet_type": "ToolActionRequest",
        "tool_id": "local_grid_panel",
        "action": "query_heat_alarm",
        "mode": "dry_run",
    }
    work_order = {
        **common,
        "evidence_refs": list(evidence_refs),
        "packet_id": _EXPECTED_DRAFTS["WorkOrderRequest"],
        "packet_type": "WorkOrderRequest",
        "target_crew_id": "crew_aster_repair_02",
        "target_location": "Aster/Grid-Spine-03",
        "task_type": "inspect_and_patch",
        "priority": "high",
    }

    validated_tool = _validate_packet_with_schema(
        _dump_json(tool_action, "tool action candidate"),
        tool_schema,
        "tool action schema",
    )
    validated_work_order = _validate_packet_with_schema(
        _dump_json(work_order, "work order candidate"),
        work_order_schema,
        "work order schema",
    )
    return {
        TOOL_ACTION_KEY: validated_tool,
        WORK_ORDER_KEY: validated_work_order,
    }


def write_aster_action_candidates(
    candidates: Mapping[str, Mapping[str, Any]],
    output_dir: Path,
    tool_schema: Path,
    work_order_schema: Path,
) -> list[Path]:
    """Reserve, write, and verify both final candidates without clobbering."""
    if set(candidates) != {TOOL_ACTION_KEY, WORK_ORDER_KEY}:
        raise BrainValidationError(
            "Aster action candidates must contain exactly tool_action_request and work_order_request"
        )

    specifications = (
        (TOOL_ACTION_KEY, TOOL_ACTION_FILENAME, tool_schema),
        (WORK_ORDER_KEY, WORK_ORDER_FILENAME, work_order_schema),
    )
    payloads: list[tuple[str, bytes]] = []
    for key, filename, schema_path in specifications:
        candidate = _require_json_object(candidates[key], f"{key} candidate")
        raw_candidate = _dump_json(candidate, f"{key} candidate")
        schema_label = "tool action schema" if key == TOOL_ACTION_KEY else "work order schema"
        validated = _validate_packet_with_schema(raw_candidate, schema_path, schema_label)
        payloads.append((filename, (_dump_json(validated, f"{key} candidate") + "\n").encode("utf-8")))

    return write_verified_artifacts(payloads, output_dir, "Aster action")


def _validate_context(context: dict[str, Any]) -> None:
    if set(context) != _EXPECTED_CONTEXT_KEYS:
        raise BrainValidationError("A4-D context fields do not match the accepted Aster input surface")
    if context["actorId"] != "A1" or context["layer"] != "L1":
        raise BrainValidationError("A4-D context actor or layer does not match the accepted Aster identity")
    if context["displayName"] != "A1" or context["role"] != "senior grid runner":
        raise BrainValidationError("A4-D context role does not match the accepted Aster identity")
    if context["observations"] != [_EXPECTED_OBSERVATION]:
        raise BrainValidationError("A4-D context observation does not match the accepted visible heat alarm")
    if context["crewRefs"] != ["crew_aster_repair_02"]:
        raise BrainValidationError("A4-D context crew references do not match the accepted Aster target")
    if context["toolRefs"] != ["local_grid_panel", "maintenance_console"]:
        raise BrainValidationError("A4-D context tool references do not match the accepted Aster targets")


def _validate_pulse(context: dict[str, Any], pulse: dict[str, Any]) -> None:
    if pulse.get("packet_id") != "pulse_A1_t000":
        raise BrainValidationError("A4-D pulse identity does not match the accepted Aster pulse")
    if not (
        pulse.get("issuer_id") == pulse.get("actor_id") == context["actorId"] == "A1"
        and pulse.get("issuer_layer") == context["layer"] == "L1"
    ):
        raise BrainValidationError("A4-D pulse actor or layer does not match the accepted context")
    if pulse.get("issued_at_tick") != 0 or pulse.get("source_context_id") != "ctx_A1_aster_heat_t000":
        raise BrainValidationError("A4-D pulse tick or source context does not match the accepted Aster pulse")
    if pulse.get("observed") != [_EXPECTED_OBSERVATION["signal"]]:
        raise BrainValidationError("A4-D pulse observation does not preserve the accepted visible signal")

    expected_evidence = [
        _EXPECTED_OBSERVATION["observationId"],
        _EXPECTED_OBSERVATION["sourceRef"],
    ]
    if pulse.get("evidence_refs") != expected_evidence:
        raise BrainValidationError("A4-D pulse evidence does not preserve the accepted observation provenance")

    requested_packets = pulse.get("requested_packets")
    if not isinstance(requested_packets, list) or len(requested_packets) != len(_EXPECTED_DRAFTS):
        raise BrainValidationError("A4-D pulse must contain the accepted scene, tool, and work-order drafts")
    for packet_type, expected_draft in _EXPECTED_DRAFTS.items():
        matches = [
            item
            for item in requested_packets
            if isinstance(item, dict) and item.get("packet_type") == packet_type
        ]
        if len(matches) != 1 or matches[0].get("draft_ref") != expected_draft:
            raise BrainValidationError(f"A4-D pulse {packet_type} draft does not match the accepted request")


def _require_json_object(value: object, label: str) -> dict[str, Any]:
    if not isinstance(value, Mapping):
        raise BrainValidationError(f"{label} must be a JSON object")
    return dict(value)


def _dump_json(value: object, label: str) -> str:
    try:
        return json.dumps(value, sort_keys=True, separators=(",", ":"), ensure_ascii=True)
    except (TypeError, ValueError) as exc:
        raise BrainValidationError(f"{label} must contain only JSON values") from exc


def _validate_packet_with_schema(raw_packet: str, schema_path: Path, schema_label: str) -> dict[str, Any]:
    try:
        return validate_packet_json(raw_packet, schema_path)
    except UnicodeDecodeError as exc:
        raise BrainValidationError(f"{schema_label} file is not valid UTF-8") from exc
