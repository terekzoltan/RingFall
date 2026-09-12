"""Deterministic dev/mock cognition and cost artifacts for B3-G and A4-H."""

from __future__ import annotations

from collections.abc import Mapping
import json
from pathlib import Path
from typing import Any

from ringfall_brain.artifact_transaction import write_verified_artifacts
from ringfall_brain.cognition.aster_action_emitter import (
    TOOL_ACTION_FILENAME,
    TOOL_ACTION_KEY,
    WORK_ORDER_FILENAME,
    WORK_ORDER_KEY,
)
from ringfall_brain.schemas.validator import BrainValidationError, validate_packet_json


PACKET_FILENAME = "avatar-pulse-packet.json"
COGNITION_TRACE_FILENAME = "cognition-trace.json"
COST_EVENT_FILENAME = "cost-event.json"

TOOL_COGNITION_TRACE_KEY = "tool_action_cognition_trace"
WORK_ORDER_COGNITION_TRACE_KEY = "work_order_cognition_trace"
TOOL_COST_EVENT_KEY = "tool_action_cost_event"
WORK_ORDER_COST_EVENT_KEY = "work_order_cost_event"

TOOL_COGNITION_TRACE_FILENAME = "tool-action-cognition-trace.json"
WORK_ORDER_COGNITION_TRACE_FILENAME = "work-order-cognition-trace.json"
TOOL_COST_EVENT_FILENAME = "tool-action-cost-event.json"
WORK_ORDER_COST_EVENT_FILENAME = "work-order-cost-event.json"

ASTER_ARTIFACT_FILENAMES = (
    TOOL_ACTION_FILENAME,
    WORK_ORDER_FILENAME,
    TOOL_COGNITION_TRACE_FILENAME,
    WORK_ORDER_COGNITION_TRACE_FILENAME,
    TOOL_COST_EVENT_FILENAME,
    WORK_ORDER_COST_EVENT_FILENAME,
)

_ASTER_ARTIFACT_KEYS = (
    TOOL_ACTION_KEY,
    WORK_ORDER_KEY,
    TOOL_COGNITION_TRACE_KEY,
    WORK_ORDER_COGNITION_TRACE_KEY,
    TOOL_COST_EVENT_KEY,
    WORK_ORDER_COST_EVENT_KEY,
)
_CONTEXT_REF = {
    "ref_id": "ctx_A1_aster_heat_t000",
    "ref_type": "context",
    "artifact_uri": "fixtures://contexts/aster-a1-context.example.json",
}
_PROMPT_REF = {
    "ref_id": "prompt-l1-pulse-template",
    "ref_type": "prompt",
    "artifact_uri": "fixtures://prompts/l1_pulse_context_template.md",
}
_FROZEN_A4_E_TOOL_ACTION_PAYLOAD = {
    "packet_id": "draft_A1_tool_heat_alarm_check",
    "packet_type": "ToolActionRequest",
    "schema_version": "0.1",
    "issuer_id": "A1",
    "issuer_layer": "L1",
    "issued_at_tick": 0,
    "source_context_id": "ctx_A1_aster_heat_t000",
    "evidence_refs": [
        "obs-a1-t000-aster-r2-heat-alarm",
        "event:event-t000-aster-r2-heat-alarm",
    ],
    "tool_id": "local_grid_panel",
    "action": "query_heat_alarm",
    "mode": "dry_run",
}
_FROZEN_A4_E_WORK_ORDER_PAYLOAD = {
    "packet_id": "draft_A1_work_order_heat_alarm_inspection",
    "packet_type": "WorkOrderRequest",
    "schema_version": "0.1",
    "issuer_id": "A1",
    "issuer_layer": "L1",
    "issued_at_tick": 0,
    "source_context_id": "ctx_A1_aster_heat_t000",
    "evidence_refs": [
        "obs-a1-t000-aster-r2-heat-alarm",
        "event:event-t000-aster-r2-heat-alarm",
    ],
    "target_crew_id": "crew_aster_repair_02",
    "target_location": "Aster/Grid-Spine-03",
    "task_type": "inspect_and_patch",
    "priority": "high",
}
_ASTER_SPECS = (
    {
        "candidate_key": TOOL_ACTION_KEY,
        "trace_key": TOOL_COGNITION_TRACE_KEY,
        "cost_key": TOOL_COST_EVENT_KEY,
        "packet_id": "draft_A1_tool_heat_alarm_check",
        "cognition_id": "cog_A1_t000_tool_heat_alarm_check",
        "cost_id": "cost_A1_t000_tool_heat_alarm_check",
        "lane": "l1_tool_action",
        "raw_id": "raw_draft_A1_tool_heat_alarm_check",
        "candidate_uri": "fixtures://aster-a4-h/tool-action-request.json",
        "trace_uri": "fixtures://aster-a4-h/tool-action-cognition-trace.json",
        "cost_uri": "fixtures://aster-a4-h/tool-action-cost-event.json",
    },
    {
        "candidate_key": WORK_ORDER_KEY,
        "trace_key": WORK_ORDER_COGNITION_TRACE_KEY,
        "cost_key": WORK_ORDER_COST_EVENT_KEY,
        "packet_id": "draft_A1_work_order_heat_alarm_inspection",
        "cognition_id": "cog_A1_t000_work_order_heat_alarm_inspection",
        "cost_id": "cost_A1_t000_work_order_heat_alarm_inspection",
        "lane": "l1_work_order",
        "raw_id": "raw_draft_A1_work_order_heat_alarm_inspection",
        "candidate_uri": "fixtures://aster-a4-h/work-order-request.json",
        "trace_uri": "fixtures://aster-a4-h/work-order-cognition-trace.json",
        "cost_uri": "fixtures://aster-a4-h/work-order-cost-event.json",
    },
)


def build_mock_cognition_artifacts(
    packet: dict[str, object],
    packet_schema: Path,
    cognition_schema: Path,
    cost_schema: Path,
) -> dict[str, dict[str, Any]]:
    validated_packet = validate_packet_json(_dump(packet), packet_schema)
    cognition_trace = _build_cognition_trace(validated_packet, schema_valid=False)
    cost_event = _build_cost_event(cognition_trace, schema_valid=False)

    validate_packet_json(_dump(cognition_trace), cognition_schema)
    validate_packet_json(_dump(cost_event), cost_schema)

    cognition_trace["schema_valid"] = True
    cost_event["schema_valid"] = True
    validate_packet_json(_dump(cognition_trace), cognition_schema)
    validate_packet_json(_dump(cost_event), cost_schema)
    return {"packet": validated_packet, "cognition_trace": cognition_trace, "cost_event": cost_event}


def write_mock_cognition_artifacts(artifacts: dict[str, dict[str, Any]], output_dir: Path) -> list[Path]:
    output_dir.mkdir(parents=True, exist_ok=True)
    files = [
        (PACKET_FILENAME, artifacts["packet"]),
        (COGNITION_TRACE_FILENAME, artifacts["cognition_trace"]),
        (COST_EVENT_FILENAME, artifacts["cost_event"]),
    ]
    written: list[Path] = []
    for filename, payload in files:
        path = output_dir / filename
        path.write_text(_dump(payload) + "\n", encoding="utf-8")
        written.append(path)
    return written


def build_aster_cognition_artifacts(
    candidates: Mapping[str, Mapping[str, Any]],
    tool_schema: Path,
    work_order_schema: Path,
    cognition_schema: Path,
    cost_schema: Path,
) -> dict[str, dict[str, Any]]:
    """Build the deterministic A4-H proposal, cognition, and cost bundle."""
    if set(candidates) != {TOOL_ACTION_KEY, WORK_ORDER_KEY}:
        raise BrainValidationError(
            "Aster cognition candidates must contain exactly tool_action_request and work_order_request"
        )

    candidate_objects = {
        TOOL_ACTION_KEY: _require_mapping(candidates[TOOL_ACTION_KEY], "tool action candidate"),
        WORK_ORDER_KEY: _require_mapping(candidates[WORK_ORDER_KEY], "work order candidate"),
    }
    frozen_candidates = {
        TOOL_ACTION_KEY: _FROZEN_A4_E_TOOL_ACTION_PAYLOAD,
        WORK_ORDER_KEY: _FROZEN_A4_E_WORK_ORDER_PAYLOAD,
    }
    for key, candidate in candidate_objects.items():
        candidate_json = _dump_checked(candidate, f"{key} candidate")
        frozen_json = _dump_checked(frozen_candidates[key], f"frozen A4-E {key} payload")
        if candidate_json != frozen_json:
            raise BrainValidationError(f"{key} candidate does not match the exact accepted A4-E payload")

    tool_candidate = _validate_aster_schema(candidate_objects[TOOL_ACTION_KEY], tool_schema, "tool action schema")
    work_order_candidate = _validate_aster_schema(
        candidate_objects[WORK_ORDER_KEY], work_order_schema, "work order schema"
    )

    artifacts: dict[str, dict[str, Any]] = {
        TOOL_ACTION_KEY: tool_candidate,
        WORK_ORDER_KEY: work_order_candidate,
    }
    for spec in _ASTER_SPECS:
        trace = _build_aster_cognition_trace(spec, schema_valid=False)
        cost = _build_aster_cost_event(spec, schema_valid=False)
        _validate_aster_schema(trace, cognition_schema, "cognition trace schema")
        _validate_aster_schema(cost, cost_schema, "cost event schema")
        trace["schema_valid"] = True
        cost["schema_valid"] = True
        _validate_aster_schema(trace, cognition_schema, "cognition trace schema")
        _validate_aster_schema(cost, cost_schema, "cost event schema")
        artifacts[spec["trace_key"]] = trace
        artifacts[spec["cost_key"]] = cost

    return {
        TOOL_ACTION_KEY: artifacts[TOOL_ACTION_KEY],
        WORK_ORDER_KEY: artifacts[WORK_ORDER_KEY],
        TOOL_COGNITION_TRACE_KEY: artifacts[TOOL_COGNITION_TRACE_KEY],
        WORK_ORDER_COGNITION_TRACE_KEY: artifacts[WORK_ORDER_COGNITION_TRACE_KEY],
        TOOL_COST_EVENT_KEY: artifacts[TOOL_COST_EVENT_KEY],
        WORK_ORDER_COST_EVENT_KEY: artifacts[WORK_ORDER_COST_EVENT_KEY],
    }


def write_aster_cognition_artifacts(
    artifacts: Mapping[str, Mapping[str, Any]],
    output_dir: Path,
    tool_schema: Path,
    work_order_schema: Path,
    cognition_schema: Path,
    cost_schema: Path,
) -> list[Path]:
    """Validate and transactionally write the exact six-file A4-H bundle."""
    if set(artifacts) != set(_ASTER_ARTIFACT_KEYS):
        raise BrainValidationError("Aster cognition artifacts must contain exactly the six A4-H artifact keys")

    expected = build_aster_cognition_artifacts(
        {
            TOOL_ACTION_KEY: artifacts[TOOL_ACTION_KEY],
            WORK_ORDER_KEY: artifacts[WORK_ORDER_KEY],
        },
        tool_schema,
        work_order_schema,
        cognition_schema,
        cost_schema,
    )
    for key in _ASTER_ARTIFACT_KEYS:
        value = _require_mapping(artifacts[key], f"{key} artifact")
        if value != expected[key]:
            raise BrainValidationError(f"{key} artifact does not match the deterministic A4-H graph")

    payloads = [
        (filename, (_dump_checked(expected[key], f"{key} artifact") + "\n").encode("utf-8"))
        for key, filename in zip(_ASTER_ARTIFACT_KEYS, ASTER_ARTIFACT_FILENAMES, strict=True)
    ]
    return write_verified_artifacts(payloads, output_dir, "Aster artifact")


def _build_cognition_trace(packet: dict[str, Any], schema_valid: bool) -> dict[str, Any]:
    cognition_id = "cog-mock-001"
    cost_event_id = "cost-event-mock-001"
    return {
        "trace_type": "CognitionTrace",
        "schema_version": "0.1",
        "cognition_id": cognition_id,
        "turn_ref": "turn-mock-001",
        "tick": packet["issued_at_tick"],
        "lane": "l1_pulse",
        "target_id": packet["actor_id"],
        "context_ref": _source_ref("ctx-aster-001", "context", "fixtures://contexts/ctx-aster-001.json"),
        "prompt_ref": _source_ref("prompt-l1-pulse-template", "prompt", "fixtures://prompts/l1-pulse-context-template.md"),
        "raw_output_ref": _source_ref("raw-output-mock-001", "raw_output", f"fixtures://outputs/{PACKET_FILENAME}"),
        "parsed_packet_ref": _source_ref(packet["packet_id"], "packet", f"fixtures://packets/{PACKET_FILENAME}"),
        "schema_valid": schema_valid,
        "model_evidence": {
            "model_id": "fixture/mock",
            "provider": "mock_provider",
            "run_mode": "dev",
            "temperature": 0,
            "max_output_tokens": 1,
        },
        "retry_count": 0,
        "fallback_used": False,
        "cost_event_ref": _source_ref(cost_event_id, "cost_event", f"fixtures://traces/{COST_EVENT_FILENAME}"),
    }


def _build_cost_event(cognition_trace: dict[str, Any], schema_valid: bool) -> dict[str, Any]:
    return {
        "event_type": "CostEvent",
        "schema_version": "0.1",
        "cost_event_id": cognition_trace["cost_event_ref"]["ref_id"],
        "cognition_id": cognition_trace["cognition_id"],
        "run_id": "run-dev-mock-001",
        "turn_ref": cognition_trace["turn_ref"],
        "tick": cognition_trace["tick"],
        "lane": cognition_trace["lane"],
        "run_mode": "dev",
        "provider": "mock_provider",
        "model_id": "fixture/mock",
        "input_tokens": 0,
        "output_tokens": 0,
        "estimated_cost_usd": 0,
        "cost_estimate_status": "free_or_mock_zero",
        "latency_ms": 0,
        "retry_count": 0,
        "fallback_count": 0,
        "schema_valid": schema_valid,
        "free_model_used": True,
        "fallback_used": False,
        "budget_status": "not_applicable",
        "cognition_trace_ref": _source_ref(
            cognition_trace["cognition_id"],
            "cognition_trace",
            f"fixtures://traces/{COGNITION_TRACE_FILENAME}",
        ),
    }


def _build_aster_cognition_trace(spec: dict[str, str], schema_valid: bool) -> dict[str, Any]:
    return {
        "trace_type": "CognitionTrace",
        "schema_version": "0.1",
        "cognition_id": spec["cognition_id"],
        "turn_ref": "turn_A1_t000_aster_actions",
        "tick": 0,
        "lane": spec["lane"],
        "target_id": "A1",
        "context_ref": dict(_CONTEXT_REF),
        "prompt_ref": dict(_PROMPT_REF),
        "raw_output_ref": _source_ref(spec["raw_id"], "raw_output", spec["candidate_uri"]),
        "parsed_packet_ref": _source_ref(spec["packet_id"], "packet", spec["candidate_uri"]),
        "schema_valid": schema_valid,
        "model_evidence": {
            "provider": "deterministic_fixture",
            "model_id": "fixture/aster-a4-e",
            "run_mode": "dev",
            "temperature": 0,
            "max_output_tokens": 1,
        },
        "retry_count": 0,
        "fallback_used": False,
        "cost_event_ref": _source_ref(spec["cost_id"], "cost_event", spec["cost_uri"]),
    }


def _build_aster_cost_event(spec: dict[str, str], schema_valid: bool) -> dict[str, Any]:
    return {
        "event_type": "CostEvent",
        "schema_version": "0.1",
        "cost_event_id": spec["cost_id"],
        "cognition_id": spec["cognition_id"],
        "run_id": "run_dev_Aster_t000",
        "turn_ref": "turn_A1_t000_aster_actions",
        "tick": 0,
        "lane": spec["lane"],
        "run_mode": "dev",
        "provider": "deterministic_fixture",
        "model_id": "fixture/aster-a4-e",
        "input_tokens": 0,
        "output_tokens": 0,
        "estimated_cost_usd": 0,
        "cost_estimate_status": "free_or_mock_zero",
        "latency_ms": 0,
        "retry_count": 0,
        "fallback_count": 0,
        "schema_valid": schema_valid,
        "target_id": "A1",
        "request_ref": _source_ref(spec["packet_id"], "packet", spec["candidate_uri"]),
        "cognition_trace_ref": _source_ref(
            spec["cognition_id"], "cognition_trace", spec["trace_uri"]
        ),
        "context_ref": dict(_CONTEXT_REF),
        "free_model_used": True,
        "fallback_used": False,
        "budget_status": "not_applicable",
    }


def _validate_aster_schema(
    value: Mapping[str, Any], schema_path: Path, schema_label: str
) -> dict[str, Any]:
    try:
        return validate_packet_json(_dump_checked(value, schema_label), schema_path)
    except UnicodeDecodeError as exc:
        raise BrainValidationError(f"{schema_label} file is not valid UTF-8") from exc


def _require_mapping(value: object, label: str) -> dict[str, Any]:
    if not isinstance(value, Mapping):
        raise BrainValidationError(f"{label} must be a JSON object")
    return dict(value)


def _dump_checked(value: object, label: str) -> str:
    try:
        return json.dumps(
            value,
            sort_keys=True,
            separators=(",", ":"),
            ensure_ascii=True,
            allow_nan=False,
        )
    except (TypeError, ValueError) as exc:
        raise BrainValidationError(f"{label} must contain only JSON values") from exc


def _source_ref(ref_id: object, ref_type: str, artifact_uri: str) -> dict[str, str]:
    return {"ref_id": str(ref_id), "ref_type": ref_type, "artifact_uri": artifact_uri}


def _dump(payload: dict[str, Any]) -> str:
    return json.dumps(payload, sort_keys=True, separators=(",", ":"))
