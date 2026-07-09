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


SAMPLE_POLICY = ROOT / "examples" / "model-policy.example.json"
AVATAR_PULSE_SCHEMA = REPO_ROOT / "src" / "ringfall-contracts" / "schemas" / "packets" / "avatar-pulse-packet.schema.json"
COGNITION_TRACE_SCHEMA = REPO_ROOT / "src" / "ringfall-contracts" / "schemas" / "traces" / "cognition-trace.schema.json"
COST_EVENT_SCHEMA = REPO_ROOT / "src" / "ringfall-contracts" / "schemas" / "traces" / "cost-event.schema.json"


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


if __name__ == "__main__":
    unittest.main()
