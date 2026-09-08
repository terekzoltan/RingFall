from __future__ import annotations

import io
import json
import os
import sys
import tempfile
import unittest
from contextlib import redirect_stderr, redirect_stdout
from pathlib import Path
from unittest.mock import patch

ROOT = Path(__file__).resolve().parents[1]
REPO_ROOT = ROOT.parents[1]
sys.path.insert(0, str(ROOT))

from ringfall_brain.cli import main
from ringfall_brain.cognition import aster_action_emitter as emitter


SAMPLE_POLICY = ROOT / "examples" / "model-policy.example.json"
AVATAR_PULSE_SCHEMA = REPO_ROOT / "src" / "ringfall-contracts" / "schemas" / "packets" / "avatar-pulse-packet.schema.json"
COGNITION_TRACE_SCHEMA = REPO_ROOT / "src" / "ringfall-contracts" / "schemas" / "traces" / "cognition-trace.schema.json"
COST_EVENT_SCHEMA = REPO_ROOT / "src" / "ringfall-contracts" / "schemas" / "traces" / "cost-event.schema.json"
ASTER_CONTEXT = ROOT / "examples" / "aster-a1-context.example.json"
ASTER_PULSE = ROOT / "examples" / "aster-a1-pulse.example.json"
TOOL_ACTION_SCHEMA = REPO_ROOT / "src" / "ringfall-contracts" / "schemas" / "packets" / "tool-action-request.schema.json"
WORK_ORDER_SCHEMA = REPO_ROOT / "src" / "ringfall-contracts" / "schemas" / "packets" / "work-order-request.schema.json"


class CliTests(unittest.TestCase):
    def run_cli(self, *args: str) -> tuple[int, str, str]:
        stdout = io.StringIO()
        stderr = io.StringIO()
        with redirect_stdout(stdout), redirect_stderr(stderr):
            try:
                code = main(args)
            except SystemExit as exc:
                code = int(exc.code)
        return code, stdout.getvalue(), stderr.getvalue()

    def aster_action_args(
        self,
        output_dir: Path,
        *,
        context: Path = ASTER_CONTEXT,
        pulse: Path = ASTER_PULSE,
        pulse_schema: Path = AVATAR_PULSE_SCHEMA,
        tool_schema: Path = TOOL_ACTION_SCHEMA,
        work_order_schema: Path = WORK_ORDER_SCHEMA,
    ) -> tuple[str, ...]:
        return (
            "mock",
            "aster-actions",
            "--context",
            str(context),
            "--pulse",
            str(pulse),
            "--pulse-schema",
            str(pulse_schema),
            "--tool-schema",
            str(tool_schema),
            "--work-order-schema",
            str(work_order_schema),
            "--output-dir",
            str(output_dir),
        )

    def test_help_returns_usage(self) -> None:
        code, stdout, stderr = self.run_cli("--help")

        self.assertEqual(0, code)
        self.assertIn("Ringfall brain CLI skeleton", stdout)
        self.assertEqual("", stderr)

    def test_version_returns_package_version(self) -> None:
        code, stdout, stderr = self.run_cli("--version")

        self.assertEqual(0, code)
        self.assertIn("Ringfall Brain 0.1.0", stdout)
        self.assertEqual("", stderr)

    def test_policy_check_requires_explicit_mode(self) -> None:
        code, _stdout, stderr = self.run_cli("policy", "check", "--policy", str(SAMPLE_POLICY))

        self.assertEqual(2, code)
        self.assertIn("--mode", stderr)

    def test_policy_command_requires_subcommand(self) -> None:
        code, stdout, stderr = self.run_cli("policy")

        self.assertEqual(2, code)
        self.assertEqual("", stdout)
        self.assertIn("policy subcommand required", stderr)

    def test_policy_check_accepts_supported_mode(self) -> None:
        code, stdout, stderr = self.run_cli(
            "policy",
            "check",
            "--policy",
            str(SAMPLE_POLICY),
            "--mode",
            "mock",
        )

        self.assertEqual(0, code)
        self.assertIn("Policy OK", stdout)
        self.assertIn("mode=mock", stdout)
        self.assertEqual("", stderr)

    def test_policy_check_rejects_unsupported_mode(self) -> None:
        code, _stdout, stderr = self.run_cli(
            "policy",
            "check",
            "--policy",
            str(SAMPLE_POLICY),
            "--mode",
            "canonical",
        )

        self.assertEqual(2, code)
        self.assertIn("invalid choice", stderr)

    def test_policy_check_reports_missing_policy(self) -> None:
        code, _stdout, stderr = self.run_cli(
            "policy",
            "check",
            "--policy",
            str(ROOT / "examples" / "missing.json"),
            "--mode",
            "disabled",
        )

        self.assertEqual(2, code)
        self.assertIn("policy file not found", stderr)

    def test_policy_check_reports_invalid_json(self) -> None:
        with tempfile.TemporaryDirectory() as temp_dir:
            policy_path = Path(temp_dir) / "invalid.json"
            policy_path.write_text("{", encoding="utf-8")

            code, _stdout, stderr = self.run_cli(
                "policy",
                "check",
                "--policy",
                str(policy_path),
                "--mode",
                "disabled",
            )

        self.assertEqual(2, code)
        self.assertIn("policy JSON is invalid", stderr)

    def test_mock_pulse_validates_against_schema(self) -> None:
        code, stdout, stderr = self.run_cli("mock", "pulse", "--schema", str(AVATAR_PULSE_SCHEMA))

        self.assertEqual(0, code)
        self.assertIn("Mock packet OK", stdout)
        self.assertIn("packet_type=AvatarPulsePacket", stdout)
        self.assertEqual("", stderr)

    def test_mock_pulse_missing_schema_fails_without_traceback(self) -> None:
        code, stdout, stderr = self.run_cli("mock", "pulse", "--schema", str(ROOT / "missing.schema.json"))

        self.assertEqual(2, code)
        self.assertEqual("", stdout)
        self.assertIn("schema file not found", stderr)
        self.assertNotIn("Traceback", stderr)

    def test_openrouter_check_env_missing_key_fails_without_traceback(self) -> None:
        with patch.dict(os.environ, {}, clear=True):
            code, stdout, stderr = self.run_cli("provider", "openrouter", "check-env")

        self.assertEqual(2, code)
        self.assertEqual("", stdout)
        self.assertIn("OPENROUTER_API_KEY", stderr)
        self.assertNotIn("Traceback", stderr)

    def test_openrouter_check_env_reports_safe_json_without_secret(self) -> None:
        secret = "dummy-api-key-for-redaction-test"
        with patch.dict(os.environ, {"OPENROUTER_API_KEY": secret}, clear=True):
            code, stdout, stderr = self.run_cli("provider", "openrouter", "check-env")

        self.assertEqual(0, code)
        self.assertEqual("", stderr)
        payload = json.loads(stdout)
        self.assertEqual("openrouter", payload["provider"])
        self.assertTrue(payload["api_key_present"])
        self.assertEqual("openrouter/manual-unconfigured", payload["model_id"])
        self.assertFalse(payload["base_url_present"])
        self.assertIsNone(payload["base_url_host"])
        self.assertNotIn(secret, stdout)
        self.assertNotIn(secret, stderr)

    def test_openrouter_check_env_sanitizes_credential_bearing_base_url(self) -> None:
        secret = "dummy-api-key-for-redaction-test"
        raw_url = "https://user:pass@example.test/path?token=secret#frag"
        with patch.dict(
            os.environ,
            {"OPENROUTER_API_KEY": secret, "OPENROUTER_BASE_URL": raw_url},
            clear=True,
        ):
            code, stdout, stderr = self.run_cli("provider", "openrouter", "check-env")

        self.assertEqual(0, code)
        self.assertEqual("", stderr)
        payload = json.loads(stdout)
        self.assertTrue(payload["base_url_present"])
        self.assertEqual("example.test", payload["base_url_host"])
        self.assertNotIn("base_url", payload)
        for output in (stdout, stderr):
            self.assertNotIn(raw_url, output)
            self.assertNotIn("user", output)
            self.assertNotIn("pass", output)
            self.assertNotIn("token", output)
            self.assertNotIn("secret", output)
            self.assertNotIn("/path", output)
            self.assertNotIn("#frag", output)
            self.assertNotIn("?token=secret", output)

    def test_mock_cognition_validates_without_writing_files_by_default(self) -> None:
        with tempfile.TemporaryDirectory() as temp_dir:
            before = set(Path(temp_dir).iterdir())
            code, stdout, stderr = self.run_cli(
                "mock",
                "cognition",
                "--packet-schema",
                str(AVATAR_PULSE_SCHEMA),
                "--cognition-schema",
                str(COGNITION_TRACE_SCHEMA),
                "--cost-schema",
                str(COST_EVENT_SCHEMA),
            )
            after = set(Path(temp_dir).iterdir())

        self.assertEqual(0, code)
        self.assertEqual("", stderr)
        payload = json.loads(stdout)
        self.assertEqual("ok", payload["status"])
        self.assertEqual("dev", payload["run_mode"])
        self.assertTrue(payload["schema_valid"])
        self.assertEqual([], payload["written_files"])
        self.assertEqual(before, after)

    def test_mock_cognition_output_dir_writes_expected_files_only(self) -> None:
        with tempfile.TemporaryDirectory() as temp_dir:
            output_dir = Path(temp_dir) / "artifacts"
            code, stdout, stderr = self.run_cli(
                "mock",
                "cognition",
                "--packet-schema",
                str(AVATAR_PULSE_SCHEMA),
                "--cognition-schema",
                str(COGNITION_TRACE_SCHEMA),
                "--cost-schema",
                str(COST_EVENT_SCHEMA),
                "--output-dir",
                str(output_dir),
            )
            files = sorted(path.name for path in output_dir.iterdir())

        self.assertEqual(0, code)
        self.assertEqual("", stderr)
        self.assertEqual(["avatar-pulse-packet.json", "cognition-trace.json", "cost-event.json"], files)
        payload = json.loads(stdout)
        self.assertEqual(files, payload["written_files"])

    def test_mock_aster_actions_writes_exact_candidate_set_and_summary(self) -> None:
        with tempfile.TemporaryDirectory() as temp_dir:
            output_dir = Path(temp_dir) / "artifacts"
            code, stdout, stderr = self.run_cli(*self.aster_action_args(output_dir))
            files = sorted(path.name for path in output_dir.iterdir())
            tool_action = json.loads((output_dir / emitter.TOOL_ACTION_FILENAME).read_text(encoding="utf-8"))
            work_order = json.loads((output_dir / emitter.WORK_ORDER_FILENAME).read_text(encoding="utf-8"))

        self.assertEqual(0, code)
        self.assertEqual("", stderr)
        self.assertEqual(
            [emitter.TOOL_ACTION_FILENAME, emitter.WORK_ORDER_FILENAME],
            files,
        )
        self.assertEqual("ToolActionRequest", tool_action["packet_type"])
        self.assertEqual("WorkOrderRequest", work_order["packet_type"])

        summary = json.loads(stdout)
        self.assertEqual(
            {"candidate_only", "packet_ids", "schema_valid", "status", "written_files"},
            set(summary),
        )
        self.assertEqual(
            {
                "candidate_only": True,
                "packet_ids": {
                    emitter.TOOL_ACTION_KEY: "draft_A1_tool_heat_alarm_check",
                    emitter.WORK_ORDER_KEY: "draft_A1_work_order_heat_alarm_inspection",
                },
                "schema_valid": True,
                "status": "ok",
                "written_files": [
                    emitter.TOOL_ACTION_FILENAME,
                    emitter.WORK_ORDER_FILENAME,
                ],
            },
            summary,
        )

    def test_cli_preexisting_target_collision_preserves_content(self) -> None:
        with tempfile.TemporaryDirectory() as temp_dir:
            output_dir = Path(temp_dir) / "artifacts"
            output_dir.mkdir()
            existing_target = output_dir / emitter.TOOL_ACTION_FILENAME
            existing_target.write_bytes(b"pre-existing-content\n")
            unrelated = output_dir / "unrelated.txt"
            unrelated.write_bytes(b"preserve-me\n")

            code, stdout, stderr = self.run_cli(*self.aster_action_args(output_dir))

            self.assertEqual(b"pre-existing-content\n", existing_target.read_bytes())
            self.assertEqual(b"preserve-me\n", unrelated.read_bytes())
            self.assertFalse((output_dir / emitter.WORK_ORDER_FILENAME).exists())

        self.assertEqual(2, code)
        self.assertEqual("", stdout)
        self.assertIn("already exists", stderr)
        self.assertNotIn("Traceback", stderr)

    def test_cli_descriptor_verification_failure_rolls_back_candidate_set(self) -> None:
        with tempfile.TemporaryDirectory() as temp_dir:
            output_dir = Path(temp_dir) / "artifacts"
            output_dir.mkdir()
            unrelated = output_dir / "unrelated.txt"
            unrelated.write_bytes(b"preserve-me\n")

            with patch.object(
                emitter,
                "_verify_owned_output",
                side_effect=OSError("injected descriptor verification failure"),
            ):
                code, stdout, stderr = self.run_cli(*self.aster_action_args(output_dir))

            self.assertEqual([unrelated], list(output_dir.iterdir()))
            self.assertEqual(b"preserve-me\n", unrelated.read_bytes())

        self.assertEqual(2, code)
        self.assertEqual("", stdout)
        self.assertIn("injected descriptor verification failure", stderr)
        self.assertNotIn("Traceback", stderr)

    def test_cli_replaced_output_directory_is_untrusted_and_not_cleaned_A4E_RFR_001(self) -> None:
        with tempfile.TemporaryDirectory() as temp_dir:
            root = Path(temp_dir)
            output_dir = root / "artifacts"
            displaced_dir = root / "displaced-artifacts"
            real_close = emitter._close_owned_descriptors

            def close_then_replace(records: list[emitter._OwnedOutput]) -> list[str]:
                errors = real_close(records)
                output_dir.rename(displaced_dir)
                output_dir.mkdir()
                (output_dir / "replacement-sentinel.txt").write_bytes(b"preserve replacement\n")
                return errors

            with (
                patch.object(
                    emitter,
                    "_verify_owned_output",
                    side_effect=OSError("injected descriptor verification failure"),
                ),
                patch.object(emitter, "_close_owned_descriptors", side_effect=close_then_replace),
            ):
                code, stdout, stderr = self.run_cli(*self.aster_action_args(output_dir))

            self.assertEqual(
                b"preserve replacement\n",
                (output_dir / "replacement-sentinel.txt").read_bytes(),
            )
            self.assertEqual(
                [emitter.TOOL_ACTION_FILENAME, emitter.WORK_ORDER_FILENAME],
                sorted(path.name for path in displaced_dir.iterdir()),
            )

        self.assertEqual(2, code)
        self.assertEqual("", stdout)
        self.assertIn("untrusted", stderr)
        self.assertNotIn("Traceback", stderr)

    def test_mock_aster_actions_invalid_json_fails_before_output(self) -> None:
        with tempfile.TemporaryDirectory() as temp_dir:
            invalid_pulse = Path(temp_dir) / "invalid-pulse.json"
            invalid_pulse.write_text("{", encoding="utf-8")
            output_dir = Path(temp_dir) / "artifacts"

            code, stdout, stderr = self.run_cli(
                *self.aster_action_args(output_dir, pulse=invalid_pulse)
            )

            self.assertFalse(output_dir.exists())

        self.assertEqual(2, code)
        self.assertEqual("", stdout)
        self.assertIn("A4-D pulse JSON is invalid", stderr)
        self.assertNotIn("Traceback", stderr)

    def test_mock_aster_actions_rejects_duplicate_object_names_recursively_A4E_TE_001(self) -> None:
        marker = "duplicate-value-must-not-be-echoed"
        context_text = ASTER_CONTEXT.read_text(encoding="utf-8")
        pulse_text = ASTER_PULSE.read_text(encoding="utf-8")
        cases = (
            (
                "context top level",
                "context.json",
                context_text.replace("{", f'{{\n  "actorId": "{marker}",', 1),
                "context",
            ),
            (
                "context nested",
                "context.json",
                context_text.replace('"signal":', f'"signal": "{marker}",\n      "signal":', 1),
                "context",
            ),
            (
                "pulse top level",
                "pulse.json",
                pulse_text.replace("{", f'{{\n  "packet_id": "{marker}",', 1),
                "pulse",
            ),
            (
                "pulse nested",
                "pulse.json",
                pulse_text.replace('"draft_ref":', f'"draft_ref": "{marker}",\n      "draft_ref":', 1),
                "pulse",
            ),
        )

        with tempfile.TemporaryDirectory() as temp_dir:
            temp_path = Path(temp_dir)
            for name, filename, duplicate_json, input_kind in cases:
                with self.subTest(name=name):
                    input_path = temp_path / f"{name.replace(' ', '-')}-{filename}"
                    input_path.write_text(duplicate_json, encoding="utf-8")
                    output_dir = temp_path / f"{name.replace(' ', '-')}-artifacts"
                    overrides = {input_kind: input_path}

                    code, stdout, stderr = self.run_cli(
                        *self.aster_action_args(output_dir, **overrides)  # type: ignore[arg-type]
                    )

                    # A4E-TE-001: default json.loads silently retained the last duplicate value.
                    self.assertEqual(2, code)
                    self.assertEqual("", stdout)
                    self.assertIn(input_kind, stderr.casefold())
                    self.assertIn("duplicate", stderr.casefold())
                    self.assertNotIn(marker, stderr)
                    self.assertNotIn("Traceback", stderr)
                    self.assertFalse(output_dir.exists())

    def test_mock_aster_actions_bounds_invalid_utf8_for_every_input_A4E_TE_002(self) -> None:
        cases = (
            ("context", "A4-D context"),
            ("pulse", "A4-D pulse"),
            ("pulse_schema", "pulse schema"),
            ("tool_schema", "tool action schema"),
            ("work_order_schema", "work order schema"),
        )

        with tempfile.TemporaryDirectory() as temp_dir:
            temp_path = Path(temp_dir)
            for argument, expected_label in cases:
                with self.subTest(argument=argument):
                    invalid_path = temp_path / f"invalid-{argument}.json"
                    invalid_path.write_bytes(b'{"invalid":"\xff"}')
                    output_dir = temp_path / f"{argument}-artifacts"

                    code, stdout, stderr = self.run_cli(
                        *self.aster_action_args(output_dir, **{argument: invalid_path})  # type: ignore[arg-type]
                    )

                    # A4E-TE-002: UnicodeDecodeError previously escaped the bounded CLI boundary.
                    self.assertEqual(2, code)
                    self.assertEqual("", stdout)
                    self.assertIn(expected_label.casefold(), stderr.casefold())
                    self.assertIn("utf-8", stderr.casefold())
                    self.assertNotIn("Traceback", stderr)
                    self.assertFalse(output_dir.exists())

    def test_mock_aster_actions_requires_output_directory(self) -> None:
        args_without_output = self.aster_action_args(Path("unused"))[:-2]

        code, stdout, stderr = self.run_cli(*args_without_output)

        self.assertEqual(2, code)
        self.assertEqual("", stdout)
        self.assertIn("--output-dir", stderr)


if __name__ == "__main__":
    unittest.main()
