#!/usr/bin/env python3
"""Validate a generated Ringfall core artifact bundle against public contracts."""

from __future__ import annotations

import argparse
import json
import re
import sys
from datetime import datetime
from pathlib import Path
from typing import Any

try:
    from jsonschema import Draft202012Validator, FormatChecker
except ModuleNotFoundError:  # pragma: no cover - exercised by missing local dependency.
    print(
        "Missing dependency: jsonschema. Install dev tooling with "
        "`python -m pip install -r requirements-dev.txt`.",
        file=sys.stderr,
    )
    sys.exit(2)


ROOT = Path(__file__).resolve().parents[1]
RUN_MANIFEST_SCHEMA = ROOT / "src" / "ringfall-contracts" / "schemas" / "traces" / "run-manifest.schema.json"
STATE_DIFF_SCHEMA = ROOT / "src" / "ringfall-contracts" / "schemas" / "state" / "state-diff.schema.json"

REQUIRED_ARTIFACTS = (
    "manifest.json",
    "snapshots/initial-world-state.json",
    "snapshots/final-world-state.json",
    "events/event-log.json",
    "state-diffs/state-diff-t000.json",
)
PUBLIC_SCHEMA_ARTIFACTS = {
    "manifest.json": RUN_MANIFEST_SCHEMA,
    "state-diffs/state-diff-t000.json": STATE_DIFF_SCHEMA,
}
EXPECTED_MANIFEST_REFS = {
    "run_manifest": "manifest.json",
    "state_diff": "state-diffs/state-diff-t000.json",
}
FORMAT_CHECKER = FormatChecker()
RFC3339_DATE_TIME = re.compile(
    r"^(?P<year>\d{4})-(?P<month>\d{2})-(?P<day>\d{2})"
    r"T(?P<hour>\d{2}):(?P<minute>\d{2}):(?P<second>\d{2})"
    r"(?P<fraction>\.\d+)?(?P<timezone>Z|[+-]\d{2}:\d{2})$"
)


@FORMAT_CHECKER.checks("date-time")
def is_date_time(value: object) -> bool:
    if not isinstance(value, str):
        return True
    match = RFC3339_DATE_TIME.fullmatch(value)
    if match is None:
        return False
    try:
        datetime(
            int(match.group("year")),
            int(match.group("month")),
            int(match.group("day")),
            int(match.group("hour")),
            int(match.group("minute")),
            int(match.group("second")),
        )
    except ValueError:
        return False
    timezone = match.group("timezone")
    if timezone != "Z":
        offset_hour = int(timezone[1:3])
        offset_minute = int(timezone[4:6])
        if offset_hour > 23 or offset_minute > 59:
            return False
    return True


class SmokeError(Exception):
    pass


def load_json(path: Path) -> Any:
    try:
        with path.open("r", encoding="utf-8") as handle:
            return json.load(handle)
    except json.JSONDecodeError as exc:
        raise SmokeError(f"{path}: invalid JSON at line {exc.lineno}, column {exc.colno}: {exc.msg}") from exc


def json_pointer(error_path: object) -> str:
    parts = [str(part).replace("~", "~0").replace("/", "~1") for part in error_path]
    return "$" if not parts else "$/" + "/".join(parts)


def validate_schema(artifact_path: Path, schema_path: Path) -> None:
    artifact = load_json(artifact_path)
    schema = load_json(schema_path)
    validator = Draft202012Validator(schema, format_checker=FORMAT_CHECKER)
    errors = sorted(validator.iter_errors(artifact), key=lambda error: list(error.path))
    if errors:
        first = errors[0]
        raise SmokeError(
            f"{artifact_path}: failed schema {schema_path} at {json_pointer(first.path)}: {first.message}"
        )


def require_artifact(bundle_root: Path, relative_path: str) -> Path:
    path = bundle_root / relative_path
    if not path.is_file():
        raise SmokeError(f"missing required artifact: {relative_path}")
    return path


def artifact_refs(manifest: Any) -> list[dict[str, Any]]:
    refs: list[dict[str, Any]] = []
    tracks = manifest.get("tracks", []) if isinstance(manifest, dict) else []
    for track in tracks:
        if isinstance(track, dict):
            refs.extend(ref for ref in track.get("artifact_refs", []) if isinstance(ref, dict))
    return refs


def validate_manifest_refs(bundle_root: Path, manifest_path: Path) -> None:
    manifest = load_json(manifest_path)
    refs = artifact_refs(manifest)
    if len(refs) != len(EXPECTED_MANIFEST_REFS):
        raise SmokeError(
            f"{manifest_path}: artifact_refs must contain exactly "
            f"{len(EXPECTED_MANIFEST_REFS)} refs"
        )

    refs_by_type: dict[str, dict[str, Any]] = {}
    for ref in refs:
        ref_type = ref.get("ref_type")
        if ref_type not in EXPECTED_MANIFEST_REFS:
            raise SmokeError(f"{manifest_path}: unexpected artifact ref_type: {ref_type!r}")
        if ref_type in refs_by_type:
            raise SmokeError(f"{manifest_path}: duplicate artifact ref_type: {ref_type}")
        refs_by_type[ref_type] = ref

    missing = set(EXPECTED_MANIFEST_REFS) - set(refs_by_type)
    if missing:
        raise SmokeError(f"{manifest_path}: missing artifact ref_type(s): {sorted(missing)}")

    resolved_bundle_root = bundle_root.resolve()
    for ref_type, expected_uri in EXPECTED_MANIFEST_REFS.items():
        ref = refs_by_type[ref_type]
        uri = ref.get("artifact_uri")
        if not isinstance(uri, str) or not uri:
            raise SmokeError(f"{manifest_path}: artifact_ref {ref_type} requires artifact_uri")
        uri_path = Path(uri)
        if uri_path.is_absolute():
            raise SmokeError(f"{manifest_path}: artifact_uri must be relative: {uri}")
        resolved_uri = (bundle_root / uri_path).resolve()
        try:
            resolved_uri.relative_to(resolved_bundle_root)
        except ValueError as exc:
            raise SmokeError(f"{manifest_path}: artifact_uri escapes bundle root: {uri}") from exc
        if not resolved_uri.is_file():
            raise SmokeError(f"{manifest_path}: artifact_uri target does not exist: {uri}")
        if uri != expected_uri:
            raise SmokeError(
                f"{manifest_path}: artifact_ref {ref_type} must point to {expected_uri}, found {uri}"
            )


def validate_internal_smoke(bundle_root: Path) -> None:
    event_log = load_json(bundle_root / "events" / "event-log.json")
    events = event_log.get("events", []) if isinstance(event_log, dict) else []
    if not events:
        raise SmokeError(f"{bundle_root / 'events' / 'event-log.json'}: events must not be empty")
    for index, event in enumerate(events):
        message = event.get("message", "") if isinstance(event, dict) else ""
        if not isinstance(message, str):
            raise SmokeError(f"{bundle_root / 'events' / 'event-log.json'}: event {index} message must be a string")
        for hidden_value in ("0.41", "0.46"):
            if hidden_value in message:
                raise SmokeError(
                    f"{bundle_root / 'events' / 'event-log.json'}: event {index} message leaks hidden thermal debt value {hidden_value}"
                )

    state_diff = load_json(bundle_root / "state-diffs" / "state-diff-t000.json")
    changes = state_diff.get("changes", []) if isinstance(state_diff, dict) else []
    if not any(isinstance(change, dict) and change.get("visibility") == "hidden" for change in changes):
        raise SmokeError(f"{bundle_root / 'state-diffs' / 'state-diff-t000.json'}: expected hidden state-diff change")


def validate_bundle(bundle_root: Path) -> None:
    if not bundle_root.is_dir():
        raise SmokeError(f"bundle directory does not exist: {bundle_root}")

    for relative_path in REQUIRED_ARTIFACTS:
        require_artifact(bundle_root, relative_path)
        load_json(bundle_root / relative_path)

    for relative_path, schema_path in PUBLIC_SCHEMA_ARTIFACTS.items():
        validate_schema(bundle_root / relative_path, schema_path)

    validate_manifest_refs(bundle_root, bundle_root / "manifest.json")
    validate_internal_smoke(bundle_root)


def main() -> int:
    parser = argparse.ArgumentParser(description="Validate a Ringfall core artifact bundle smoke fixture.")
    parser.add_argument("bundle", help="Path to the generated core artifact bundle directory.")
    args = parser.parse_args()

    try:
        validate_bundle(Path(args.bundle))
    except SmokeError as exc:
        print(f"core_artifact_smoke error: {exc}", file=sys.stderr)
        return 1
    print(f"core artifact smoke passed: {args.bundle}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
