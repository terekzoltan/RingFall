from __future__ import annotations

import json
import sys
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
REPO_ROOT = ROOT.parents[1]
sys.path.insert(0, str(ROOT))

from ringfall_brain.providers.mock_provider import build_avatar_pulse_packet_json
from ringfall_brain.schemas.validator import validate_packet_json


AVATAR_PULSE_SCHEMA = REPO_ROOT / "src" / "ringfall-contracts" / "schemas" / "packets" / "avatar-pulse-packet.schema.json"


class MockProviderTests(unittest.TestCase):
    def test_avatar_pulse_output_is_deterministic(self) -> None:
        self.assertEqual(build_avatar_pulse_packet_json(), build_avatar_pulse_packet_json())

    def test_avatar_pulse_output_uses_required_fixture_fields_only(self) -> None:
        packet = json.loads(build_avatar_pulse_packet_json())

        self.assertEqual(
            {
                "actor_id",
                "issued_at_tick",
                "issuer_id",
                "issuer_layer",
                "packet_id",
                "packet_type",
                "schema_version",
                "source_context_id",
            },
            set(packet),
        )

    def test_avatar_pulse_output_validates_against_contract_schema(self) -> None:
        packet = validate_packet_json(build_avatar_pulse_packet_json(), AVATAR_PULSE_SCHEMA)

        self.assertEqual("AvatarPulsePacket", packet["packet_type"])


if __name__ == "__main__":
    unittest.main()
