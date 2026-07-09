"""Deterministic dev/mock cognition trace and cost artifacts for B3-G."""

from __future__ import annotations

import json
from pathlib import Path
from typing import Any

from ringfall_brain.schemas.validator import validate_packet_json


PACKET_FILENAME = "avatar-pulse-packet.json"
COGNITION_TRACE_FILENAME = "cognition-trace.json"
COST_EVENT_FILENAME = "cost-event.json"


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


def _source_ref(ref_id: object, ref_type: str, artifact_uri: str) -> dict[str, str]:
    return {"ref_id": str(ref_id), "ref_type": ref_type, "artifact_uri": artifact_uri}


def _dump(payload: dict[str, Any]) -> str:
    return json.dumps(payload, sort_keys=True, separators=(",", ":"))
