#!/usr/bin/env python3
"""Validate generated Wave 3 dev/mock brain artifacts against public contracts."""

from __future__ import annotations

import argparse
import json
import math
import sys
from pathlib import Path
from typing import Any

try:
    from jsonschema import Draft202012Validator
except ModuleNotFoundError:  # pragma: no cover - exercised by missing local dependency.
    print(
        "Missing dependency: jsonschema. Install dev tooling with "
        "`python -m pip install -r requirements-dev.txt`.",
        file=sys.stderr,
    )
    sys.exit(2)


ROOT = Path(__file__).resolve().parents[1]
PACKET_SCHEMA = ROOT / "src" / "ringfall-contracts" / "schemas" / "packets" / "avatar-pulse-packet.schema.json"
COGNITION_TRACE_SCHEMA = ROOT / "src" / "ringfall-contracts" / "schemas" / "traces" / "cognition-trace.schema.json"
COST_EVENT_SCHEMA = ROOT / "src" / "ringfall-contracts" / "schemas" / "traces" / "cost-event.schema.json"

PACKET_FILE = "avatar-pulse-packet.json"
COGNITION_TRACE_FILE = "cognition-trace.json"
COST_EVENT_FILE = "cost-event.json"
REQUIRED_FILES = {PACKET_FILE, COGNITION_TRACE_FILE, COST_EVENT_FILE}

DIRECT_MUTATION_KEYS = {
    "action_trace",
    "core_command",
    "execution_result",
    "mutations",
    "state_diff",
    "state_patch",
    "world_state_patch",
}
HIDDEN_TEXT_PATTERNS = ("thermaldebt", "thermal_debt", "hidden thermal")
HIDDEN_NUMERIC_PATTERNS = ("0.41", "0.46")


class SmokeError(Exception):
    pass


def reject_json_constant(value: str) -> None:
    raise SmokeError(f"non-standard JSON numeric constant is not allowed: {value}")


def reject_duplicate_keys(pairs: list[tuple[str, Any]]) -> dict[str, Any]:
    result: dict[str, Any] = {}
    for key, value in pairs:
        if key in result:
            raise SmokeError(f"duplicate JSON object key is not allowed: {key!r}")
        result[key] = value
    return result


def load_json(path: Path) -> Any:
    try:
        with path.open("r", encoding="utf-8") as handle:
            return json.load(
                handle,
                parse_constant=reject_json_constant,
                object_pairs_hook=reject_duplicate_keys,
            )
    except json.JSONDecodeError as exc:
        raise SmokeError(f"{path}: invalid JSON at line {exc.lineno}, column {exc.colno}: {exc.msg}") from exc
    except UnicodeError as exc:
        raise SmokeError(f"{path}: JSON file cannot be decoded as UTF-8: {exc}") from exc
    except OSError as exc:
        raise SmokeError(f"{path}: JSON file cannot be read: {exc}") from exc
    except ValueError as exc:
        raise SmokeError(f"{path}: JSON numeric value cannot be decoded: {exc}") from exc


def json_pointer(error_path: object) -> str:
    parts = [str(part).replace("~", "~0").replace("/", "~1") for part in error_path]
    return "$" if not parts else "$/" + "/".join(parts)


def validate_schema(artifact_path: Path, schema_path: Path) -> dict[str, Any]:
    artifact = load_json(artifact_path)
    if not isinstance(artifact, dict):
        raise SmokeError(f"{artifact_path}: artifact JSON must parse to an object")
    schema = load_json(schema_path)
    validator = Draft202012Validator(schema)
    errors = sorted(validator.iter_errors(artifact), key=lambda error: list(error.path))
    if errors:
        first = errors[0]
        raise SmokeError(
            f"{artifact_path}: failed schema {schema_path} at {json_pointer(first.path)}: {first.message}"
        )
    return artifact


def validate_file_set(artifact_root: Path) -> None:
    if not artifact_root.is_dir():
        raise SmokeError(f"artifact directory does not exist: {artifact_root}")
    try:
        entries = {entry.name for entry in artifact_root.iterdir()}
    except OSError as exc:
        raise SmokeError(f"{artifact_root}: artifact directory cannot be inspected: {exc}") from exc
    missing = sorted(REQUIRED_FILES - entries)
    if missing:
        raise SmokeError(f"missing required artifact(s): {missing}")
    extra = sorted(entries - REQUIRED_FILES)
    if extra:
        raise SmokeError(f"unexpected top-level artifact(s): {extra}")
    for filename in sorted(REQUIRED_FILES):
        if not (artifact_root / filename).is_file():
            raise SmokeError(f"required artifact must be a file: {filename}")


def validate_bundle(artifact_root: Path) -> None:
    validate_file_set(artifact_root)
    raw_packet = load_artifact_object(artifact_root / PACKET_FILE)
    raw_trace = load_artifact_object(artifact_root / COGNITION_TRACE_FILE)
    raw_cost = load_artifact_object(artifact_root / COST_EVENT_FILE)

    validate_no_direct_mutation(raw_packet, raw_trace, raw_cost)
    validate_finite_numbers(raw_packet, raw_trace, raw_cost)
    validate_hidden_truth_smoke(raw_packet, raw_trace, raw_cost)

    packet = validate_schema(artifact_root / PACKET_FILE, PACKET_SCHEMA)
    trace = validate_schema(artifact_root / COGNITION_TRACE_FILE, COGNITION_TRACE_SCHEMA)
    cost = validate_schema(artifact_root / COST_EVENT_FILE, COST_EVENT_SCHEMA)

    validate_cross_links(packet, trace, cost)
    validate_dev_mock_boundaries(trace, cost)


def load_artifact_object(path: Path) -> dict[str, Any]:
    artifact = load_json(path)
    if not isinstance(artifact, dict):
        raise SmokeError(f"{path}: artifact JSON must parse to an object")
    return artifact


def validate_cross_links(packet: dict[str, Any], trace: dict[str, Any], cost: dict[str, Any]) -> None:
    require_equal(packet, "packet.packet_type", "AvatarPulsePacket")
    require_equal(trace, "trace.trace_type", "CognitionTrace")
    require_equal(cost, "cost.event_type", "CostEvent")
    require_equal(trace, "trace.schema_valid", True)
    require_equal(cost, "cost.schema_valid", True)
    require_equal(trace, "trace.parsed_packet_ref.ref_id", packet["packet_id"])
    require_equal(trace, "trace.target_id", packet["actor_id"])
    require_equal(trace, "trace.tick", packet["issued_at_tick"])
    require_equal(trace, "trace.cost_event_ref.ref_id", cost["cost_event_id"])
    require_equal(cost, "cost.cognition_id", trace["cognition_id"])
    require_equal(cost, "cost.cognition_trace_ref.ref_id", trace["cognition_id"])
    require_equal(cost, "cost.turn_ref", trace["turn_ref"])
    require_equal(cost, "cost.tick", trace["tick"])
    require_equal(cost, "cost.lane", trace["lane"])
    require_equal(trace, "trace.parsed_packet_ref.ref_type", "packet")
    require_equal(trace, "trace.raw_output_ref.ref_type", "raw_output")
    require_equal(trace, "trace.cost_event_ref.ref_type", "cost_event")
    require_equal(cost, "cost.cognition_trace_ref.ref_type", "cognition_trace")
    require_equal(trace, "trace.context_ref.ref_id", packet["source_context_id"])
    require_equal(trace, "trace.context_ref.ref_type", "context")
    require_equal(trace, "trace.prompt_ref.ref_type", "prompt")

    require_artifact_basename(trace, "trace.parsed_packet_ref.artifact_uri", PACKET_FILE)
    require_artifact_basename(trace, "trace.raw_output_ref.artifact_uri", PACKET_FILE)
    require_artifact_basename(trace, "trace.cost_event_ref.artifact_uri", COST_EVENT_FILE)
    require_artifact_basename(cost, "cost.cognition_trace_ref.artifact_uri", COGNITION_TRACE_FILE)
    require_equal(packet, "packet.issuer_layer", "L1")
    require_equal(packet, "packet.issuer_id", packet["actor_id"])


def validate_dev_mock_boundaries(trace: dict[str, Any], cost: dict[str, Any]) -> None:
    require_equal(trace, "trace.model_evidence.provider", "mock_provider")
    require_equal(trace, "trace.model_evidence.run_mode", "dev")
    require_equal(trace, "trace.model_evidence.model_id", "fixture/mock")
    require_equal(trace, "trace.retry_count", 0)
    require_equal(trace, "trace.fallback_used", False)
    require_equal(cost, "cost.provider", "mock_provider")
    require_equal(cost, "cost.run_mode", "dev")
    require_equal(cost, "cost.model_id", "fixture/mock")
    require_equal(cost, "cost.estimated_cost_usd", 0)
    require_equal(cost, "cost.cost_estimate_status", "free_or_mock_zero")
    require_equal(cost, "cost.free_model_used", True)
    require_equal(cost, "cost.fallback_used", False)
    require_equal(cost, "cost.retry_count", 0)
    require_equal(cost, "cost.fallback_count", 0)
    require_equal(cost, "cost.budget_status", "not_applicable")


def validate_no_direct_mutation(*artifacts: dict[str, Any]) -> None:
    for index, artifact in enumerate(artifacts):
        for path, key in walk_keys(artifact):
            if key in DIRECT_MUTATION_KEYS:
                raise SmokeError(f"artifact {index}: direct world mutation key is not allowed at {path}: {key}")


def validate_finite_numbers(*artifacts: dict[str, Any]) -> None:
    for index, artifact in enumerate(artifacts):
        for path, value in walk_numbers(artifact):
            if isinstance(value, float) and not math.isfinite(value):
                raise SmokeError(f"artifact {index}: non-finite JSON number is not allowed at {path}: {value}")


def validate_hidden_truth_smoke(*artifacts: dict[str, Any]) -> None:
    for index, artifact in enumerate(artifacts):
        for path, value in walk_strings(artifact):
            lowered = value.lower()
            for pattern in HIDDEN_TEXT_PATTERNS:
                if pattern in lowered:
                    raise SmokeError(f"artifact {index}: hidden truth text appears at {path}: {pattern}")
            for pattern in HIDDEN_NUMERIC_PATTERNS:
                if pattern in value:
                    raise SmokeError(f"artifact {index}: hidden truth value appears at {path}: {pattern}")


def require_equal(root: dict[str, Any], dotted_path: str, expected: object) -> None:
    actual = nested_value(root, dotted_path.split(".", 1)[1])
    if actual != expected:
        raise SmokeError(f"{dotted_path} must be {expected!r}, found {actual!r}")


def require_artifact_basename(root: dict[str, Any], dotted_path: str, expected: str) -> None:
    value = nested_value(root, dotted_path.split(".", 1)[1])
    if not isinstance(value, str) or not value:
        raise SmokeError(f"{dotted_path} requires artifact_uri")
    basename = value.replace("\\", "/").rsplit("/", 1)[-1]
    if basename != expected:
        raise SmokeError(f"{dotted_path} basename must be {expected}, found {basename}")


def nested_value(root: dict[str, Any], path: str) -> Any:
    value: Any = root
    for part in path.split("."):
        if not isinstance(value, dict) or part not in value:
            raise SmokeError(f"missing required field: {path}")
        value = value[part]
    return value


def walk_keys(value: Any, path: str = "$") -> list[tuple[str, str]]:
    found: list[tuple[str, str]] = []
    if isinstance(value, dict):
        for key, nested in value.items():
            key_path = f"{path}.{key}"
            found.append((key_path, key))
            found.extend(walk_keys(nested, key_path))
    elif isinstance(value, list):
        for index, nested in enumerate(value):
            found.extend(walk_keys(nested, f"{path}[{index}]"))
    return found


def walk_strings(value: Any, path: str = "$") -> list[tuple[str, str]]:
    found: list[tuple[str, str]] = []
    if isinstance(value, str):
        found.append((path, value))
    elif isinstance(value, dict):
        for key, nested in value.items():
            found.extend(walk_strings(nested, f"{path}.{key}"))
    elif isinstance(value, list):
        for index, nested in enumerate(value):
            found.extend(walk_strings(nested, f"{path}[{index}]"))
    return found


def walk_numbers(value: Any, path: str = "$") -> list[tuple[str, int | float]]:
    found: list[tuple[str, int | float]] = []
    if isinstance(value, (int, float)) and not isinstance(value, bool):
        found.append((path, value))
    elif isinstance(value, dict):
        for key, nested in value.items():
            found.extend(walk_numbers(nested, f"{path}.{key}"))
    elif isinstance(value, list):
        for index, nested in enumerate(value):
            found.extend(walk_numbers(nested, f"{path}[{index}]"))
    return found


def main() -> int:
    parser = argparse.ArgumentParser(description="Validate a Ringfall Wave 3 dev/mock brain artifact directory.")
    parser.add_argument("artifact_dir", help="Path to the generated brain artifact directory.")
    args = parser.parse_args()

    try:
        validate_bundle(Path(args.artifact_dir))
    except SmokeError as exc:
        print(f"brain_artifact_smoke error: {exc}", file=sys.stderr)
        return 1
    print(f"brain artifact smoke passed: {args.artifact_dir}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
