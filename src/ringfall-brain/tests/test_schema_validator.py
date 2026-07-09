from __future__ import annotations

import json
import sys
import tempfile
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
REPO_ROOT = ROOT.parents[1]
sys.path.insert(0, str(ROOT))

from ringfall_brain.providers.mock_provider import build_avatar_pulse_packet_json
from ringfall_brain.schemas.validator import BrainValidationError, validate_packet_json


AVATAR_PULSE_SCHEMA = REPO_ROOT / "src" / "ringfall-contracts" / "schemas" / "packets" / "avatar-pulse-packet.schema.json"


class SchemaValidatorTests(unittest.TestCase):
    def test_valid_avatar_pulse_packet_passes(self) -> None:
        packet = validate_packet_json(build_avatar_pulse_packet_json(), AVATAR_PULSE_SCHEMA)

        self.assertEqual("pkt-avatar-pulse-001", packet["packet_id"])

    def test_malformed_json_fails(self) -> None:
        with self.assertRaisesRegex(BrainValidationError, "candidate packet JSON is invalid"):
            validate_packet_json("{", AVATAR_PULSE_SCHEMA)

    def test_non_object_json_fails(self) -> None:
        with self.assertRaisesRegex(BrainValidationError, "must parse to an object"):
            validate_packet_json("[]", AVATAR_PULSE_SCHEMA)

    def test_wrong_packet_type_fails_schema_validation(self) -> None:
        packet = json.loads(build_avatar_pulse_packet_json())
        packet["packet_type"] = "WrongPacket"

        with self.assertRaisesRegex(BrainValidationError, "failed schema validation"):
            validate_packet_json(json.dumps(packet), AVATAR_PULSE_SCHEMA)

    def test_missing_schema_file_fails_clearly(self) -> None:
        with self.assertRaisesRegex(BrainValidationError, "schema file not found"):
            validate_packet_json(build_avatar_pulse_packet_json(), ROOT / "missing.schema.json")

    def test_invalid_schema_file_fails_clearly(self) -> None:
        with tempfile.TemporaryDirectory() as temp_dir:
            schema_path = Path(temp_dir) / "invalid.schema.json"
            schema_path.write_text("[]", encoding="utf-8")

            with self.assertRaisesRegex(BrainValidationError, "schema JSON must parse to an object"):
                validate_packet_json(build_avatar_pulse_packet_json(), schema_path)


if __name__ == "__main__":
    unittest.main()
