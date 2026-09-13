"""Private descriptor-held transaction for verified JSON artifact output."""

from __future__ import annotations

from collections.abc import Sequence
from dataclasses import dataclass
from hashlib import sha256
import os
from pathlib import Path
import stat

from ringfall_brain.schemas.validator import BrainValidationError


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


def write_verified_artifacts(
    payloads: Sequence[tuple[str, bytes]],
    output_dir: Path,
    operation_label: str,
) -> list[Path]:
    """Reserve, write, and verify an ordered artifact set without clobbering."""
    if not payloads:
        raise BrainValidationError(f"{operation_label} output payload set must not be empty")

    seen: set[str] = set()
    for filename, payload in payloads:
        if (
            not filename
            or filename in {".", ".."}
            or "/" in filename
            or "\\" in filename
            or Path(filename).name != filename
        ):
            raise BrainValidationError(f"{operation_label} output filename must be a leaf name: {filename}")
        if filename in seen:
            raise BrainValidationError(f"{operation_label} output filenames must be unique: {filename}")
        if not isinstance(payload, bytes):
            raise BrainValidationError(f"{operation_label} output payload must be bytes: {filename}")
        seen.add(filename)

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
                f"{operation_label} output target already exists: {', '.join(collisions)}"
            )

        for (filename, payload), final_path in zip(payloads, final_paths, strict=True):
            _assert_directory_identity(output_dir, directory_identity)
            owned_outputs.append(_reserve_owned_output(final_path, payload, operation_label))
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
        cleanup_errors = (
            _cleanup_owned_entries(
                owned_outputs,
                output_dir,
                directory_identity,
                created_output_dir,
            )
            if directory_identity is not None
            else []
        )
        recovery_errors = [*close_errors, *cleanup_errors]
        if recovery_errors:
            details = "; ".join(recovery_errors)
            raise OSError(
                f"{operation_label} output cleanup failed; output directory is untrusted: {details}"
            ) from exc
        raise

    close_errors = _close_owned_descriptors(owned_outputs)
    if close_errors:
        close_details = "; ".join(close_errors)
        cleanup_errors = _cleanup_owned_entries(
            owned_outputs,
            output_dir,
            directory_identity,
            created_output_dir,
        )
        if cleanup_errors:
            cleanup_details = "; ".join(cleanup_errors)
            raise OSError(
                f"{operation_label} output close failed: {close_details}; "
                f"output cleanup failed; output directory is untrusted: {cleanup_details}"
            )
        raise OSError(f"{operation_label} output close failed: {close_details}")
    return final_paths


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


def _reserve_owned_output(path: Path, payload: bytes, operation_label: str) -> _OwnedOutput:
    flags = os.O_CREAT | os.O_EXCL | os.O_RDWR
    flags |= getattr(os, "O_BINARY", 0) | getattr(os, "O_CLOEXEC", 0)
    try:
        descriptor = os.open(path, flags, 0o600)
    except FileExistsError as exc:
        raise BrainValidationError(f"{operation_label} output target already exists: {path.name}") from exc

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

    return _OwnedOutput(
        path=path,
        descriptor=descriptor,
        identity=identity,
        expected_length=len(payload),
        expected_digest=expected_digest,
    )


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
