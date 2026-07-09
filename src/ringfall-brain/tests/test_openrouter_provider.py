from __future__ import annotations

import unittest

from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT))

from ringfall_brain.providers.openrouter_provider import OpenRouterConfigError, load_openrouter_config


class OpenRouterProviderTests(unittest.TestCase):
    CREDENTIAL_URL = "https://user:pass@example.test/path?token=secret#frag"

    def test_missing_api_key_is_rejected(self) -> None:
        with self.assertRaisesRegex(OpenRouterConfigError, "OPENROUTER_API_KEY"):
            load_openrouter_config({})

    def test_public_config_never_exposes_raw_secret(self) -> None:
        secret = "dummy-api-key-for-redaction-test"
        config = load_openrouter_config({"OPENROUTER_API_KEY": secret})

        self.assertTrue(config.api_key_present)
        self.assertNotIn(secret, repr(config))
        self.assertNotIn(secret, str(config.as_public_dict()))
        self.assertEqual("openrouter/manual-unconfigured", config.model_id)

    def test_optional_model_and_base_url_expose_hostname_only(self) -> None:
        config = load_openrouter_config(
            {
                "OPENROUTER_API_KEY": "dummy-api-key-for-redaction-test",
                "OPENROUTER_MODEL": "openrouter/test-model",
                "OPENROUTER_BASE_URL": "https://openrouter.example.test/api/v1",
            }
        )

        self.assertEqual("openrouter/test-model", config.model_id)
        self.assertTrue(config.base_url_present)
        self.assertEqual("openrouter.example.test", config.base_url_host)

    def test_base_url_public_metadata_never_exposes_credentials_or_raw_url(self) -> None:
        config = load_openrouter_config(
            {
                "OPENROUTER_API_KEY": "dummy-api-key-for-redaction-test",
                "OPENROUTER_BASE_URL": self.CREDENTIAL_URL,
            }
        )
        public = str(config.as_public_dict())
        representation = repr(config)

        self.assertTrue(config.base_url_present)
        self.assertEqual("example.test", config.base_url_host)
        self.assertEqual(
            {"provider", "api_key_present", "model_id", "base_url_present", "base_url_host"},
            set(config.as_public_dict()),
        )
        for output in (public, representation):
            self.assertNotIn(self.CREDENTIAL_URL, output)
            self.assertNotIn("user", output)
            self.assertNotIn("pass", output)
            self.assertNotIn("token", output)
            self.assertNotIn("secret", output)
            self.assertNotIn("/path", output)
            self.assertNotIn("#frag", output)
            self.assertNotIn("?token=secret", output)

    def test_whitespace_only_base_url_is_treated_as_absent(self) -> None:
        config = load_openrouter_config(
            {
                "OPENROUTER_API_KEY": "dummy-api-key-for-redaction-test",
                "OPENROUTER_BASE_URL": "   ",
            }
        )

        self.assertFalse(config.base_url_present)
        self.assertIsNone(config.base_url_host)

    def test_malformed_base_url_does_not_echo_raw_value(self) -> None:
        raw_url = "https://[not-a-host/path?token=secret"
        config = load_openrouter_config(
            {
                "OPENROUTER_API_KEY": "dummy-api-key-for-redaction-test",
                "OPENROUTER_BASE_URL": raw_url,
            }
        )

        self.assertTrue(config.base_url_present)
        self.assertIsNone(config.base_url_host)
        self.assertNotIn(raw_url, repr(config))
        self.assertNotIn(raw_url, str(config.as_public_dict()))


if __name__ == "__main__":
    unittest.main()
