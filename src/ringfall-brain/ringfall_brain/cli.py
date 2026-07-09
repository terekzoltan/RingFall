"""Command line entry point for the Ringfall brain skeleton."""

from __future__ import annotations

import argparse
import json
import os
import sys
from pathlib import Path
from typing import Sequence

from ringfall_brain import __version__
from ringfall_brain.config.model_policy_loader import (
    SUPPORTED_STEP_ONE_RUN_MODES,
    ModelPolicyError,
    load_model_policy,
)
from ringfall_brain.providers.mock_provider import build_avatar_pulse_packet, build_avatar_pulse_packet_json
from ringfall_brain.providers.openrouter_provider import OpenRouterConfigError, load_openrouter_config
from ringfall_brain.schemas.validator import BrainValidationError, validate_packet_json
from ringfall_brain.traces.artifacts import build_mock_cognition_artifacts, write_mock_cognition_artifacts


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

    cognition_parser = mock_subparsers.add_parser("cognition", help="Emit and validate mock cognition trace/cost artifacts.")
    cognition_parser.add_argument("--packet-schema", required=True, help="Path to the AvatarPulsePacket JSON Schema file.")
    cognition_parser.add_argument("--cognition-schema", required=True, help="Path to the CognitionTrace JSON Schema file.")
    cognition_parser.add_argument("--cost-schema", required=True, help="Path to the CostEvent JSON Schema file.")
    cognition_parser.add_argument("--output-dir", help="Optional directory for explicit dev/mock artifact output.")

    provider_parser = subparsers.add_parser("provider", help="Provider shell commands.")
    provider_subparsers = provider_parser.add_subparsers(dest="provider_command")
    openrouter_parser = provider_subparsers.add_parser("openrouter", help="OpenRouter shell checks.")
    openrouter_subparsers = openrouter_parser.add_subparsers(dest="openrouter_command")
    openrouter_subparsers.add_parser("check-env", help="Check shell-only OpenRouter environment configuration.")

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

    if args.command == "mock" and args.mock_command == "cognition":
        try:
            artifacts = build_mock_cognition_artifacts(
                build_avatar_pulse_packet(),
                Path(args.packet_schema),
                Path(args.cognition_schema),
                Path(args.cost_schema),
            )
            written = []
            if args.output_dir:
                written = write_mock_cognition_artifacts(artifacts, Path(args.output_dir))
        except BrainValidationError as exc:
            print(f"Mock cognition failed: {exc}", file=sys.stderr)
            return 2
        except OSError as exc:
            print(f"Mock cognition failed: artifact output error: {exc}", file=sys.stderr)
            return 2

        summary = {
            "status": "ok",
            "packet_id": artifacts["packet"]["packet_id"],
            "cognition_id": artifacts["cognition_trace"]["cognition_id"],
            "cost_event_id": artifacts["cost_event"]["cost_event_id"],
            "run_mode": artifacts["cost_event"]["run_mode"],
            "schema_valid": artifacts["cognition_trace"]["schema_valid"] and artifacts["cost_event"]["schema_valid"],
            "written_files": [path.name for path in written],
        }
        print(json.dumps(summary, sort_keys=True, separators=(",", ":")))
        return 0

    if args.command == "mock":
        print("mock subcommand required: choose pulse or cognition", file=sys.stderr)
        return 2

    if args.command == "provider" and args.provider_command == "openrouter" and args.openrouter_command == "check-env":
        try:
            config = load_openrouter_config(os.environ)
        except OpenRouterConfigError as exc:
            print(f"OpenRouter env check failed: {exc}", file=sys.stderr)
            return 2

        print(json.dumps(config.as_public_dict(), sort_keys=True, separators=(",", ":")))
        return 0

    if args.command == "provider" and args.provider_command == "openrouter":
        print("openrouter subcommand required: choose check-env", file=sys.stderr)
        return 2

    if args.command == "provider":
        print("provider subcommand required: choose openrouter", file=sys.stderr)
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
