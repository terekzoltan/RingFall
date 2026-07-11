#!/usr/bin/env python3
"""Regression tests for tools/brain_artifact_smoke.py."""

from __future__ import annotations

import json
import sys
import tempfile
from contextlib import redirect_stderr, redirect_stdout
from io import StringIO
from pathlib import Path
from unittest.mock import patch

ROOT = Path(__file__).resolve().parents[1]
BRAIN_ROOT = ROOT / "src" / "ringfall-brain"
sys.path.insert(0, str(BRAIN_ROOT))

import brain_artifact_smoke
from ringfall_brain.providers.mock_provider import build_avatar_pulse_packet
from ringfall_brain.traces.artifacts import build_mock_cognition_artifacts, write_mock_cognition_artifacts


PACKET_SCHEMA = ROOT / "src" / "ringfall-contracts" / "schemas" / "packets" / "avatar-pulse-packet.schema.json"
COGNITION_TRACE_SCHEMA = ROOT / "src" / "ringfall-contracts" / "schemas" / "traces" / "cognition-trace.schema.json"
COST_EVENT_SCHEMA = ROOT / "src" / "ringfall-contracts" / "schemas" / "traces" / "cost-event.schema.json"


def write_generated_artifacts() -> tempfile.TemporaryDirectory[str]:
    temp = tempfile.TemporaryDirectory(prefix="ringfall-brain-smoke-")
    artifacts = build_mock_cognition_artifacts(
        build_avatar_pulse_packet(),
        PACKET_SCHEMA,
        COGNITION_TRACE_SCHEMA,
        COST_EVENT_SCHEMA,
    )
    write_mock_cognition_artifacts(artifacts, Path(temp.name))
    return temp


def load_json(path: Path) -> object:
    with path.open("r", encoding="utf-8") as handle:
        return json.load(handle)


def write_json(path: Path, value: object) -> None:
    with path.open("w", encoding="utf-8") as handle:
        json.dump(value, handle, indent=2)
        handle.write("\n")


def replace_json_literal(path: Path, original: str, replacement: str) -> None:
    text = path.read_text(encoding="utf-8")
    assert original in text
    path.write_text(text.replace(original, replacement, 1), encoding="utf-8")


def expect_failure(bundle: Path, expected: str) -> None:
    try:
        brain_artifact_smoke.validate_bundle(bundle)
    except brain_artifact_smoke.SmokeError as exc:
        if expected not in str(exc):
            raise AssertionError(f"expected {expected!r} in {exc!r}") from exc
        return
    raise AssertionError(f"expected smoke validation failure containing {expected!r}")


def test_valid_generated_artifacts_pass() -> None:
    with write_generated_artifacts() as temp:
        brain_artifact_smoke.validate_bundle(Path(temp))


def test_missing_required_file_fails() -> None:
    with write_generated_artifacts() as temp:
        bundle = Path(temp)
        (bundle / "cost-event.json").unlink()
        expect_failure(bundle, "missing required artifact")


def test_extra_top_level_file_fails() -> None:
    with write_generated_artifacts() as temp:
        bundle = Path(temp)
        (bundle / "extra.json").write_text("{}\n", encoding="utf-8")
        expect_failure(bundle, "unexpected top-level artifact")


def test_invalid_packet_schema_fails() -> None:
    with write_generated_artifacts() as temp:
        bundle = Path(temp)
        packet = load_json(bundle / "avatar-pulse-packet.json")
        assert isinstance(packet, dict)
        packet["packet_type"] = "WrongPacket"
        write_json(bundle / "avatar-pulse-packet.json", packet)
        expect_failure(bundle, "failed schema")


def test_invalid_trace_schema_fails() -> None:
    with write_generated_artifacts() as temp:
        bundle = Path(temp)
        trace = load_json(bundle / "cognition-trace.json")
        assert isinstance(trace, dict)
        trace.pop("cognition_id")
        write_json(bundle / "cognition-trace.json", trace)
        expect_failure(bundle, "failed schema")


def test_invalid_cost_schema_fails() -> None:
    with write_generated_artifacts() as temp:
        bundle = Path(temp)
        cost = load_json(bundle / "cost-event.json")
        assert isinstance(cost, dict)
        cost["estimated_cost_usd"] = -1
        write_json(bundle / "cost-event.json", cost)
        expect_failure(bundle, "failed schema")


def test_broken_packet_trace_ref_fails() -> None:
    with write_generated_artifacts() as temp:
        bundle = Path(temp)
        trace = load_json(bundle / "cognition-trace.json")
        assert isinstance(trace, dict)
        trace["parsed_packet_ref"]["ref_id"] = "wrong-packet"
        write_json(bundle / "cognition-trace.json", trace)
        expect_failure(bundle, "trace.parsed_packet_ref.ref_id")


def test_broken_trace_cost_ref_fails() -> None:
    with write_generated_artifacts() as temp:
        bundle = Path(temp)
        cost = load_json(bundle / "cost-event.json")
        assert isinstance(cost, dict)
        cost["cognition_trace_ref"]["ref_id"] = "wrong-trace"
        write_json(bundle / "cost-event.json", cost)
        expect_failure(bundle, "cost.cognition_trace_ref.ref_id")


def test_turn_ref_mismatch_fails() -> None:
    with write_generated_artifacts() as temp:
        bundle = Path(temp)
        cost = load_json(bundle / "cost-event.json")
        assert isinstance(cost, dict)
        cost["turn_ref"] = "wrong-turn"
        write_json(bundle / "cost-event.json", cost)
        expect_failure(bundle, "cost.turn_ref")


def test_reference_type_mismatches_fail() -> None:
    cases = [
        ("cognition-trace.json", ("parsed_packet_ref", "ref_type"), "wrong", "trace.parsed_packet_ref.ref_type"),
        ("cognition-trace.json", ("raw_output_ref", "ref_type"), "wrong", "trace.raw_output_ref.ref_type"),
        ("cognition-trace.json", ("cost_event_ref", "ref_type"), "wrong", "trace.cost_event_ref.ref_type"),
        ("cost-event.json", ("cognition_trace_ref", "ref_type"), "wrong", "cost.cognition_trace_ref.ref_type"),
    ]
    for filename, path, value, expected in cases:
        with write_generated_artifacts() as temp:
            bundle = Path(temp)
            artifact = load_json(bundle / filename)
            assert isinstance(artifact, dict)
            artifact[path[0]][path[1]] = value
            write_json(bundle / filename, artifact)
            expect_failure(bundle, expected)


def test_cognition_provenance_mismatches_fail() -> None:
    cases = [
        (("context_ref", "ref_id"), "wrong-context", "trace.context_ref.ref_id"),
        (("context_ref", "ref_type"), "wrong", "trace.context_ref.ref_type"),
        (("prompt_ref", "ref_type"), "wrong", "trace.prompt_ref.ref_type"),
    ]
    for path, value, expected in cases:
        with write_generated_artifacts() as temp:
            bundle = Path(temp)
            trace = load_json(bundle / "cognition-trace.json")
            assert isinstance(trace, dict)
            trace[path[0]][path[1]] = value
            write_json(bundle / "cognition-trace.json", trace)
            expect_failure(bundle, expected)


def test_l1_packet_boundary_mismatches_fail() -> None:
    cases = [
        ("issuer_layer", "L2", "packet.issuer_layer"),
        ("issuer_id", "actor-aster-999", "packet.issuer_id"),
    ]
    for field, value, expected in cases:
        with write_generated_artifacts() as temp:
            bundle = Path(temp)
            packet = load_json(bundle / "avatar-pulse-packet.json")
            assert isinstance(packet, dict)
            packet[field] = value
            write_json(bundle / "avatar-pulse-packet.json", packet)
            expect_failure(bundle, expected)


def test_artifact_uri_basename_only_is_enforced() -> None:
    with write_generated_artifacts() as temp:
        bundle = Path(temp)
        trace = load_json(bundle / "cognition-trace.json")
        assert isinstance(trace, dict)
        trace["parsed_packet_ref"]["artifact_uri"] = "other://namespace/avatar-pulse-packet.json"
        write_json(bundle / "cognition-trace.json", trace)
        brain_artifact_smoke.validate_bundle(bundle)


def test_wrong_artifact_uri_basename_fails() -> None:
    with write_generated_artifacts() as temp:
        bundle = Path(temp)
        trace = load_json(bundle / "cognition-trace.json")
        assert isinstance(trace, dict)
        trace["parsed_packet_ref"]["artifact_uri"] = "fixtures://packets/wrong.json"
        write_json(bundle / "cognition-trace.json", trace)
        expect_failure(bundle, "basename must be avatar-pulse-packet.json")


def test_non_dev_mock_provider_fails() -> None:
    with write_generated_artifacts() as temp:
        bundle = Path(temp)
        trace = load_json(bundle / "cognition-trace.json")
        assert isinstance(trace, dict)
        trace["model_evidence"]["provider"] = "openrouter"
        write_json(bundle / "cognition-trace.json", trace)
        expect_failure(bundle, "trace.model_evidence.provider")


def test_non_zero_mock_cost_fails() -> None:
    with write_generated_artifacts() as temp:
        bundle = Path(temp)
        cost = load_json(bundle / "cost-event.json")
        assert isinstance(cost, dict)
        cost["estimated_cost_usd"] = 0.01
        write_json(bundle / "cost-event.json", cost)
        expect_failure(bundle, "cost.estimated_cost_usd")


def test_retry_and_fallback_mismatches_fail() -> None:
    cases = [
        ("cognition-trace.json", "retry_count", 1, "trace.retry_count"),
        ("cognition-trace.json", "fallback_used", True, "trace.fallback_used"),
        ("cost-event.json", "retry_count", 1, "cost.retry_count"),
        ("cost-event.json", "fallback_count", 1, "cost.fallback_count"),
    ]
    for filename, field, value, expected in cases:
        with write_generated_artifacts() as temp:
            bundle = Path(temp)
            artifact = load_json(bundle / filename)
            assert isinstance(artifact, dict)
            artifact[field] = value
            write_json(bundle / filename, artifact)
            expect_failure(bundle, expected)


def test_direct_world_mutation_key_fails() -> None:
    with write_generated_artifacts() as temp:
        bundle = Path(temp)
        trace = load_json(bundle / "cognition-trace.json")
        assert isinstance(trace, dict)
        trace["model_evidence"]["world_state_patch"] = {"path": "R2"}
        write_json(bundle / "cognition-trace.json", trace)
        expect_failure(bundle, "direct world mutation key")


def test_hidden_truth_text_fails_case_insensitive() -> None:
    with write_generated_artifacts() as temp:
        bundle = Path(temp)
        packet = load_json(bundle / "avatar-pulse-packet.json")
        assert isinstance(packet, dict)
        packet["rationale"] = "Hidden Thermal status is visible."
        write_json(bundle / "avatar-pulse-packet.json", packet)
        expect_failure(bundle, "hidden truth text")


def test_hidden_truth_numeric_string_fails() -> None:
    with write_generated_artifacts() as temp:
        bundle = Path(temp)
        packet = load_json(bundle / "avatar-pulse-packet.json")
        assert isinstance(packet, dict)
        packet["rationale"] = "Observed value 0.46."
        write_json(bundle / "avatar-pulse-packet.json", packet)
        expect_failure(bundle, "hidden truth value")


def test_legitimate_numeric_local_status_values_pass() -> None:
    with write_generated_artifacts() as temp:
        bundle = Path(temp)
        packet = load_json(bundle / "avatar-pulse-packet.json")
        assert isinstance(packet, dict)
        packet["local_status"] = {
            "stress": 0.46,
            "fatigue": 0.41,
            "immediate_risk": "medium",
        }
        write_json(bundle / "avatar-pulse-packet.json", packet)
        brain_artifact_smoke.validate_bundle(bundle)


def test_legitimate_numeric_belief_confidence_passes() -> None:
    with write_generated_artifacts() as temp:
        bundle = Path(temp)
        packet = load_json(bundle / "avatar-pulse-packet.json")
        assert isinstance(packet, dict)
        packet["belief_updates"] = [
            {
                "claim": "Maintenance conditions may be contributing to instability.",
                "confidence": 0.41,
                "source_refs": ["ctx-aster-001"],
            }
        ]
        write_json(bundle / "avatar-pulse-packet.json", packet)
        brain_artifact_smoke.validate_bundle(bundle)


def test_main_non_standard_numeric_constants_return_bounded_errors() -> None:
    for constant in ("NaN", "Infinity", "-Infinity"):
        with write_generated_artifacts() as temp:
            bundle = Path(temp)
            replace_json_literal(
                bundle / "cognition-trace.json",
                '"temperature":0',
                f'"temperature":{constant}',
            )
            code, stdout, stderr = call_main(bundle)
            assert code == 1
            assert stdout == ""
            assert stderr.startswith("brain_artifact_smoke error:")
            assert f"non-standard JSON numeric constant is not allowed: {constant}" in stderr
            assert "Traceback" not in stderr


def test_main_overflowed_exponent_returns_bounded_error() -> None:
    with write_generated_artifacts() as temp:
        bundle = Path(temp)
        replace_json_literal(
            bundle / "cognition-trace.json",
            '"temperature":0',
            '"temperature":1e9999',
        )
        code, stdout, stderr = call_main(bundle)
        assert code == 1
        assert stdout == ""
        assert stderr.startswith("brain_artifact_smoke error:")
        assert "non-finite JSON number is not allowed" in stderr
        assert "Traceback" not in stderr


def test_main_duplicate_keys_return_bounded_errors() -> None:
    cases = [
        (
            "avatar-pulse-packet.json",
            '"actor_id":"actor-aster-001"',
            '"actor_id":"hidden thermal 0.46","actor_id":"actor-aster-001"',
            "'actor_id'",
        ),
        (
            "cognition-trace.json",
            '"ref_id":"ctx-aster-001"',
            '"ref_id":"hidden-context","ref_id":"ctx-aster-001"',
            "'ref_id'",
        ),
    ]
    for filename, original, replacement, expected_key in cases:
        with write_generated_artifacts() as temp:
            bundle = Path(temp)
            replace_json_literal(bundle / filename, original, replacement)
            code, stdout, stderr = call_main(bundle)
            assert code == 1
            assert stdout == ""
            assert stderr.startswith("brain_artifact_smoke error:")
            assert f"duplicate JSON object key is not allowed: {expected_key}" in stderr
            assert "Traceback" not in stderr


def test_main_oversized_integer_or_fallback_returns_bounded_error() -> None:
    with write_generated_artifacts() as temp:
        bundle = Path(temp)
        get_limit = getattr(sys, "get_int_max_str_digits", None)
        digit_limit = get_limit() if get_limit is not None else 0
        if digit_limit > 0:
            replace_json_literal(
                bundle / "cognition-trace.json",
                '"max_output_tokens":1',
                f'"max_output_tokens":{"1" * (digit_limit + 1)}',
            )
            code, stdout, stderr = call_main(bundle)
        else:
            with patch.object(
                brain_artifact_smoke.json,
                "load",
                side_effect=ValueError("deterministic numeric conversion failure"),
            ):
                code, stdout, stderr = call_main(bundle)
        assert code == 1
        assert stdout == ""
        assert stderr.startswith("brain_artifact_smoke error:")
        assert "JSON numeric value cannot be decoded" in stderr
        assert "Traceback" not in stderr


def call_main(bundle: Path) -> tuple[int, str, str]:
    original_argv = sys.argv
    stdout = StringIO()
    stderr = StringIO()
    try:
        sys.argv = ["brain_artifact_smoke.py", str(bundle)]
        with redirect_stdout(stdout), redirect_stderr(stderr):
            code = brain_artifact_smoke.main()
    finally:
        sys.argv = original_argv
    return code, stdout.getvalue(), stderr.getvalue()


def test_main_success_returns_zero_and_prints_success() -> None:
    with write_generated_artifacts() as temp:
        code, stdout, stderr = call_main(Path(temp))
        assert code == 0
        assert "brain artifact smoke passed" in stdout
        assert stderr == ""


def test_main_failure_returns_one_and_prints_error_prefix() -> None:
    with write_generated_artifacts() as temp:
        bundle = Path(temp)
        (bundle / "cost-event.json").unlink()
        code, stdout, stderr = call_main(bundle)
        assert code == 1
        assert stdout == ""
        assert stderr.startswith("brain_artifact_smoke error:")
        assert "Traceback" not in stderr


def test_main_invalid_utf8_returns_bounded_error() -> None:
    with write_generated_artifacts() as temp:
        bundle = Path(temp)
        (bundle / "avatar-pulse-packet.json").write_bytes(b"\xff")
        code, stdout, stderr = call_main(bundle)
        assert code == 1
        assert stdout == ""
        assert stderr.startswith("brain_artifact_smoke error:")
        assert "cannot be decoded as UTF-8" in stderr
        assert "Traceback" not in stderr


def test_main_oserror_returns_bounded_error() -> None:
    with write_generated_artifacts() as temp:
        bundle = Path(temp)
        with patch.object(Path, "open", side_effect=OSError("deterministic read failure")):
            code, stdout, stderr = call_main(bundle)
        assert code == 1
        assert stdout == ""
        assert stderr.startswith("brain_artifact_smoke error:")
        assert "JSON file cannot be read" in stderr
        assert "Traceback" not in stderr


def test_main_directory_inspection_error_returns_bounded_error() -> None:
    with write_generated_artifacts() as temp:
        bundle = Path(temp)
        with patch.object(Path, "iterdir", side_effect=OSError("deterministic directory failure")):
            code, stdout, stderr = call_main(bundle)
        assert code == 1
        assert stdout == ""
        assert stderr.startswith("brain_artifact_smoke error:")
        assert "artifact directory cannot be inspected" in stderr
        assert "JSON file cannot be read" not in stderr
        assert "Traceback" not in stderr


def run() -> None:
    tests = [
        test_valid_generated_artifacts_pass,
        test_missing_required_file_fails,
        test_extra_top_level_file_fails,
        test_invalid_packet_schema_fails,
        test_invalid_trace_schema_fails,
        test_invalid_cost_schema_fails,
        test_broken_packet_trace_ref_fails,
        test_broken_trace_cost_ref_fails,
        test_turn_ref_mismatch_fails,
        test_reference_type_mismatches_fail,
        test_cognition_provenance_mismatches_fail,
        test_l1_packet_boundary_mismatches_fail,
        test_artifact_uri_basename_only_is_enforced,
        test_wrong_artifact_uri_basename_fails,
        test_non_dev_mock_provider_fails,
        test_non_zero_mock_cost_fails,
        test_retry_and_fallback_mismatches_fail,
        test_direct_world_mutation_key_fails,
        test_hidden_truth_text_fails_case_insensitive,
        test_hidden_truth_numeric_string_fails,
        test_legitimate_numeric_local_status_values_pass,
        test_legitimate_numeric_belief_confidence_passes,
        test_main_non_standard_numeric_constants_return_bounded_errors,
        test_main_overflowed_exponent_returns_bounded_error,
        test_main_duplicate_keys_return_bounded_errors,
        test_main_oversized_integer_or_fallback_returns_bounded_error,
        test_main_success_returns_zero_and_prints_success,
        test_main_failure_returns_one_and_prints_error_prefix,
        test_main_invalid_utf8_returns_bounded_error,
        test_main_oserror_returns_bounded_error,
        test_main_directory_inspection_error_returns_bounded_error,
    ]
    for test in tests:
        test()
        print(f"PASS {test.__name__}")
    print(f"brain_artifact_smoke_tests: {len(tests)} passed")


if __name__ == "__main__":
    run()
