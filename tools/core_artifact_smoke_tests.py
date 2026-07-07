#!/usr/bin/env python3
"""Regression tests for tools/core_artifact_smoke.py."""

from __future__ import annotations

import json
import shutil
import sys
import tempfile
from contextlib import redirect_stderr, redirect_stdout
from io import StringIO
from pathlib import Path

import core_artifact_smoke


ROOT = Path(__file__).resolve().parents[1]
FIXTURE = ROOT / "tools" / "fixtures" / "k2h-core-artifact-bundle"


def copy_fixture() -> tempfile.TemporaryDirectory[str]:
    temp = tempfile.TemporaryDirectory(prefix="ringfall-core-smoke-")
    shutil.copytree(FIXTURE, Path(temp.name), dirs_exist_ok=True)
    return temp


def load_json(path: Path) -> object:
    with path.open("r", encoding="utf-8") as handle:
        return json.load(handle)


def write_json(path: Path, value: object) -> None:
    with path.open("w", encoding="utf-8") as handle:
        json.dump(value, handle, indent=2)
        handle.write("\n")


def write_text(path: Path, value: str) -> None:
    with path.open("w", encoding="utf-8") as handle:
        handle.write(value)


def expect_failure(bundle: Path, expected: str) -> None:
    try:
        core_artifact_smoke.validate_bundle(bundle)
    except core_artifact_smoke.SmokeError as exc:
        if expected not in str(exc):
            raise AssertionError(f"expected {expected!r} in {exc!r}") from exc
        return
    raise AssertionError(f"expected smoke validation failure containing {expected!r}")


def test_valid_fixture_passes() -> None:
    core_artifact_smoke.validate_bundle(FIXTURE)


def test_missing_manifest_fails() -> None:
    with copy_fixture() as temp:
        bundle = Path(temp)
        (bundle / "manifest.json").unlink()
        expect_failure(bundle, "missing required artifact: manifest.json")


def test_invalid_state_diff_fails_schema_validation() -> None:
    with copy_fixture() as temp:
        bundle = Path(temp)
        state_diff = load_json(bundle / "state-diffs" / "state-diff-t000.json")
        assert isinstance(state_diff, dict)
        state_diff.pop("diff_id")
        write_json(bundle / "state-diffs" / "state-diff-t000.json", state_diff)
        expect_failure(bundle, "failed schema")


def test_invalid_created_at_utc_format_fails() -> None:
    with copy_fixture() as temp:
        bundle = Path(temp)
        manifest = load_json(bundle / "manifest.json")
        assert isinstance(manifest, dict)
        manifest["created_at_utc"] = "not-a-date"
        write_json(bundle / "manifest.json", manifest)
        expect_failure(bundle, "created_at_utc")


def test_created_at_utc_space_separator_fails() -> None:
    with copy_fixture() as temp:
        bundle = Path(temp)
        manifest = load_json(bundle / "manifest.json")
        assert isinstance(manifest, dict)
        manifest["created_at_utc"] = "2026-07-07 00:00:00Z"
        write_json(bundle / "manifest.json", manifest)
        expect_failure(bundle, "created_at_utc")


def test_created_at_utc_missing_timezone_fails() -> None:
    with copy_fixture() as temp:
        bundle = Path(temp)
        manifest = load_json(bundle / "manifest.json")
        assert isinstance(manifest, dict)
        manifest["created_at_utc"] = "2026-07-07T00:00:00"
        write_json(bundle / "manifest.json", manifest)
        expect_failure(bundle, "created_at_utc")


def test_created_at_utc_impossible_month_fails() -> None:
    with copy_fixture() as temp:
        bundle = Path(temp)
        manifest = load_json(bundle / "manifest.json")
        assert isinstance(manifest, dict)
        manifest["created_at_utc"] = "2026-13-07T00:00:00Z"
        write_json(bundle / "manifest.json", manifest)
        expect_failure(bundle, "created_at_utc")


def test_created_at_utc_impossible_hour_fails() -> None:
    with copy_fixture() as temp:
        bundle = Path(temp)
        manifest = load_json(bundle / "manifest.json")
        assert isinstance(manifest, dict)
        manifest["created_at_utc"] = "2026-07-07T25:00:00Z"
        write_json(bundle / "manifest.json", manifest)
        expect_failure(bundle, "created_at_utc")


def test_created_at_utc_offset_hour_too_high_fails() -> None:
    with copy_fixture() as temp:
        bundle = Path(temp)
        manifest = load_json(bundle / "manifest.json")
        assert isinstance(manifest, dict)
        manifest["created_at_utc"] = "2026-07-07T00:00:00+24:00"
        write_json(bundle / "manifest.json", manifest)
        expect_failure(bundle, "created_at_utc")


def test_created_at_utc_offset_minute_too_high_fails() -> None:
    with copy_fixture() as temp:
        bundle = Path(temp)
        manifest = load_json(bundle / "manifest.json")
        assert isinstance(manifest, dict)
        manifest["created_at_utc"] = "2026-07-07T00:00:00+00:60"
        write_json(bundle / "manifest.json", manifest)
        expect_failure(bundle, "created_at_utc")


def test_created_at_utc_offset_components_too_high_fail() -> None:
    with copy_fixture() as temp:
        bundle = Path(temp)
        manifest = load_json(bundle / "manifest.json")
        assert isinstance(manifest, dict)
        manifest["created_at_utc"] = "2026-07-07T00:00:00+99:99"
        write_json(bundle / "manifest.json", manifest)
        expect_failure(bundle, "created_at_utc")


def test_created_at_utc_fractional_offset_passes() -> None:
    with copy_fixture() as temp:
        bundle = Path(temp)
        manifest = load_json(bundle / "manifest.json")
        assert isinstance(manifest, dict)
        manifest["created_at_utc"] = "2026-07-07T00:00:00.123+00:00"
        write_json(bundle / "manifest.json", manifest)
        core_artifact_smoke.validate_bundle(bundle)


def test_created_at_utc_positive_offset_boundary_passes() -> None:
    with copy_fixture() as temp:
        bundle = Path(temp)
        manifest = load_json(bundle / "manifest.json")
        assert isinstance(manifest, dict)
        manifest["created_at_utc"] = "2026-07-07T00:00:00+23:59"
        write_json(bundle / "manifest.json", manifest)
        core_artifact_smoke.validate_bundle(bundle)


def test_created_at_utc_negative_offset_boundary_passes() -> None:
    with copy_fixture() as temp:
        bundle = Path(temp)
        manifest = load_json(bundle / "manifest.json")
        assert isinstance(manifest, dict)
        manifest["created_at_utc"] = "2026-07-07T00:00:00-23:59"
        write_json(bundle / "manifest.json", manifest)
        core_artifact_smoke.validate_bundle(bundle)


def test_duplicate_allowed_ref_fails() -> None:
    with copy_fixture() as temp:
        bundle = Path(temp)
        manifest = load_json(bundle / "manifest.json")
        assert isinstance(manifest, dict)
        manifest["tracks"][0]["artifact_refs"].append(
            {
                "ref_id": "duplicate-state-diff",
                "ref_type": "state_diff",
                "artifact_uri": "state-diffs/state-diff-t000.json",
            }
        )
        write_json(bundle / "manifest.json", manifest)
        expect_failure(bundle, "artifact_refs must contain exactly")


def test_wrong_contained_uri_pairing_fails() -> None:
    with copy_fixture() as temp:
        bundle = Path(temp)
        manifest = load_json(bundle / "manifest.json")
        assert isinstance(manifest, dict)
        manifest["tracks"][0]["artifact_refs"][1]["artifact_uri"] = "manifest.json"
        write_json(bundle / "manifest.json", manifest)
        expect_failure(bundle, "state_diff must point to state-diffs/state-diff-t000.json")


def test_wrong_run_manifest_uri_pairing_fails() -> None:
    with copy_fixture() as temp:
        bundle = Path(temp)
        manifest = load_json(bundle / "manifest.json")
        assert isinstance(manifest, dict)
        manifest["tracks"][0]["artifact_refs"][0]["artifact_uri"] = "state-diffs/state-diff-t000.json"
        write_json(bundle / "manifest.json", manifest)
        expect_failure(bundle, "run_manifest must point to manifest.json")


def test_backslash_canonical_uri_fails_exact_pairing() -> None:
    with copy_fixture() as temp:
        bundle = Path(temp)
        manifest = load_json(bundle / "manifest.json")
        assert isinstance(manifest, dict)
        manifest["tracks"][0]["artifact_refs"][1]["artifact_uri"] = "state-diffs\\state-diff-t000.json"
        write_json(bundle / "manifest.json", manifest)
        expect_failure(bundle, "state_diff must point to state-diffs/state-diff-t000.json")


def test_absolute_artifact_uri_fails() -> None:
    with copy_fixture() as temp:
        bundle = Path(temp)
        manifest = load_json(bundle / "manifest.json")
        assert isinstance(manifest, dict)
        manifest["tracks"][0]["artifact_refs"][1]["artifact_uri"] = "C:/ringfall/state-diff-t000.json"
        write_json(bundle / "manifest.json", manifest)
        expect_failure(bundle, "artifact_uri must be relative")


def test_empty_artifact_uri_fails() -> None:
    with copy_fixture() as temp:
        bundle = Path(temp)
        manifest = load_json(bundle / "manifest.json")
        assert isinstance(manifest, dict)
        manifest["tracks"][0]["artifact_refs"][1]["artifact_uri"] = ""
        write_json(bundle / "manifest.json", manifest)
        expect_failure(bundle, "should be non-empty")


def test_missing_artifact_uri_fails() -> None:
    with copy_fixture() as temp:
        bundle = Path(temp)
        manifest = load_json(bundle / "manifest.json")
        assert isinstance(manifest, dict)
        del manifest["tracks"][0]["artifact_refs"][1]["artifact_uri"]
        write_json(bundle / "manifest.json", manifest)
        expect_failure(bundle, "state_diff requires artifact_uri")


def test_nonexistent_artifact_target_fails() -> None:
    with copy_fixture() as temp:
        bundle = Path(temp)
        (bundle / "state-diffs" / "state-diff-t000.json").unlink()
        expect_failure(bundle, "missing required artifact: state-diffs/state-diff-t000.json")


def test_escaping_artifact_uri_fails() -> None:
    with copy_fixture() as temp:
        bundle = Path(temp)
        manifest = load_json(bundle / "manifest.json")
        assert isinstance(manifest, dict)
        manifest["tracks"][0]["artifact_refs"][1]["artifact_uri"] = "../state-diff-t000.json"
        write_json(bundle / "manifest.json", manifest)
        expect_failure(bundle, "artifact_uri escapes bundle root")


def test_unwanted_internal_artifact_ref_fails() -> None:
    with copy_fixture() as temp:
        bundle = Path(temp)
        manifest = load_json(bundle / "manifest.json")
        assert isinstance(manifest, dict)
        manifest["tracks"][0]["artifact_refs"].append(
            {
                "ref_id": "artifact-event-log-t000",
                "ref_type": "event_log",
                "artifact_uri": "events/event-log.json",
            }
        )
        write_json(bundle / "manifest.json", manifest)
        expect_failure(bundle, "artifact_refs must contain exactly")


def test_invalid_snapshot_json_fails() -> None:
    with copy_fixture() as temp:
        bundle = Path(temp)
        write_text(bundle / "snapshots" / "initial-world-state.json", "{")
        expect_failure(bundle, "invalid JSON")


def test_invalid_event_log_json_fails() -> None:
    with copy_fixture() as temp:
        bundle = Path(temp)
        write_text(bundle / "events" / "event-log.json", "{")
        expect_failure(bundle, "invalid JSON")


def test_empty_events_fails() -> None:
    with copy_fixture() as temp:
        bundle = Path(temp)
        event_log = load_json(bundle / "events" / "event-log.json")
        assert isinstance(event_log, dict)
        event_log["events"] = []
        write_json(bundle / "events" / "event-log.json", event_log)
        expect_failure(bundle, "events must not be empty")


def test_non_string_event_message_fails() -> None:
    with copy_fixture() as temp:
        bundle = Path(temp)
        event_log = load_json(bundle / "events" / "event-log.json")
        assert isinstance(event_log, dict)
        event_log["events"][0]["message"] = 41
        write_json(bundle / "events" / "event-log.json", event_log)
        expect_failure(bundle, "event 0 message must be a string")


def test_later_event_hidden_leak_fails() -> None:
    with copy_fixture() as temp:
        bundle = Path(temp)
        event_log = load_json(bundle / "events" / "event-log.json")
        assert isinstance(event_log, dict)
        event_log["events"].append(
            {
                "event_type": "AsterHeatAlarm",
                "event_id": "event-t000-hidden-leak-check",
                "tick": 0,
                "sector_id": "Aster",
                "system_id": "R2",
                "severity": "alarm",
                "message": "Hidden thermal debt reached 0.46.",
                "deterministic_seed": 42,
            }
        )
        write_json(bundle / "events" / "event-log.json", event_log)
        expect_failure(bundle, "event 1 message leaks hidden thermal debt value 0.46")


def test_missing_hidden_diff_fails() -> None:
    with copy_fixture() as temp:
        bundle = Path(temp)
        state_diff = load_json(bundle / "state-diffs" / "state-diff-t000.json")
        assert isinstance(state_diff, dict)
        state_diff["changes"] = [change for change in state_diff["changes"] if change["visibility"] != "hidden"]
        write_json(bundle / "state-diffs" / "state-diff-t000.json", state_diff)
        expect_failure(bundle, "expected hidden state-diff change")


def call_main(bundle: Path) -> tuple[int, str, str]:
    original_argv = sys.argv
    stdout = StringIO()
    stderr = StringIO()
    try:
        sys.argv = ["core_artifact_smoke.py", str(bundle)]
        with redirect_stdout(stdout), redirect_stderr(stderr):
            code = core_artifact_smoke.main()
    finally:
        sys.argv = original_argv
    return code, stdout.getvalue(), stderr.getvalue()


def test_main_success_returns_zero_and_prints_success() -> None:
    code, stdout, stderr = call_main(FIXTURE)
    assert code == 0
    assert "core artifact smoke passed" in stdout
    assert stderr == ""


def test_main_failure_returns_one_and_prints_error_prefix() -> None:
    with copy_fixture() as temp:
        bundle = Path(temp)
        (bundle / "manifest.json").unlink()
        code, stdout, stderr = call_main(bundle)
        assert code == 1
        assert stdout == ""
        assert "core_artifact_smoke error:" in stderr


def run() -> None:
    tests = [
        test_valid_fixture_passes,
        test_missing_manifest_fails,
        test_invalid_state_diff_fails_schema_validation,
        test_invalid_created_at_utc_format_fails,
        test_created_at_utc_space_separator_fails,
        test_created_at_utc_missing_timezone_fails,
        test_created_at_utc_impossible_month_fails,
        test_created_at_utc_impossible_hour_fails,
        test_created_at_utc_offset_hour_too_high_fails,
        test_created_at_utc_offset_minute_too_high_fails,
        test_created_at_utc_offset_components_too_high_fail,
        test_created_at_utc_fractional_offset_passes,
        test_created_at_utc_positive_offset_boundary_passes,
        test_created_at_utc_negative_offset_boundary_passes,
        test_duplicate_allowed_ref_fails,
        test_wrong_contained_uri_pairing_fails,
        test_wrong_run_manifest_uri_pairing_fails,
        test_backslash_canonical_uri_fails_exact_pairing,
        test_absolute_artifact_uri_fails,
        test_empty_artifact_uri_fails,
        test_missing_artifact_uri_fails,
        test_nonexistent_artifact_target_fails,
        test_escaping_artifact_uri_fails,
        test_unwanted_internal_artifact_ref_fails,
        test_invalid_snapshot_json_fails,
        test_invalid_event_log_json_fails,
        test_empty_events_fails,
        test_non_string_event_message_fails,
        test_later_event_hidden_leak_fails,
        test_missing_hidden_diff_fails,
        test_main_success_returns_zero_and_prints_success,
        test_main_failure_returns_one_and_prints_error_prefix,
    ]
    for test in tests:
        test()
        print(f"PASS {test.__name__}")
    print(f"core_artifact_smoke_tests: {len(tests)} passed")


if __name__ == "__main__":
    run()
