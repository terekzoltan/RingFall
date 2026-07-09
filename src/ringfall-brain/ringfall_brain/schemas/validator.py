"""Local JSON Schema validation helper; schema ownership remains in ringfall-contracts."""

from __future__ import annotations

import json
from pathlib import Path
from typing import Any

from jsonschema import Draft202012Validator
from jsonschema.exceptions import SchemaError


class BrainValidationError(ValueError):
    """Raised when local candidate packet validation fails."""


def validate_packet_json(raw_packet: str, schema_path: Path) -> dict[str, Any]:
    schema = _load_schema(schema_path)
    packet = _parse_packet(raw_packet)
    if not isinstance(packet, dict):
        raise BrainValidationError("candidate packet JSON must parse to an object")

    try:
        validator = Draft202012Validator(schema)
        errors = sorted(validator.iter_errors(packet), key=lambda error: list(error.path))
    except SchemaError as exc:
        raise BrainValidationError(f"schema is invalid: {exc.message}") from exc

    if errors:
        raise BrainValidationError(f"candidate packet failed schema validation: {errors[0].message}")
    return packet


def _load_schema(schema_path: Path) -> dict[str, Any]:
    try:
        with schema_path.open("r", encoding="utf-8") as handle:
            schema = json.load(handle)
    except FileNotFoundError as exc:
        raise BrainValidationError(f"schema file not found: {schema_path}") from exc
    except json.JSONDecodeError as exc:
        raise BrainValidationError(f"schema JSON is invalid at line {exc.lineno}, column {exc.colno}") from exc
    except OSError as exc:
        raise BrainValidationError(f"schema file cannot be read: {schema_path}") from exc

    if not isinstance(schema, dict):
        raise BrainValidationError("schema JSON must parse to an object")
    try:
        Draft202012Validator.check_schema(schema)
    except SchemaError as exc:
        raise BrainValidationError(f"schema is invalid: {exc.message}") from exc
    return schema


def _parse_packet(raw_packet: str) -> Any:
    try:
        return json.loads(raw_packet)
    except json.JSONDecodeError as exc:
        raise BrainValidationError(f"candidate packet JSON is invalid at line {exc.lineno}, column {exc.colno}") from exc
