#!/usr/bin/env python3
"""Check Ringfall contract CI hygiene boundaries."""

from __future__ import annotations

from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
WORKFLOW_PATH = ROOT / ".github" / "workflows" / "contract-ci.yml"
GITIGNORE_PATH = ROOT / ".gitignore"

REQUIRED_RUN_ORDER = [
    "python -m pip install -r requirements-dev.txt",
    "python tools/ci_hygiene_check.py",
    "python tools/schema_check.py",
]

ALLOWED_USES = {
    "actions/checkout@v4",
    "actions/setup-python@v5",
}

HARD_BAN_TERMS = {
    "secret reference": "secrets.",
    "OpenRouter credential/runtime scope": "openrouter",
    "API key credential": "api_key",
    "API key credential spelling": "api-key",
    "API key compact credential spelling": "apikey",
    "token credential": "token",
    "password credential": "password",
    "provider credential/runtime scope": "provider",
    "model credential/runtime scope": "model",
    "OpenCode session scope": "opencode",
    "Unity/client CI scope": "unity",
    "dotnet runtime CI scope": "dotnet",
    "C# runtime CI scope": "csharp",
    "C# runtime CI scope spelling": "c#",
    "Python brain runtime scope": "brain",
    "scenario/replay runner scope": "scenario",
    "simulation scope": "simulation",
    "coverage hard-gate scope": "coverage",
    "Refinery/formal CI scope": "refinery",
    "solver/formal CI scope": "solver",
    "eval-runner scope": "eval-runner",
    "artifact upload action": "upload-artifact",
}

REQUIRED_ABSENCE_PATHS = {
    "private FAL state": ".fal",
    "private OpenCode state": ".opencode",
    "private swarm state": ".swarm",
    "private kontext state": "kontext",
    "generated run artifacts": "data/runs",
    "generated replay artifacts": "data/replays",
    "generated trace artifacts": "data/traces",
    "generated eval artifacts": "data/eval",
}

REQUIRED_GITIGNORE_PATTERNS = {
    ".fal/",
    ".opencode/",
    ".opencode-router/",
    ".swarm/",
    "kontext/",
    ".env",
    ".env.*",
    "configs/local*.yaml",
    "configs/*.local.yaml",
    "configs/*secret*",
    "configs/*private*",
    "data/runs/",
    "data/replays/",
    "data/traces/",
    "data/eval/",
    "__pycache__/",
    "*.py[cod]",
    "client/**/Library/",
    "client/**/Temp/",
    "client/**/Obj/",
    "client/**/Logs/",
    "client/**/UserSettings/",
}


def rel(path: Path) -> str:
    try:
        return path.relative_to(ROOT).as_posix()
    except ValueError:
        return path.as_posix()


def read_text(path: Path, failures: list[str]) -> str:
    if not path.is_file():
        failures.append(f"missing file: {rel(path)}")
        return ""
    return path.read_text(encoding="utf-8")


def indent_width(line: str) -> int:
    return len(line) - len(line.lstrip(" "))


def parse_scalar_body_until(lines: list[str], start_index: int, scalar_indent: int) -> int:
    index = start_index + 1
    while index < len(lines):
        line = lines[index]
        stripped = line.strip()
        if stripped and not stripped.startswith("#") and indent_width(line) <= scalar_indent:
            break
        index += 1
    return index


def has_block_scalar_value(line: str) -> bool:
    value = line.split(":", 1)[1].strip() if ":" in line else ""
    return value in {"|", "|-", "|+", ">", ">-", ">+"}


def yaml_value(line: str, key: str) -> str | None:
    stripped = line.strip()
    if stripped.startswith("- "):
        stripped = stripped[2:].strip()
    prefix = f"{key}:"
    if not stripped.startswith(prefix):
        return None
    return stripped.split(":", 1)[1].strip().strip('"\'')


def yaml_key_value(line: str) -> tuple[str, str] | None:
    stripped = line.strip()
    if stripped.startswith("- "):
        stripped = stripped[2:].strip()
    if ":" not in stripped:
        return None
    key, value = stripped.split(":", 1)
    return key.strip(), value.strip().strip('"\'')


def top_level_mapping_values(text: str, mapping_key: str) -> dict[str, str]:
    values: dict[str, str] = {}
    lines = text.splitlines()
    in_mapping = False
    child_indent: int | None = None
    index = 0
    while index < len(lines):
        line = lines[index]
        stripped = line.strip()
        if not stripped or stripped.startswith("#"):
            index += 1
            continue
        current_indent = indent_width(line)

        if in_mapping and current_indent == 0:
            in_mapping = False
            child_indent = None

        if stripped == f"{mapping_key}:" and current_indent == 0:
            in_mapping = True
            child_indent = None
            index += 1
            continue

        if not in_mapping:
            if has_block_scalar_value(line):
                index = parse_scalar_body_until(lines, index, current_indent)
            else:
                index += 1
            continue

        if child_indent is None:
            child_indent = current_indent

        if current_indent == child_indent:
            key_value = yaml_key_value(line)
            if key_value is not None:
                key, value = key_value
                values[key] = value

        if has_block_scalar_value(line):
            index = parse_scalar_body_until(lines, index, current_indent)
        else:
            index += 1
    return values


def active_yaml_key_lines(text: str, key: str) -> list[int]:
    line_numbers: list[int] = []
    lines = text.splitlines()
    index = 0
    while index < len(lines):
        line = lines[index]
        stripped = line.strip()
        if not stripped or stripped.startswith("#"):
            index += 1
            continue
        key_value = yaml_key_value(line)
        if key_value is not None and key_value[0] == key:
            line_numbers.append(index + 1)
        if has_block_scalar_value(line):
            index = parse_scalar_body_until(lines, index, indent_width(line))
        else:
            index += 1
    return line_numbers


def job_level_values(text: str, key: str) -> list[tuple[int, str]]:
    values: list[tuple[int, str]] = []
    lines = text.splitlines()
    in_jobs = False
    jobs_indent = -1
    in_job = False
    job_indent = -1
    job_child_indent: int | None = None
    index = 0
    while index < len(lines):
        line = lines[index]
        stripped = line.strip()
        if not stripped or stripped.startswith("#"):
            index += 1
            continue
        current_indent = indent_width(line)

        if in_job and current_indent <= job_indent:
            in_job = False
            job_child_indent = None

        if in_jobs and current_indent <= jobs_indent:
            in_jobs = False
            in_job = False
            job_child_indent = None

        if stripped == "jobs:" and current_indent == 0:
            in_jobs = True
            jobs_indent = current_indent
            in_job = False
            job_child_indent = None
            index += 1
            continue

        if not in_jobs:
            if has_block_scalar_value(line):
                index = parse_scalar_body_until(lines, index, current_indent)
            else:
                index += 1
            continue

        if not in_job and current_indent > jobs_indent and stripped.endswith(":"):
            in_job = True
            job_indent = current_indent
            job_child_indent = None
            index += 1
            continue

        if not in_job:
            if has_block_scalar_value(line):
                index = parse_scalar_body_until(lines, index, current_indent)
            else:
                index += 1
            continue

        if job_child_indent is None:
            job_child_indent = current_indent

        if current_indent == job_child_indent:
            value = yaml_value(line, key)
            if value is not None:
                values.append((index + 1, value))

        if has_block_scalar_value(line):
            index = parse_scalar_body_until(lines, index, current_indent)
        else:
            index += 1
    return values


def active_step_values(text: str, key: str) -> list[tuple[int, str]]:
    values: list[tuple[int, str]] = []
    lines = text.splitlines()
    in_jobs = False
    jobs_indent = -1
    in_job = False
    job_indent = -1
    in_steps = False
    steps_indent = -1
    in_step_item = False
    step_item_indent = -1
    step_child_indent: int | None = None
    step_item_starts_nested_map = False
    index = 0
    while index < len(lines):
        line = lines[index]
        line_number = index + 1
        stripped = line.strip()
        if not stripped or stripped.startswith("#"):
            index += 1
            continue
        current_indent = indent_width(line)

        if in_steps and current_indent <= steps_indent and not stripped.startswith("-"):
            in_steps = False
            in_step_item = False
            step_child_indent = None
            step_item_starts_nested_map = False

        if in_job and current_indent <= job_indent:
            in_job = False
            in_steps = False
            in_step_item = False
            step_child_indent = None
            step_item_starts_nested_map = False

        if in_jobs and current_indent <= jobs_indent:
            in_jobs = False
            in_job = False
            in_steps = False
            in_step_item = False
            step_child_indent = None
            step_item_starts_nested_map = False

        if stripped == "jobs:" and current_indent == 0:
            in_jobs = True
            jobs_indent = current_indent
            in_job = False
            in_steps = False
            in_step_item = False
            step_child_indent = None
            step_item_starts_nested_map = False
            index += 1
            continue

        if not in_jobs:
            if has_block_scalar_value(line):
                index = parse_scalar_body_until(lines, index, current_indent)
            else:
                index += 1
            continue

        if not in_job and current_indent > jobs_indent and stripped.endswith(":"):
            in_job = True
            job_indent = current_indent
            in_steps = False
            in_step_item = False
            step_child_indent = None
            step_item_starts_nested_map = False
            index += 1
            continue

        if not in_job:
            if has_block_scalar_value(line):
                index = parse_scalar_body_until(lines, index, current_indent)
            else:
                index += 1
            continue

        if stripped == "steps:" and current_indent > job_indent:
            in_steps = True
            steps_indent = current_indent
            in_step_item = False
            step_child_indent = None
            step_item_starts_nested_map = False
            index += 1
            continue

        if not in_steps:
            if has_block_scalar_value(line):
                index = parse_scalar_body_until(lines, index, current_indent)
            else:
                index += 1
            continue

        if current_indent == steps_indent + 2 and stripped.startswith("-"):
            in_step_item = True
            step_item_indent = current_indent
            step_child_indent = None
            step_item_starts_nested_map = stripped[2:].strip().endswith(":")
            value = yaml_value(line, key)
            if value is not None:
                values.append((line_number, value))
            if has_block_scalar_value(line):
                index = parse_scalar_body_until(lines, index, current_indent)
            else:
                index += 1
            continue

        if in_step_item and current_indent <= step_item_indent:
            in_step_item = False
            step_child_indent = None
            step_item_starts_nested_map = False

        if in_step_item and current_indent > step_item_indent:
            if step_item_starts_nested_map:
                if has_block_scalar_value(line):
                    index = parse_scalar_body_until(lines, index, current_indent)
                else:
                    index += 1
                continue
            if step_child_indent is None:
                step_child_indent = current_indent
            if current_indent != step_child_indent:
                if has_block_scalar_value(line):
                    index = parse_scalar_body_until(lines, index, current_indent)
                else:
                    index += 1
                continue
            value = yaml_value(line, key)
            if value is not None:
                values.append((line_number, value))
            if has_block_scalar_value(line):
                index = parse_scalar_body_until(lines, index, current_indent)
            else:
                index += 1
            continue

        index += 1
    return values


def check_workflow(failures: list[str]) -> None:
    text = read_text(WORKFLOW_PATH, failures)
    if not text:
        return

    workflow = rel(WORKFLOW_PATH)
    lowered = text.lower()
    normalized = "".join(lowered.split())

    permissions = top_level_mapping_values(text, "permissions")
    if not permissions:
        failures.append(f"{workflow}: missing required workflow invariant: top-level permissions block")
    elif permissions.get("contents") != "read":
        failures.append(f"{workflow}: required workflow invariant not met: permissions contents: read")

    run_commands = active_step_values(text, "run")
    run_values = [value for _line_number, value in run_commands]
    run_positions: list[int] = []
    for command in REQUIRED_RUN_ORDER:
        try:
            run_positions.append(run_values.index(command))
        except ValueError:
            failures.append(f"{workflow}: missing required active run command: {command}")
    if len(run_positions) == len(REQUIRED_RUN_ORDER) and run_positions != sorted(run_positions):
        expected = " -> ".join(REQUIRED_RUN_ORDER)
        actual = " -> ".join(run_values)
        failures.append(f"{workflow}: required active run commands are out of order: expected {expected}; actual {actual}")

    for line_number, action in active_step_values(text, "uses"):
        if action not in ALLOWED_USES:
            failures.append(f"{workflow}:{line_number}: disallowed action in uses: {action}")

    for line_number, reusable_workflow in job_level_values(text, "uses"):
        failures.append(f"{workflow}:{line_number}: disallowed job-level reusable workflow uses: {reusable_workflow}")

    for line_number in active_yaml_key_lines(text, "secrets"):
        failures.append(f"{workflow}:{line_number}: disallowed active secrets mapping")

    if "secrets[" in normalized:
        failures.append(f"{workflow}: banned term for bracket-style secret reference: secrets[")

    for policy, term in HARD_BAN_TERMS.items():
        if term.lower() in lowered:
            failures.append(f"{workflow}: banned term for {policy}: {term}")

    for policy, path in REQUIRED_ABSENCE_PATHS.items():
        if path.lower() in lowered:
            failures.append(f"{workflow}: banned private/local path for {policy}: {path}")


def check_gitignore(failures: list[str]) -> None:
    text = read_text(GITIGNORE_PATH, failures)
    if not text:
        return

    patterns = {
        line.strip()
        for line in text.splitlines()
        if line.strip() and not line.lstrip().startswith("#")
    }
    missing = sorted(REQUIRED_GITIGNORE_PATTERNS - patterns)
    for pattern in missing:
        failures.append(f"{rel(GITIGNORE_PATH)}: missing required private/local ignore pattern: {pattern}")


def main() -> int:
    failures: list[str] = []
    check_workflow(failures)
    check_gitignore(failures)

    if failures:
        print("CI hygiene guard failed:")
        for failure in failures:
            print(f"FAIL {failure}")
        return 1

    print("CI hygiene guard passed: contract workflow and private/local ignore boundaries are preserved")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
