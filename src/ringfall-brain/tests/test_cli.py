from __future__ import annotations

import io
import sys
import tempfile
import unittest
from contextlib import redirect_stderr, redirect_stdout
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
REPO_ROOT = ROOT.parents[1]
sys.path.insert(0, str(ROOT))

from ringfall_brain.cli import main


SAMPLE_POLICY = ROOT / "examples" / "model-policy.example.json"
AVATAR_PULSE_SCHEMA = REPO_ROOT / "src" / "ringfall-contracts" / "schemas" / "packets" / "avatar-pulse-packet.schema.json"


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


if __name__ == "__main__":
    unittest.main()
