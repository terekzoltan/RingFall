"""Load and validate the local B3-A/B3-B JSON model policy fixture."""

from __future__ import annotations

import json
from dataclasses import dataclass
from pathlib import Path
from typing import Any


class ModelPolicyError(ValueError):
    """Raised when a model policy fixture is missing or invalid."""


SUPPORTED_STEP_ONE_RUN_MODES = ("disabled", "mock", "manual")


@dataclass(frozen=True)
class ModelPolicy:
    version: str
    default_mode: str
    available_modes: tuple[str, ...]
    models: tuple[str, ...]
    lanes: tuple[str, ...]


def load_model_policy(path: Path, requested_mode: str) -> ModelPolicy:
    data = _read_policy(path)
    return _validate_policy(data, requested_mode)


def _read_policy(path: Path) -> Any:
    try:
        with path.open("r", encoding="utf-8") as handle:
            return json.load(handle)
    except FileNotFoundError as exc:
        raise ModelPolicyError(f"policy file not found: {path}") from exc
    except json.JSONDecodeError as exc:
        raise ModelPolicyError(f"policy JSON is invalid at line {exc.lineno}, column {exc.colno}") from exc
    except OSError as exc:
        raise ModelPolicyError(f"policy file cannot be read: {path}") from exc


def _validate_policy(data: Any, requested_mode: str) -> ModelPolicy:
    if not isinstance(data, dict):
        raise ModelPolicyError("policy root must be an object")

    version = data.get("version")
    if not isinstance(version, str) or not version.strip():
        raise ModelPolicyError("policy version must be a non-empty string")

    run_modes = data.get("run_modes")
    if not isinstance(run_modes, dict):
        raise ModelPolicyError("run_modes must be an object")


    available = run_modes.get("available")
    if not isinstance(available, list) or not available:
        raise ModelPolicyError("run_modes.available must be a non-empty list")
    if not all(isinstance(mode, str) and mode.strip() for mode in available):
        raise ModelPolicyError("run_modes.available entries must be non-empty strings")

    available_modes = tuple(available)
    unsupported_modes = [mode for mode in available_modes if mode not in SUPPORTED_STEP_ONE_RUN_MODES]
    if unsupported_modes:
        unsupported = ", ".join(sorted(unsupported_modes))
        raise ModelPolicyError(f"run_modes.available includes unsupported Step 1 mode(s): {unsupported}")

    default_mode = run_modes.get("default")
    if not isinstance(default_mode, str) or default_mode not in available_modes:
        raise ModelPolicyError("run_modes.default must be one of run_modes.available")
    if requested_mode not in available_modes:
        raise ModelPolicyError(f"unsupported mode for policy: {requested_mode}")

    models = data.get("models")
    if not isinstance(models, dict) or not models:
        raise ModelPolicyError("models must be a non-empty object")
    for model_name, model_value in models.items():
        if not isinstance(model_name, str) or not model_name.strip():
            raise ModelPolicyError("model names must be non-empty strings")
        if not isinstance(model_value, dict):
            raise ModelPolicyError(f"model {model_name} must be an object")

    lanes = data.get("lanes")
    if not isinstance(lanes, dict) or not lanes:
        raise ModelPolicyError("lanes must be a non-empty object")
    for lane_name, lane_value in lanes.items():
        if not isinstance(lane_name, str) or not lane_name.strip():
            raise ModelPolicyError("lane names must be non-empty strings")
        if not isinstance(lane_value, dict):
            raise ModelPolicyError(f"lane {lane_name} must be an object")
        _validate_lane_model_refs(lane_name, lane_value, models)

    return ModelPolicy(
        version=version,
        default_mode=default_mode,
        available_modes=available_modes,
        models=tuple(sorted(models)),
        lanes=tuple(sorted(lanes)),
    )


def _validate_lane_model_refs(lane_name: str, lane: dict[str, Any], models: dict[str, Any]) -> None:
    for field in ("primary", "fallback", "helper_only"):
        if field not in lane:
            continue
        refs = lane[field]
        if not isinstance(refs, list):
            raise ModelPolicyError(f"lane {lane_name}.{field} must be a list")
        for ref in refs:
            if not isinstance(ref, str) or not ref.strip():
                raise ModelPolicyError(f"lane {lane_name}.{field} entries must be non-empty strings")
            if ref not in models:
                raise ModelPolicyError(f"lane {lane_name}.{field} references unknown model: {ref}")
