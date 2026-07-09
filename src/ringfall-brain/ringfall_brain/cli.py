"""Command line entry point for the Ringfall brain skeleton."""

from __future__ import annotations

import argparse
import sys
from pathlib import Path
from typing import Sequence

from ringfall_brain import __version__
from ringfall_brain.config.model_policy_loader import (
    SUPPORTED_STEP_ONE_RUN_MODES,
    ModelPolicyError,
    load_model_policy,
)
from ringfall_brain.providers.mock_provider import build_avatar_pulse_packet_json
from ringfall_brain.schemas.validator import BrainValidationError, validate_packet_json


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        prog="ringfall-brain",
        description="Ringfall brain CLI skeleton for local policy checks.",
    )
    parser.add_argument("--version", action="store_true", help="Print the Ringfall brain version and exit.")

    subparsers = parser.add_subparsers(dest="command")
    mock_parser = subparsers.add_parser("mock", help="Deterministic mock commands.")
    mock_subparsers = mock_parser.add_subparsers(dest="mock_command")

    pulse_parser = mock_subparsers.add_parser("pulse", help="Emit and validate a mock AvatarPulsePacket.")
    pulse_parser.add_argument("--schema", required=True, help="Path to the AvatarPulsePacket JSON Schema file.")

    policy_parser = subparsers.add_parser("policy", help="Policy inspection commands.")
    policy_subparsers = policy_parser.add_subparsers(dest="policy_command")

    check_parser = policy_subparsers.add_parser("check", help="Validate a model policy fixture.")
    check_parser.add_argument("--policy", required=True, help="Path to a JSON model policy fixture.")
    check_parser.add_argument(
        "--mode",
        required=True,
        choices=SUPPORTED_STEP_ONE_RUN_MODES,
        help="Explicit Step 1 CLI run mode.",
    )
    return parser


def main(argv: Sequence[str] | None = None) -> int:
    parser = build_parser()
    args = parser.parse_args(argv)

    if args.version:
        print(f"Ringfall Brain {__version__}")
        return 0

    if args.command == "mock" and args.mock_command == "pulse":
        raw_packet = build_avatar_pulse_packet_json()
        try:
            packet = validate_packet_json(raw_packet, Path(args.schema))
        except BrainValidationError as exc:
            print(f"Mock pulse failed: {exc}", file=sys.stderr)
            return 2

        print(f"Mock packet OK: packet_type={packet['packet_type']} packet_id={packet['packet_id']}")
        return 0

    if args.command == "mock":
        print("mock subcommand required: choose pulse", file=sys.stderr)
        return 2

    if args.command == "policy" and args.policy_command == "check":
        try:
            policy = load_model_policy(Path(args.policy), args.mode)
        except ModelPolicyError as exc:
            print(f"Policy check failed: {exc}", file=sys.stderr)
            return 2

        lanes = ", ".join(sorted(policy.lanes))
        print(f"Policy OK: version={policy.version} mode={args.mode} lanes={lanes}")
        return 0

    if args.command == "policy":
        print("policy subcommand required: choose check", file=sys.stderr)
        return 2

    parser.print_help()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
