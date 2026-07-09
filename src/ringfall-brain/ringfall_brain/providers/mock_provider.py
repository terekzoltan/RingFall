"""Deterministic mock packet output for B3-C."""

from __future__ import annotations

import json


def build_avatar_pulse_packet() -> dict[str, object]:
    """Return the minimal accepted AvatarPulsePacket fixture fields."""
    return {
        "packet_id": "pkt-avatar-pulse-001",
        "packet_type": "AvatarPulsePacket",
        "schema_version": "0.1",
        "issuer_id": "actor-aster-001",
        "issuer_layer": "L1",
        "issued_at_tick": 1,
        "source_context_id": "ctx-aster-001",
        "actor_id": "actor-aster-001",
    }


def build_avatar_pulse_packet_json() -> str:
    """Return deterministic strict JSON for the mock AvatarPulsePacket."""
    return json.dumps(build_avatar_pulse_packet(), sort_keys=True, separators=(",", ":"))
