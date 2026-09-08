"""Deterministic Aster L1 action candidates for Wave 4 A4-E."""

from __future__ import annotations

from dataclasses import dataclass
from hashlib import sha256
import json
import os
import stat
from collections.abc import Mapping
from pathlib import Path
from typing import Any

from ringfall_brain.schemas.validator import BrainValidationError, validate_packet_json


TOOL_ACTION_KEY = "tool_action_request"
WORK_ORDER_KEY = "work_order_request"
TOOL_ACTION_FILENAME = "tool-action-request.json"
WORK_ORDER_FILENAME = "work-order-request.json"

_EXPECTED_CONTEXT_KEYS = {
    "actorId",
    "displayName",
    "role",
    "layer",
    "observations",
    "crewRefs",
    "toolRefs",
}
_EXPECTED_OBSERVATION = {
    "observationId": "obs-a1-t000-aster-r2-heat-alarm",
    "kind": "alarm",
    "signal": "Local R2 heat alarm is active.",
    "sourceRef": "event:event-t000-aster-r2-heat-alarm",
}
_EXPECTED_DRAFTS = {
    "SceneActionPacket": "draft_A1_scene_heat_alarm",
    "ToolActionRequest": "draft_A1_tool_heat_alarm_check",
    "WorkOrderRequest": "draft_A1_work_order_heat_alarm_inspection",
}
_EMPTY_DIGEST = sha256(b"").hexdigest()


@dataclass
class _OwnedOutput:
    path: Path
    descriptor: int | None
    identity: tuple[int, int]
    expected_length: int
    expected_digest: str
    owned_length: int = 0
    owned_digest: str = _EMPTY_DIGEST
    verified: bool = False


def build_aster_action_candidates(
    context: Mapping[str, object],
    pulse: Mapping[str, object],
    pulse_schema: Path,
    tool_schema: Path,
    work_order_schema: Path,
) -> dict[str, dict[str, Any]]:
    """Build and schema-validate the two bounded Aster action proposals."""
    context_data = _require_json_object(context, "A4-D context")
    pulse_data = _validate_packet_with_schema(
        _dump_json(pulse, "A4-D pulse"),
        pulse_schema,
        "pulse schema",
    )

    _validate_context(context_data)
    _validate_pulse(context_data, pulse_data)

    evidence_refs = [
        _EXPECTED_OBSERVATION["observationId"],
        _EXPECTED_OBSERVATION["sourceRef"],
    ]
    common = {
        "schema_version": "0.1",
        "issuer_id": "A1",
        "issuer_layer": "L1",
        "issued_at_tick": 0,
        "source_context_id": "ctx_A1_aster_heat_t000",
        "evidence_refs": evidence_refs,
    }
    tool_action = {
        **common,
        "packet_id": _EXPECTED_DRAFTS["ToolActionRequest"],
        "packet_type": "ToolActionRequest",
        "tool_id": "local_grid_panel",
        "action": "query_heat_alarm",
        "mode": "dry_run",
    }
    work_order = {
        **common,
        "evidence_refs": list(evidence_refs),
        "packet_id": _EXPECTED_DRAFTS["WorkOrderRequest"],
        "packet_type": "WorkOrderRequest",
        "target_crew_id": "crew_aster_repair_02",
        "target_location": "Aster/Grid-Spine-03",
        "task_type": "inspect_and_patch",
        "priority": "high",
    }

    validated_tool = _validate_packet_with_schema(
        _dump_json(tool_action, "tool action candidate"),
        tool_schema,
        "tool action schema",
    )
    validated_work_order = _validate_packet_with_schema(
        _dump_json(work_order, "work order candidate"),
        work_order_schema,
        "work order schema",
    )
    return {
        TOOL_ACTION_KEY: validated_tool,
        WORK_ORDER_KEY: validated_work_order,
    }


def write_aster_action_candidates(
    candidates: Mapping[str, Mapping[str, Any]],
    output_dir: Path,
    tool_schema: Path,
    work_order_schema: Path,
) -> list[Path]:
    """Reserve, write, and verify both final candidates without clobbering."""
    if set(candidates) != {TOOL_ACTION_KEY, WORK_ORDER_KEY}:
        raise BrainValidationError(
            "Aster action candidates must contain exactly tool_action_request and work_order_request"
        )

    specifications = (
        (TOOL_ACTION_KEY, TOOL_ACTION_FILENAME, tool_schema),
        (WORK_ORDER_KEY, WORK_ORDER_FILENAME, work_order_schema),
    )
    payloads: list[tuple[str, bytes]] = []
    for key, filename, schema_path in specifications:
        candidate = _require_json_object(candidates[key], f"{key} candidate")
        raw_candidate = _dump_json(candidate, f"{key} candidate")
        schema_label = "tool action schema" if key == TOOL_ACTION_KEY else "work order schema"
        validated = _validate_packet_with_schema(raw_candidate, schema_path, schema_label)
        payloads.append((filename, (_dump_json(validated, f"{key} candidate") + "\n").encode("utf-8")))

    parent = output_dir.parent
    if not parent.is_dir():
        raise OSError(f"artifact output parent is not a directory: {parent}")

    created_output_dir = False
    directory_identity: tuple[int, int] | None = None
    owned_outputs: list[_OwnedOutput] = []
    final_paths = [output_dir / filename for filename, _payload in payloads]

    try:
        try:
            output_dir.mkdir()
            created_output_dir = True
        except FileExistsError:
            pass

        directory_stat = _lstat_entry(output_dir, "artifact output directory")
        if not stat.S_ISDIR(directory_stat.st_mode):
            raise OSError(f"artifact output path is not a directory: {output_dir}")
        directory_identity = _identity_from_stat(directory_stat, "output directory")
        _assert_directory_identity(output_dir, directory_identity)

        collisions = [path.name for path in final_paths if _entry_exists(path)]
        _assert_directory_identity(output_dir, directory_identity)
        if collisions:
            raise BrainValidationError(
                f"Aster action output target already exists: {', '.join(collisions)}"
            )

        for (filename, payload), final_path in zip(payloads, final_paths, strict=True):
            _assert_directory_identity(output_dir, directory_identity)
            owned_outputs.append(_reserve_owned_output(final_path, payload))
            _assert_owned_output_identity(owned_outputs[-1])
            _assert_directory_identity(output_dir, directory_identity)

        for record, (_filename, payload) in zip(owned_outputs, payloads, strict=True):
            _assert_directory_identity(output_dir, directory_identity)
            _write_owned_payload(record, payload)
            _assert_directory_identity(output_dir, directory_identity)

        for record in owned_outputs:
            _verify_owned_output(record, output_dir, directory_identity)

        _assert_directory_identity(output_dir, directory_identity)
        for record in owned_outputs:
            _assert_owned_output_identity(record)
        _assert_directory_identity(output_dir, directory_identity)
    except Exception as exc:
        close_errors = _close_owned_descriptors(owned_outputs)
        cleanup_errors = _cleanup_owned_entries(
            owned_outputs,
            output_dir,
            directory_identity,
            created_output_dir,
        ) if directory_identity is not None else []
        recovery_errors = [*close_errors, *cleanup_errors]
        if recovery_errors:
            details = "; ".join(recovery_errors)
            raise OSError(
                f"Aster action output cleanup failed; output directory is untrusted: {details}"
            ) from exc
        raise

    close_errors = _close_owned_descriptors(owned_outputs)
    if close_errors:
        details = "; ".join(close_errors)
        raise OSError(f"Aster action output close failed; output directory is untrusted: {details}")
    return final_paths


def _validate_context(context: dict[str, Any]) -> None:
    if set(context) != _EXPECTED_CONTEXT_KEYS:
        raise BrainValidationError("A4-D context fields do not match the accepted Aster input surface")
    if context["actorId"] != "A1" or context["layer"] != "L1":
        raise BrainValidationError("A4-D context actor or layer does not match the accepted Aster identity")
    if context["displayName"] != "A1" or context["role"] != "senior grid runner":
        raise BrainValidationError("A4-D context role does not match the accepted Aster identity")
    if context["observations"] != [_EXPECTED_OBSERVATION]:
        raise BrainValidationError("A4-D context observation does not match the accepted visible heat alarm")
    if context["crewRefs"] != ["crew_aster_repair_02"]:
        raise BrainValidationError("A4-D context crew references do not match the accepted Aster target")
    if context["toolRefs"] != ["local_grid_panel", "maintenance_console"]:
        raise BrainValidationError("A4-D context tool references do not match the accepted Aster targets")


def _validate_pulse(context: dict[str, Any], pulse: dict[str, Any]) -> None:
    if pulse.get("packet_id") != "pulse_A1_t000":
        raise BrainValidationError("A4-D pulse identity does not match the accepted Aster pulse")
    if not (
        pulse.get("issuer_id") == pulse.get("actor_id") == context["actorId"] == "A1"
        and pulse.get("issuer_layer") == context["layer"] == "L1"
    ):
        raise BrainValidationError("A4-D pulse actor or layer does not match the accepted context")
    if pulse.get("issued_at_tick") != 0 or pulse.get("source_context_id") != "ctx_A1_aster_heat_t000":
        raise BrainValidationError("A4-D pulse tick or source context does not match the accepted Aster pulse")
    if pulse.get("observed") != [_EXPECTED_OBSERVATION["signal"]]:
        raise BrainValidationError("A4-D pulse observation does not preserve the accepted visible signal")

    expected_evidence = [
        _EXPECTED_OBSERVATION["observationId"],
        _EXPECTED_OBSERVATION["sourceRef"],
    ]
    if pulse.get("evidence_refs") != expected_evidence:
        raise BrainValidationError("A4-D pulse evidence does not preserve the accepted observation provenance")

    requested_packets = pulse.get("requested_packets")
    if not isinstance(requested_packets, list) or len(requested_packets) != len(_EXPECTED_DRAFTS):
        raise BrainValidationError("A4-D pulse must contain the accepted scene, tool, and work-order drafts")
    for packet_type, expected_draft in _EXPECTED_DRAFTS.items():
        matches = [
            item
            for item in requested_packets
            if isinstance(item, dict) and item.get("packet_type") == packet_type
        ]
        if len(matches) != 1 or matches[0].get("draft_ref") != expected_draft:
            raise BrainValidationError(f"A4-D pulse {packet_type} draft does not match the accepted request")


def _require_json_object(value: object, label: str) -> dict[str, Any]:
    if not isinstance(value, Mapping):
        raise BrainValidationError(f"{label} must be a JSON object")
    return dict(value)


def _dump_json(value: object, label: str) -> str:
    try:
        return json.dumps(value, sort_keys=True, separators=(",", ":"), ensure_ascii=True)
    except (TypeError, ValueError) as exc:
        raise BrainValidationError(f"{label} must contain only JSON values") from exc


def _validate_packet_with_schema(raw_packet: str, schema_path: Path, schema_label: str) -> dict[str, Any]:
    try:
        return validate_packet_json(raw_packet, schema_path)
    except UnicodeDecodeError as exc:
        raise BrainValidationError(f"{schema_label} file is not valid UTF-8") from exc


def _entry_exists(path: Path) -> bool:
    return os.path.lexists(os.fspath(path))


def _identity_from_stat(entry_stat: os.stat_result, label: str) -> tuple[int, int]:
    device = getattr(entry_stat, "st_dev", None)
    inode = getattr(entry_stat, "st_ino", None)
    if not isinstance(device, int) or device < 0 or not isinstance(inode, int) or inode <= 0:
        raise OSError(f"{label} identity is unavailable")
    return device, inode


def _lstat_entry(path: Path, label: str) -> os.stat_result:
    try:
        return path.lstat()
    except OSError as exc:
        raise OSError(f"{label} identity cannot be verified") from exc


def _assert_directory_identity(output_dir: Path, expected_identity: tuple[int, int]) -> None:
    directory_stat = _lstat_entry(output_dir, "output directory")
    if not stat.S_ISDIR(directory_stat.st_mode):
        raise OSError("output directory identity changed; output directory is untrusted")
    if _identity_from_stat(directory_stat, "output directory") != expected_identity:
        raise OSError("output directory identity changed; output directory is untrusted")


def _reserve_owned_output(path: Path, payload: bytes) -> _OwnedOutput:
    flags = os.O_CREAT | os.O_EXCL | os.O_RDWR
    flags |= getattr(os, "O_BINARY", 0) | getattr(os, "O_CLOEXEC", 0)
    try:
        descriptor = os.open(path, flags, 0o600)
    except FileExistsError as exc:
        raise BrainValidationError(f"Aster action output target already exists: {path.name}") from exc

    expected_digest = sha256(payload).hexdigest()
    try:
        descriptor_stat = os.fstat(descriptor)
        if not stat.S_ISREG(descriptor_stat.st_mode):
            raise OSError(f"reserved output is not a regular file: {path.name}")
        identity = _identity_from_stat(descriptor_stat, path.name)
    except Exception as exc:
        os.close(descriptor)
        raise OSError(
            f"reserved output identity is unavailable; output directory is untrusted: {path.name}"
        ) from exc

    record = _OwnedOutput(
        path=path,
        descriptor=descriptor,
        identity=identity,
        expected_length=len(payload),
        expected_digest=expected_digest,
    )
    return record


def _require_descriptor(record: _OwnedOutput) -> int:
    if record.descriptor is None:
        raise OSError(f"owned output descriptor is unavailable: {record.path.name}")
    return record.descriptor


def _write_owned_payload(record: _OwnedOutput, payload: bytes) -> None:
    descriptor = _require_descriptor(record)
    digest = sha256()
    offset = 0
    while offset < len(payload):
        written = os.write(descriptor, memoryview(payload)[offset:])
        if written <= 0:
            raise OSError(f"output write made no progress: {record.path.name}")
        if written > len(payload) - offset:
            raise OSError(f"output write reported invalid progress: {record.path.name}")
        digest.update(payload[offset : offset + written])
        offset += written
        record.owned_length = offset
        record.owned_digest = digest.hexdigest()
    os.fsync(descriptor)


def _read_descriptor_payload(descriptor: int, expected_length: int) -> bytes:
    os.lseek(descriptor, 0, os.SEEK_SET)
    chunks: list[bytes] = []
    total = 0
    limit = expected_length + 1
    while total < limit:
        chunk = os.read(descriptor, min(65536, limit - total))
        if not chunk:
            break
        chunks.append(chunk)
        total += len(chunk)
    payload = b"".join(chunks)
    if len(payload) != expected_length:
        raise OSError("output payload length changed during descriptor verification")
    return payload


def _assert_owned_output_identity(record: _OwnedOutput) -> None:
    descriptor = _require_descriptor(record)
    descriptor_stat = os.fstat(descriptor)
    if not stat.S_ISREG(descriptor_stat.st_mode):
        raise OSError(f"output descriptor type changed: {record.path.name}")
    if _identity_from_stat(descriptor_stat, record.path.name) != record.identity:
        raise OSError(f"output descriptor identity changed: {record.path.name}")

    path_stat = _lstat_entry(record.path, record.path.name)
    if not stat.S_ISREG(path_stat.st_mode):
        raise OSError(f"output path identity changed: {record.path.name}")
    if _identity_from_stat(path_stat, record.path.name) != record.identity:
        raise OSError(f"output path identity changed: {record.path.name}")


def _verify_owned_output(
    record: _OwnedOutput,
    output_dir: Path,
    directory_identity: tuple[int, int],
) -> None:
    _assert_directory_identity(output_dir, directory_identity)
    _assert_owned_output_identity(record)
    payload = _read_descriptor_payload(_require_descriptor(record), record.expected_length)
    if sha256(payload).hexdigest() != record.expected_digest:
        raise OSError(f"output payload digest changed during descriptor verification: {record.path.name}")
    _assert_owned_output_identity(record)
    _assert_directory_identity(output_dir, directory_identity)
    record.verified = True


def _close_owned_descriptors(records: list[_OwnedOutput]) -> list[str]:
    errors: list[str] = []
    for record in records:
        if record.descriptor is None:
            continue
        descriptor = record.descriptor
        record.descriptor = None
        try:
            os.close(descriptor)
        except OSError as exc:
            errors.append(f"cannot close {record.path.name}: {exc}")
    return errors


def _cleanup_owned_entries(
    records: list[_OwnedOutput],
    output_dir: Path,
    directory_identity: tuple[int, int],
    created_output_dir: bool,
) -> list[str]:
    errors: list[str] = []
    try:
        _assert_directory_identity(output_dir, directory_identity)
    except OSError as exc:
        return [str(exc)]

    for record in reversed(records):
        try:
            _assert_directory_identity(output_dir, directory_identity)
            path_stat = _lstat_entry(record.path, record.path.name)
            if not stat.S_ISREG(path_stat.st_mode):
                raise OSError("entry is no longer a regular file")
            if _identity_from_stat(path_stat, record.path.name) != record.identity:
                raise OSError("entry identity changed")

            flags = os.O_RDONLY
            flags |= (
                getattr(os, "O_BINARY", 0)
                | getattr(os, "O_CLOEXEC", 0)
                | getattr(os, "O_NONBLOCK", 0)
            )
            descriptor = os.open(record.path, flags)
            try:
                descriptor_stat = os.fstat(descriptor)
                if not stat.S_ISREG(descriptor_stat.st_mode):
                    raise OSError("entry descriptor is no longer a regular file")
                if _identity_from_stat(descriptor_stat, record.path.name) != record.identity:
                    raise OSError("entry descriptor identity changed")

                reopened_stat = _lstat_entry(record.path, record.path.name)
                if not stat.S_ISREG(reopened_stat.st_mode):
                    raise OSError("entry path type changed during cleanup inspection")
                if _identity_from_stat(reopened_stat, record.path.name) != record.identity:
                    raise OSError("entry path identity changed during cleanup inspection")

                payload = _read_descriptor_payload(descriptor, record.owned_length)
                if sha256(payload).hexdigest() != record.owned_digest:
                    raise OSError("entry content changed")

                final_descriptor_stat = os.fstat(descriptor)
                if _identity_from_stat(final_descriptor_stat, record.path.name) != record.identity:
                    raise OSError("entry descriptor identity changed after cleanup inspection")
            finally:
                os.close(descriptor)

            _assert_directory_identity(output_dir, directory_identity)
            final_path_stat = _lstat_entry(record.path, record.path.name)
            if not stat.S_ISREG(final_path_stat.st_mode):
                raise OSError("entry path type changed before cleanup")
            if _identity_from_stat(final_path_stat, record.path.name) != record.identity:
                raise OSError("entry path identity changed before cleanup")
            record.path.unlink()
            _assert_directory_identity(output_dir, directory_identity)
        except OSError as exc:
            errors.append(
                f"cannot safely remove {record.path.name}; output directory is untrusted: {exc}"
            )
            return errors

    if created_output_dir:
        try:
            _assert_directory_identity(output_dir, directory_identity)
            output_dir.rmdir()
        except OSError as exc:
            errors.append(
                "cannot safely remove invocation-created output directory; "
                f"output directory is untrusted: {exc}"
            )
    return errors
