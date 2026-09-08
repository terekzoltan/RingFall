from __future__ import annotations

from copy import deepcopy
from hashlib import sha256
import json
import os
import sys
import tempfile
from types import SimpleNamespace
import unittest
from pathlib import Path
from unittest.mock import patch


ROOT = Path(__file__).resolve().parents[1]
REPO_ROOT = ROOT.parents[1]
sys.path.insert(0, str(ROOT))

from ringfall_brain.cognition import aster_action_emitter as emitter
from ringfall_brain.schemas.validator import BrainValidationError, validate_packet_json


CONTEXT_PATH = ROOT / "examples" / "aster-a1-context.example.json"
PULSE_PATH = ROOT / "examples" / "aster-a1-pulse.example.json"
PULSE_SCHEMA = REPO_ROOT / "src" / "ringfall-contracts" / "schemas" / "packets" / "avatar-pulse-packet.schema.json"
TOOL_SCHEMA = REPO_ROOT / "src" / "ringfall-contracts" / "schemas" / "packets" / "tool-action-request.schema.json"
WORK_ORDER_SCHEMA = REPO_ROOT / "src" / "ringfall-contracts" / "schemas" / "packets" / "work-order-request.schema.json"


class AsterActionEmitterTests(unittest.TestCase):
    def load_context(self) -> dict[str, object]:
        return json.loads(CONTEXT_PATH.read_text(encoding="utf-8"))

    def load_pulse(self) -> dict[str, object]:
        return json.loads(PULSE_PATH.read_text(encoding="utf-8"))

    def build_candidates(self) -> dict[str, dict[str, object]]:
        return emitter.build_aster_action_candidates(
            self.load_context(),
            self.load_pulse(),
            PULSE_SCHEMA,
            TOOL_SCHEMA,
            WORK_ORDER_SCHEMA,
        )

    def owned_record(
        self,
        path: Path,
        payload: bytes,
        descriptor: int | None = None,
    ) -> emitter._OwnedOutput:
        digest = sha256(payload).hexdigest()
        return emitter._OwnedOutput(
            path=path,
            descriptor=descriptor,
            identity=emitter._identity_from_stat(path.lstat(), path.name),
            expected_length=len(payload),
            expected_digest=digest,
            owned_length=len(payload),
            owned_digest=digest,
        )

    def test_accepted_a4_d_inputs_emit_schema_valid_bounded_candidates(self) -> None:
        candidates = self.build_candidates()

        self.assertEqual(
            {emitter.TOOL_ACTION_KEY, emitter.WORK_ORDER_KEY},
            set(candidates),
        )
        self.assertEqual(
            {
                "packet_id": "draft_A1_tool_heat_alarm_check",
                "packet_type": "ToolActionRequest",
                "schema_version": "0.1",
                "issuer_id": "A1",
                "issuer_layer": "L1",
                "issued_at_tick": 0,
                "source_context_id": "ctx_A1_aster_heat_t000",
                "evidence_refs": [
                    "obs-a1-t000-aster-r2-heat-alarm",
                    "event:event-t000-aster-r2-heat-alarm",
                ],
                "tool_id": "local_grid_panel",
                "action": "query_heat_alarm",
                "mode": "dry_run",
            },
            candidates[emitter.TOOL_ACTION_KEY],
        )
        self.assertEqual(
            {
                "packet_id": "draft_A1_work_order_heat_alarm_inspection",
                "packet_type": "WorkOrderRequest",
                "schema_version": "0.1",
                "issuer_id": "A1",
                "issuer_layer": "L1",
                "issued_at_tick": 0,
                "source_context_id": "ctx_A1_aster_heat_t000",
                "evidence_refs": [
                    "obs-a1-t000-aster-r2-heat-alarm",
                    "event:event-t000-aster-r2-heat-alarm",
                ],
                "target_crew_id": "crew_aster_repair_02",
                "target_location": "Aster/Grid-Spine-03",
                "task_type": "inspect_and_patch",
                "priority": "high",
            },
            candidates[emitter.WORK_ORDER_KEY],
        )

        for key, schema in (
            (emitter.TOOL_ACTION_KEY, TOOL_SCHEMA),
            (emitter.WORK_ORDER_KEY, WORK_ORDER_SCHEMA),
        ):
            raw = json.dumps(candidates[key], sort_keys=True, separators=(",", ":"))
            self.assertEqual(candidates[key], validate_packet_json(raw, schema))
            raw.encode("ascii")

    def test_candidates_preserve_draft_and_provenance_lineage(self) -> None:
        pulse = self.load_pulse()
        candidates = emitter.build_aster_action_candidates(
            self.load_context(),
            pulse,
            PULSE_SCHEMA,
            TOOL_SCHEMA,
            WORK_ORDER_SCHEMA,
        )
        drafts = {
            item["packet_type"]: item["draft_ref"]
            for item in pulse["requested_packets"]
        }

        for key, packet_type in (
            (emitter.TOOL_ACTION_KEY, "ToolActionRequest"),
            (emitter.WORK_ORDER_KEY, "WorkOrderRequest"),
        ):
            candidate = candidates[key]
            self.assertEqual(drafts[packet_type], candidate["packet_id"])
            self.assertEqual(pulse["issuer_id"], candidate["issuer_id"])
            self.assertEqual(pulse["issuer_layer"], candidate["issuer_layer"])
            self.assertEqual(pulse["issued_at_tick"], candidate["issued_at_tick"])
            self.assertEqual(pulse["source_context_id"], candidate["source_context_id"])
            self.assertEqual(pulse["evidence_refs"], candidate["evidence_refs"])

    def test_candidates_omit_mutation_authority_and_hidden_truth_surfaces(self) -> None:
        serialized = json.dumps(self.build_candidates(), sort_keys=True).casefold()

        for forbidden in (
            '"execute"',
            "world_state",
            "state_patch",
            "state_diff",
            "authorized_resources",
            "constraints",
            "fallback_protocol",
            "visibility_intent",
            "thermal debt",
            "hidden thermal vulnerability",
            "l2",
            "l3",
        ):
            self.assertNotIn(forbidden, serialized)

    def test_emitter_rejects_context_and_pulse_mismatches(self) -> None:
        cases: list[tuple[str, dict[str, object], dict[str, object]]] = []

        wrong_actor_context = self.load_context()
        wrong_actor_context["actorId"] = "A2"
        cases.append(("wrong context actor", wrong_actor_context, self.load_pulse()))

        wrong_evidence_pulse = self.load_pulse()
        wrong_evidence_pulse["evidence_refs"] = ["unrelated-observation"]
        cases.append(("wrong pulse evidence", self.load_context(), wrong_evidence_pulse))

        missing_crew_context = self.load_context()
        missing_crew_context["crewRefs"] = []
        cases.append(("missing crew ref", missing_crew_context, self.load_pulse()))

        missing_tool_context = self.load_context()
        missing_tool_context["toolRefs"] = ["maintenance_console"]
        cases.append(("missing tool ref", missing_tool_context, self.load_pulse()))

        missing_draft_pulse = self.load_pulse()
        missing_draft_pulse["requested_packets"] = missing_draft_pulse["requested_packets"][:-1]
        cases.append(("missing draft", self.load_context(), missing_draft_pulse))

        duplicate_draft_pulse = self.load_pulse()
        duplicate_draft_pulse["requested_packets"].append(
            deepcopy(duplicate_draft_pulse["requested_packets"][1])
        )
        cases.append(("duplicate draft", self.load_context(), duplicate_draft_pulse))

        for name, context, pulse in cases:
            with self.subTest(name=name), self.assertRaises(BrainValidationError):
                emitter.build_aster_action_candidates(
                    context,
                    pulse,
                    PULSE_SCHEMA,
                    TOOL_SCHEMA,
                    WORK_ORDER_SCHEMA,
                )

    def test_emitter_rejects_non_object_input(self) -> None:
        with self.assertRaisesRegex(BrainValidationError, "context must be a JSON object"):
            emitter.build_aster_action_candidates(
                [],  # type: ignore[arg-type]
                self.load_pulse(),
                PULSE_SCHEMA,
                TOOL_SCHEMA,
                WORK_ORDER_SCHEMA,
            )

    def test_writer_rejects_preexisting_target_without_overwrite(self) -> None:
        candidates = self.build_candidates()
        with tempfile.TemporaryDirectory() as temp_dir:
            output_dir = Path(temp_dir) / "artifacts"
            output_dir.mkdir()
            existing_target = output_dir / emitter.TOOL_ACTION_FILENAME
            existing_target.write_bytes(b"pre-existing-content\n")
            unrelated = output_dir / "unrelated.txt"
            unrelated.write_bytes(b"preserve-me\n")

            with self.assertRaisesRegex(BrainValidationError, "already exists"):
                emitter.write_aster_action_candidates(
                    candidates,
                    output_dir,
                    TOOL_SCHEMA,
                    WORK_ORDER_SCHEMA,
                )

            self.assertEqual(b"pre-existing-content\n", existing_target.read_bytes())
            self.assertEqual(b"preserve-me\n", unrelated.read_bytes())
            self.assertFalse((output_dir / emitter.WORK_ORDER_FILENAME).exists())

    def test_writer_retries_short_writes_and_verifies_exact_payloads_A4E_RFR_002(self) -> None:
        candidates = self.build_candidates()
        real_write = os.write
        write_calls = 0

        def short_write(descriptor: int, payload: bytes | memoryview) -> int:
            nonlocal write_calls
            write_calls += 1
            return real_write(descriptor, payload[:7])

        with tempfile.TemporaryDirectory() as temp_dir:
            output_dir = Path(temp_dir) / "artifacts"
            with patch.object(emitter.os, "write", side_effect=short_write):
                written = emitter.write_aster_action_candidates(
                    candidates,
                    output_dir,
                    TOOL_SCHEMA,
                    WORK_ORDER_SCHEMA,
                )

            # A4E-RFR-002: one buffered write previously had no complete-write proof.
            self.assertGreater(write_calls, 2)
            self.assertEqual(
                [emitter.TOOL_ACTION_FILENAME, emitter.WORK_ORDER_FILENAME],
                [path.name for path in written],
            )
            for path in written:
                self.assertTrue(path.read_bytes().endswith(b"\n"))

    def test_writer_zero_progress_write_fails_and_removes_owned_leaf_A4E_RFR_002(self) -> None:
        candidates = self.build_candidates()

        with tempfile.TemporaryDirectory() as temp_dir:
            output_dir = Path(temp_dir) / "artifacts"
            with patch.object(emitter.os, "write", return_value=0):
                with self.assertRaisesRegex(OSError, "made no progress"):
                    emitter.write_aster_action_candidates(
                        candidates,
                        output_dir,
                        TOOL_SCHEMA,
                        WORK_ORDER_SCHEMA,
                    )

            self.assertFalse(output_dir.exists())

    def test_writer_exclusive_reservation_closes_preflight_race_A4E_RFR_002(self) -> None:
        candidates = self.build_candidates()
        real_open = os.open

        def collide_on_second_reservation(path: Path, flags: int, mode: int = 0o777) -> int:
            if Path(path).name == emitter.WORK_ORDER_FILENAME and flags & os.O_EXCL:
                raise FileExistsError("injected reservation race")
            return real_open(path, flags, mode)

        with tempfile.TemporaryDirectory() as temp_dir:
            output_dir = Path(temp_dir) / "artifacts"
            with (
                patch.object(emitter, "_entry_exists", return_value=False),
                patch.object(emitter.os, "open", side_effect=collide_on_second_reservation),
            ):
                with self.assertRaisesRegex(BrainValidationError, "already exists"):
                    emitter.write_aster_action_candidates(
                        candidates,
                        output_dir,
                        TOOL_SCHEMA,
                        WORK_ORDER_SCHEMA,
                    )

            self.assertFalse(output_dir.exists())

    def test_identity_primitive_unavailable_fails_closed_A4E_RFR_002(self) -> None:
        missing_inode = SimpleNamespace(st_dev=1, st_ino=0)

        with self.assertRaisesRegex(OSError, "identity is unavailable"):
            emitter._identity_from_stat(missing_inode, "test entry")  # type: ignore[arg-type]

    def test_writer_second_payload_failure_rolls_back_verified_owned_bytes(self) -> None:
        candidates = self.build_candidates()
        real_write = emitter._write_owned_payload
        call_count = 0

        def fail_second_write(record: emitter._OwnedOutput, payload: bytes) -> None:
            nonlocal call_count
            call_count += 1
            if call_count == 2:
                raise OSError("injected second payload failure")
            real_write(record, payload)

        with tempfile.TemporaryDirectory() as temp_dir:
            output_dir = Path(temp_dir) / "artifacts"
            output_dir.mkdir()
            unrelated = output_dir / "unrelated.txt"
            unrelated.write_bytes(b"preserve-me\n")

            with patch.object(emitter, "_write_owned_payload", side_effect=fail_second_write):
                with self.assertRaisesRegex(OSError, "injected second payload failure"):
                    emitter.write_aster_action_candidates(
                        candidates,
                        output_dir,
                        TOOL_SCHEMA,
                        WORK_ORDER_SCHEMA,
                    )

            self.assertEqual([unrelated], list(output_dir.iterdir()))
            self.assertEqual(b"preserve-me\n", unrelated.read_bytes())

    def test_descriptor_reader_handles_short_reads_and_rejects_wrong_length_A4E_RFR_002(self) -> None:
        payload = b"descriptor-held-payload"
        real_read = os.read

        with tempfile.TemporaryDirectory() as temp_dir:
            path = Path(temp_dir) / "payload.bin"
            path.write_bytes(payload)
            descriptor = os.open(path, os.O_RDONLY | getattr(os, "O_BINARY", 0))
            try:
                with patch.object(
                    emitter.os,
                    "read",
                    side_effect=lambda fd, length: real_read(fd, min(length, 3)),
                ):
                    self.assertEqual(payload, emitter._read_descriptor_payload(descriptor, len(payload)))

                os.lseek(descriptor, 0, os.SEEK_SET)
                with self.assertRaisesRegex(OSError, "length changed"):
                    emitter._read_descriptor_payload(descriptor, len(payload) - 1)

                path.write_bytes(payload[:-1])
                os.lseek(descriptor, 0, os.SEEK_SET)
                with self.assertRaisesRegex(OSError, "length changed"):
                    emitter._read_descriptor_payload(descriptor, len(payload))
            finally:
                os.close(descriptor)

    def test_writer_detects_same_descriptor_digest_mismatch_without_success_A4E_RFR_002(self) -> None:
        candidates = self.build_candidates()
        real_write = emitter._write_owned_payload
        changed = False

        def mutate_after_write(record: emitter._OwnedOutput, payload: bytes) -> None:
            nonlocal changed
            real_write(record, payload)
            if not changed:
                changed = True
                assert record.descriptor is not None
                os.lseek(record.descriptor, 0, os.SEEK_SET)
                os.write(record.descriptor, b"X")
                os.fsync(record.descriptor)

        with tempfile.TemporaryDirectory() as temp_dir:
            output_dir = Path(temp_dir) / "artifacts"
            with patch.object(emitter, "_write_owned_payload", side_effect=mutate_after_write):
                with self.assertRaisesRegex(OSError, "untrusted"):
                    emitter.write_aster_action_candidates(
                        candidates,
                        output_dir,
                        TOOL_SCHEMA,
                        WORK_ORDER_SCHEMA,
                    )

            self.assertTrue(output_dir.exists())
            self.assertEqual(b"X", (output_dir / emitter.TOOL_ACTION_FILENAME).read_bytes()[:1])

    def test_cleanup_preserves_different_inode_replacement_A4E_RFR_001(self) -> None:
        original = b"invocation-owned\n"
        replacement = b"replacement-must-survive\n"
        with tempfile.TemporaryDirectory() as temp_dir:
            output_dir = Path(temp_dir) / "artifacts"
            output_dir.mkdir()
            output_path = output_dir / emitter.TOOL_ACTION_FILENAME
            output_path.write_bytes(original)
            record = self.owned_record(output_path, original)
            directory_identity = emitter._identity_from_stat(output_dir.lstat(), "output directory")

            output_path.unlink()
            output_path.write_bytes(replacement)
            errors = emitter._cleanup_owned_entries(
                [record], output_dir, directory_identity, created_output_dir=False
            )

            # A4E-RFR-001: pathname-only rollback previously deleted this replacement.
            self.assertTrue(any("untrusted" in error for error in errors))
            self.assertEqual(replacement, output_path.read_bytes())

    def test_cleanup_preserves_same_entry_mutation_A4E_RFR_001(self) -> None:
        original = b"invocation-owned\n"
        mutation = b"externally-mutated\n"
        with tempfile.TemporaryDirectory() as temp_dir:
            output_dir = Path(temp_dir) / "artifacts"
            output_dir.mkdir()
            output_path = output_dir / emitter.TOOL_ACTION_FILENAME
            output_path.write_bytes(original)
            record = self.owned_record(output_path, original)
            directory_identity = emitter._identity_from_stat(output_dir.lstat(), "output directory")

            output_path.write_bytes(mutation)
            self.assertEqual(record.identity, emitter._identity_from_stat(output_path.lstat(), output_path.name))
            errors = emitter._cleanup_owned_entries(
                [record], output_dir, directory_identity, created_output_dir=False
            )

            self.assertTrue(any("untrusted" in error for error in errors))
            self.assertEqual(mutation, output_path.read_bytes())

    def test_cleanup_never_crosses_replaced_output_directory_A4E_RFR_001(self) -> None:
        original = b"invocation-owned\n"
        replacement = b"replacement-directory-content\n"
        with tempfile.TemporaryDirectory() as temp_dir:
            root = Path(temp_dir)
            output_dir = root / "artifacts"
            displaced_dir = root / "displaced-artifacts"
            output_dir.mkdir()
            output_path = output_dir / emitter.TOOL_ACTION_FILENAME
            output_path.write_bytes(original)
            record = self.owned_record(output_path, original)
            directory_identity = emitter._identity_from_stat(output_dir.lstat(), "output directory")

            output_dir.rename(displaced_dir)
            output_dir.mkdir()
            replacement_path = output_dir / emitter.TOOL_ACTION_FILENAME
            replacement_path.write_bytes(replacement)
            errors = emitter._cleanup_owned_entries(
                [record], output_dir, directory_identity, created_output_dir=True
            )

            self.assertTrue(any("untrusted" in error for error in errors))
            self.assertEqual(replacement, replacement_path.read_bytes())
            self.assertEqual(original, (displaced_dir / emitter.TOOL_ACTION_FILENAME).read_bytes())

    def test_descriptor_verification_rejects_path_identity_mismatch_A4E_RFR_002(self) -> None:
        payload = b"descriptor-held-payload"
        with tempfile.TemporaryDirectory() as temp_dir:
            output_dir = Path(temp_dir) / "artifacts"
            output_dir.mkdir()
            output_path = output_dir / emitter.TOOL_ACTION_FILENAME
            other_path = output_dir / "other.json"
            output_path.write_bytes(payload)
            other_path.write_bytes(payload)
            descriptor = os.open(output_path, os.O_RDWR | getattr(os, "O_BINARY", 0))
            def mismatched_lstat(path: Path) -> os.stat_result:
                if path == output_path:
                    return other_stat
                return real_lstat(path)

            try:
                record = self.owned_record(output_path, payload, descriptor)
                directory_identity = emitter._identity_from_stat(output_dir.lstat(), "output directory")
                real_lstat = Path.lstat
                other_stat = other_path.lstat()
                with patch.object(Path, "lstat", mismatched_lstat):
                    with self.assertRaisesRegex(OSError, "identity changed"):
                        emitter._verify_owned_output(record, output_dir, directory_identity)
            finally:
                os.close(descriptor)


if __name__ == "__main__":
    unittest.main()
