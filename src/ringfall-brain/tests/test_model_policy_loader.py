from __future__ import annotations

import copy
import json
import sys
import tempfile
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT))

from ringfall_brain.config.model_policy_loader import ModelPolicyError, load_model_policy


SAMPLE_POLICY = ROOT / "examples" / "model-policy.example.json"


class ModelPolicyLoaderTests(unittest.TestCase):
    def test_loads_valid_policy_deterministically(self) -> None:
        policy = load_model_policy(SAMPLE_POLICY, "disabled")

        self.assertEqual("0.1", policy.version)
        self.assertEqual("disabled", policy.default_mode)
        self.assertEqual(("disabled", "mock", "manual"), policy.available_modes)
        self.assertEqual(("policy_check",), policy.lanes)

    def test_accepts_each_step_one_mode(self) -> None:
        for mode in ("disabled", "mock", "manual"):
            with self.subTest(mode=mode):
                policy = load_model_policy(SAMPLE_POLICY, mode)
                self.assertIn(mode, policy.available_modes)

    def test_rejects_mode_absent_from_policy(self) -> None:
        with self.assertRaisesRegex(ModelPolicyError, "unsupported mode"):
            load_model_policy(SAMPLE_POLICY, "canonical")

    def test_rejects_policy_declared_unsupported_step_one_mode(self) -> None:
        data = _load_sample()
        data["run_modes"]["available"].append("canonical")

        with _temp_policy(data) as policy_path:
            with self.assertRaisesRegex(ModelPolicyError, "unsupported Step 1 mode"):
                load_model_policy(policy_path, "disabled")

    def test_rejects_unknown_model_reference(self) -> None:
        data = _load_sample()
        data["lanes"]["policy_check"]["primary"] = ["missing_model"]

        with _temp_policy(data) as policy_path:
            with self.assertRaisesRegex(ModelPolicyError, "unknown model"):
                load_model_policy(policy_path, "disabled")

    def test_rejects_missing_default_mode(self) -> None:
        data = _load_sample()
        data["run_modes"]["default"] = "canonical"

        with _temp_policy(data) as policy_path:
            with self.assertRaisesRegex(ModelPolicyError, "run_modes.default"):
                load_model_policy(policy_path, "disabled")

    def test_rejects_non_object_root(self) -> None:
        with _temp_policy(["not", "an", "object"]) as policy_path:
            with self.assertRaisesRegex(ModelPolicyError, "root"):
                load_model_policy(policy_path, "disabled")


def _load_sample() -> dict[str, object]:
    with SAMPLE_POLICY.open("r", encoding="utf-8") as handle:
        return copy.deepcopy(json.load(handle))


class _temp_policy:
    def __init__(self, data: object) -> None:
        self.data = data
        self.temp_dir: tempfile.TemporaryDirectory[str] | None = None
        self.path: Path | None = None

    def __enter__(self) -> Path:
        self.temp_dir = tempfile.TemporaryDirectory()
        self.path = Path(self.temp_dir.name) / "policy.json"
        self.path.write_text(json.dumps(self.data), encoding="utf-8")
        return self.path

    def __exit__(self, exc_type: object, exc: object, traceback: object) -> None:
        if self.temp_dir is not None:
            self.temp_dir.cleanup()


if __name__ == "__main__":
    unittest.main()
