#!/usr/bin/env python3
"""Validate Ringfall W1 schemas and manifest-backed contract fixtures."""

from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path
from typing import Any, Callable

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
SCHEMA_ROOT = ROOT / "src" / "ringfall-contracts" / "schemas"
MANIFEST_PATH = ROOT / "src" / "ringfall-contracts" / "examples" / "manifest.json"

ALLOWED_REF_TYPES = {
    "action_trace",
    "claim_record",
    "cognition_trace",
    "context",
    "cost_event",
    "eval_summary",
    "packet",
    "prompt",
    "raw_output",
    "run_manifest",
    "schema",
    "state_diff",
}

REFERENCE_FIELD_NAMES = {
    "artifact_ref",
    "artifact_refs",
    "cognition_trace_ref",
    "context_ref",
    "cost_event_ref",
    "cost_summary_ref",
    "evidence_ref",
    "evidence_refs",
    "events_created",
    "finding_ref",
    "finding_refs",
    "memory_update_ref",
    "memory_update_refs",
    "parsed_packet_ref",
    "prompt_ref",
    "raw_output_ref",
    "replay_manifest_ref",
    "request_ref",
    "schema_ref",
    "source_ref",
    "source_refs",
    "state_diff_ref",
    "state_diff_refs",
    "system_ref",
    "system_refs",
    "trace_ref",
    "trace_refs",
    "visibility_ref",
}


class CheckError(Exception):
    pass


def load_json(path: Path) -> Any:
    with path.open("r", encoding="utf-8") as handle:
        return json.load(handle)


def rel(path: Path) -> str:
    return path.relative_to(ROOT).as_posix()


def schema_paths() -> list[Path]:
    return sorted(SCHEMA_ROOT.glob("**/*.schema.json"), key=lambda path: rel(path))


def load_manifest() -> list[dict[str, Any]]:
    manifest = load_json(MANIFEST_PATH)
    fixtures = manifest.get("fixtures")
    if not isinstance(fixtures, list):
        raise CheckError("manifest.json must contain a fixtures array")
    return fixtures


def check_schema_files() -> dict[str, dict[str, Any]]:
    schemas: dict[str, dict[str, Any]] = {}
    for path in schema_paths():
        schema = load_json(path)
        Draft202012Validator.check_schema(schema)
        schemas[rel(path)] = schema
    if len(schemas) != 16:
        raise CheckError(f"expected 16 schema files, found {len(schemas)}")
    return schemas


def manifest_path(value: str) -> Path:
    path = (ROOT / value).resolve()
    try:
        path.relative_to(ROOT)
    except ValueError as exc:
        raise CheckError(f"manifest path escapes repository root: {value}") from exc
    return path


def collect_ref_type_errors(
    value: Any,
    where: str = "$",
    errors: list[str] | None = None,
    in_reference_field: bool = False,
) -> list[str]:
    if errors is None:
        errors = []
    if isinstance(value, dict):
        if in_reference_field and "ref_type" in value and value["ref_type"] not in ALLOWED_REF_TYPES:
            errors.append(f"{where}.ref_type={value['ref_type']!r}")
        for key, nested in value.items():
            collect_ref_type_errors(
                nested,
                f"{where}.{key}",
                errors,
                in_reference_field or key in REFERENCE_FIELD_NAMES,
            )
    elif isinstance(value, list):
        for index, nested in enumerate(value):
            collect_ref_type_errors(nested, f"{where}[{index}]", errors, in_reference_field)
    return errors


def semantic_duplicate_track_ids(data: Any) -> bool:
    if "tracks" not in data:
        return False
    track_ids = [track.get("track_id") for track in data.get("tracks", []) if isinstance(track, dict)]
    return len(track_ids) != len(set(track_ids))


def semantic_action_validation_status_mismatch(data: Any) -> bool:
    if data.get("trace_type") != "ActionTrace":
        return False
    statuses = [item.get("status") for item in data.get("validation", {}).values() if isinstance(item, dict)]
    execution_status = data.get("execution_status")
    if execution_status in {"success", "partial_success"} and "fail" in statuses:
        return True
    if execution_status in {"rejected", "failed"} and statuses and all(status == "pass" for status in statuses):
        return True
    return False


def semantic_hidden_leak_detected(data: Any) -> bool:
    if data.get("memory_type") != "MemoryUpdate":
        return False
    return data.get("validation", {}).get("hidden_leak_detected") is True


def semantic_rumor_fact_contamination(data: Any) -> bool:
    if data.get("memory_type") != "MemoryUpdate":
        return False
    return data.get("validation", {}).get("rumor_fact_contamination_detected") is True


def semantic_source_ref_vocabulary(data: Any) -> bool:
    return bool(collect_ref_type_errors(data))


def semantic_cost_free_zero_consistency(data: Any) -> bool:
    if data.get("event_type") != "CostEvent":
        return False
    if data.get("cost_estimate_status") != "free_or_mock_zero":
        return False
    return data.get("estimated_cost_usd") != 0 or data.get("free_model_used") is not True


def semantic_cost_fallback_reason_required(data: Any) -> bool:
    if data.get("event_type") != "CostEvent":
        return False
    fallback_used = data.get("fallback_used") is True or data.get("fallback_count", 0) > 0
    return fallback_used and not data.get("fallback_reason")


def semantic_eval_summary_count_mismatch(data: Any) -> bool:
    if data.get("summary_type") != "EvalSummary":
        return False
    summary = data.get("summary", {})
    if not isinstance(summary, dict):
        return True
    expected_total = summary.get("pass", 0) + summary.get("warn", 0) + summary.get("fail", 0)
    if summary.get("total") != expected_total:
        return True
    level_total = 0
    for level in data.get("by_level", []):
        level_total += level.get("pass", 0) + level.get("warn", 0) + level.get("fail", 0)
    return level_total != expected_total


def semantic_eval_hold_requires_gate_reasons(data: Any) -> bool:
    if data.get("summary_type") != "EvalSummary":
        return False
    return data.get("gate") in {"hold", "invalid_run"} and not data.get("gate_reasons")


def semantic_eval_pass_with_hard_failures(data: Any) -> bool:
    if data.get("summary_type") != "EvalSummary":
        return False
    return data.get("gate") in {"pass", "pass_with_warnings"} and bool(data.get("hard_gate_failures"))


SEMANTIC_CHECKS: dict[str, Callable[[Any], bool]] = {
    "duplicate_track_ids": semantic_duplicate_track_ids,
    "action_validation_status_mismatch": semantic_action_validation_status_mismatch,
    "hidden_leak_detected": semantic_hidden_leak_detected,
    "rumor_fact_contamination": semantic_rumor_fact_contamination,
    "source_ref_vocabulary": semantic_source_ref_vocabulary,
    "cost_free_zero_consistency": semantic_cost_free_zero_consistency,
    "cost_fallback_reason_required": semantic_cost_fallback_reason_required,
    "eval_summary_count_mismatch": semantic_eval_summary_count_mismatch,
    "eval_hold_requires_gate_reasons": semantic_eval_hold_requires_gate_reasons,
    "eval_pass_with_hard_failures": semantic_eval_pass_with_hard_failures,
}


def check_manifest(fixtures: list[dict[str, Any]], schemas: dict[str, dict[str, Any]]) -> None:
    valid_schema_counts = {schema: 0 for schema in schemas}
    seen_fixtures: set[str] = set()
    for entry in fixtures:
        fixture = entry.get("fixture")
        schema = entry.get("schema")
        expected = entry.get("expected")
        layer = entry.get("validation_layer")
        reason_code = entry.get("reason_code")
        if not isinstance(fixture, str) or not isinstance(schema, str):
            raise CheckError("manifest entries require string fixture and schema paths")
        if fixture in seen_fixtures:
            raise CheckError(f"duplicate manifest fixture entry: {fixture}")
        seen_fixtures.add(fixture)
        if not manifest_path(fixture).is_file():
            raise CheckError(f"manifest fixture path does not exist: {fixture}")
        if schema not in schemas:
            raise CheckError(f"manifest schema path does not exist: {schema}")
        if expected not in {"valid", "invalid"}:
            raise CheckError(f"invalid expected value for {fixture}: {expected}")
        if layer not in {"json_schema", "semantic"}:
            raise CheckError(f"invalid validation_layer for {fixture}: {layer}")
        if expected == "valid":
            valid_schema_counts[schema] += 1
            if reason_code is not None:
                raise CheckError(f"valid fixture must use null reason_code: {fixture}")
        else:
            if not isinstance(reason_code, str) or not reason_code:
                raise CheckError(f"invalid fixture needs a reason_code: {fixture}")
            if layer == "semantic" and reason_code not in SEMANTIC_CHECKS:
                raise CheckError(f"unknown semantic reason_code for {fixture}: {reason_code}")
    missing_valid = [schema for schema, count in valid_schema_counts.items() if count != 1]
    if missing_valid:
        details = ", ".join(f"{schema}={valid_schema_counts[schema]}" for schema in missing_valid)
        raise CheckError(f"each schema must have exactly one valid fixture: {details}")


def validate_entry(entry: dict[str, Any], schemas: dict[str, dict[str, Any]]) -> tuple[bool, str]:
    fixture_path = manifest_path(entry["fixture"])
    data = load_json(fixture_path)
    schema = schemas[entry["schema"]]
    errors = sorted(Draft202012Validator(schema).iter_errors(data), key=lambda error: list(error.path))
    schema_failed = bool(errors)
    if entry["expected"] == "valid":
        if schema_failed:
            return False, f"valid fixture failed JSON Schema: {errors[0].message}"
        semantic_failures = [code for code, check in SEMANTIC_CHECKS.items() if check(data)]
        if semantic_failures:
            return False, f"valid fixture failed semantic checks: {', '.join(sorted(semantic_failures))}"
        return True, "valid fixture passed"
    if entry["validation_layer"] == "json_schema":
        if schema_failed:
            return True, f"invalid fixture failed JSON Schema as expected: {entry['reason_code']}"
        return False, f"invalid fixture unexpectedly passed JSON Schema: {entry['reason_code']}"
    if schema_failed:
        return False, f"semantic invalid fixture failed JSON Schema before semantic check: {errors[0].message}"
    reason_code = entry["reason_code"]
    triggered_codes = sorted(code for code, check in SEMANTIC_CHECKS.items() if check(data))
    if reason_code not in triggered_codes:
        triggered = ", ".join(triggered_codes) if triggered_codes else "none"
        return False, f"invalid fixture did not trigger semantic check: {reason_code}; triggered={triggered}"
    if len(triggered_codes) != 1:
        return False, f"semantic fixture triggered extra reason codes: {', '.join(triggered_codes)}"
    return True, f"semantic reason isolation passed: {reason_code}"


def selected_entries(fixtures: list[dict[str, Any]], args: argparse.Namespace) -> list[dict[str, Any]]:
    if args.valid_only:
        return [entry for entry in fixtures if entry["expected"] == "valid"]
    if args.invalid_only:
        return [entry for entry in fixtures if entry["expected"] == "invalid"]
    return fixtures


def main() -> int:
    parser = argparse.ArgumentParser(description="Validate Ringfall schemas and contract examples.")
    parser.add_argument("--schemas-only", action="store_true", help="Validate schema files only.")
    parser.add_argument("--valid-only", action="store_true", help="Validate only manifest entries expected to pass.")
    parser.add_argument("--invalid-only", action="store_true", help="Validate only manifest entries expected to fail.")
    parser.add_argument("--list", action="store_true", help="List manifest entries without validating fixtures.")
    args = parser.parse_args()
    if args.valid_only and args.invalid_only:
        print("--valid-only and --invalid-only are mutually exclusive", file=sys.stderr)
        return 2
    try:
        schemas = check_schema_files()
        print(f"schemas: {len(schemas)} Draft 2020-12 schema files passed metaschema checks")
        if args.schemas_only:
            return 0
        fixtures = load_manifest()
        check_manifest(fixtures, schemas)
        entries = sorted(selected_entries(fixtures, args), key=lambda item: (item["schema"], item["fixture"], item.get("reason_code") or ""))
        print(f"manifest: {len(fixtures)} entries passed consistency checks")
        if args.list:
            for entry in entries:
                print(f"{entry['expected']} {entry['validation_layer']} {entry['schema']} <- {entry['fixture']} {entry.get('reason_code') or ''}".rstrip())
            return 0
        failures = []
        for entry in entries:
            ok, detail = validate_entry(entry, schemas)
            status = "PASS" if ok else "FAIL"
            print(f"{status} {entry['fixture']} :: {detail}")
            if not ok:
                failures.append(entry["fixture"])
        if failures:
            print(f"validation failed for {len(failures)} fixture(s)", file=sys.stderr)
            return 1
        print(f"fixtures: {len(entries)} selected entries validated successfully")
        return 0
    except (CheckError, OSError, json.JSONDecodeError) as exc:
        print(f"schema_check error: {exc}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    sys.exit(main())
