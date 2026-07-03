#!/usr/bin/env python3
"""Deterministic proof cases for the Ringfall CI hygiene guard."""

from __future__ import annotations

from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[1]
if str(ROOT) not in sys.path:
    sys.path.insert(0, str(ROOT))

import tools.ci_hygiene_check as guard  # noqa: E402


VALID_WORKFLOW = """name: Contract CI

on:
  push:
  pull_request:

permissions:
  contents: read

jobs:
  schema-check:
    name: Schema checker
    runs-on: ubuntu-latest

    steps:
      - name: Checkout
        uses: actions/checkout@v4

      - name: Set up Python
        uses: actions/setup-python@v5
        with:
          python-version: "3.12"

      - name: Install dev requirements
        run: python -m pip install -r requirements-dev.txt

      - name: Run CI hygiene guard
        run: python tools/ci_hygiene_check.py

      - name: Run contract schema checker
        run: python tools/schema_check.py
"""

HEADER = """name: Contract CI

on:
  push:
  pull_request:

permissions:
  contents: read
"""

NO_RUN_JOB = HEADER + """
jobs:
  schema-check:
    runs-on: ubuntu-latest
    steps:
      - name: Checkout
        uses: actions/checkout@v4
"""


def workflow_failures(text: str) -> list[str]:
    failures: list[str] = []
    original_read_text = guard.read_text

    def fake_read_text(path: Path, read_failures: list[str]) -> str:
        if path == guard.WORKFLOW_PATH:
            return text
        return original_read_text(path, read_failures)

    guard.read_text = fake_read_text
    try:
        guard.check_workflow(failures)
    finally:
        guard.read_text = original_read_text
    return failures


def scalar_spoof(indicator: str) -> str:
    return HEADER + f"""
jobs:
  schema-check:
    runs-on: ubuntu-latest
    steps:
      - name: fake scalar
        env: {indicator}
          run: python -m pip install -r requirements-dev.txt
          run: python tools/ci_hygiene_check.py
          run: python tools/schema_check.py
"""


def main() -> int:
    fail_cases = {
        "job_level_uses": HEADER + """
jobs:
  reusable:
    uses: owner/repo/.github/workflows/reused.yml@main
  schema-check:
    runs-on: ubuntu-latest
    steps:
      - run: python -m pip install -r requirements-dev.txt
      - run: python tools/ci_hygiene_check.py
      - run: python tools/schema_check.py
""",
        "secrets_inherit": HEADER + """
jobs:
  schema-check:
    runs-on: ubuntu-latest
    secrets: inherit
    steps:
      - run: python -m pip install -r requirements-dev.txt
      - run: python tools/ci_hygiene_check.py
      - run: python tools/schema_check.py
""",
        "active_secrets_mapping": HEADER + """
jobs:
  schema-check:
    runs-on: ubuntu-latest
    steps:
      - name: forbidden mapping
        secrets:
          name: value
      - run: python -m pip install -r requirements-dev.txt
      - run: python tools/ci_hygiene_check.py
      - run: python tools/schema_check.py
""",
        "missing_permissions": VALID_WORKFLOW.replace("permissions:\n  contents: read\n\n", ""),
        "permissions_contents_write": VALID_WORKFLOW.replace("contents: read", "contents: write"),
        "permissions_missing_contents": VALID_WORKFLOW.replace("  contents: read\n", "  actions: read\n"),
        "top_level_steps": HEADER + """
steps:
  - run: python -m pip install -r requirements-dev.txt
  - run: python tools/ci_hygiene_check.py
  - run: python tools/schema_check.py

jobs:
  schema-check:
    runs-on: ubuntu-latest
    steps:
      - name: Checkout
        uses: actions/checkout@v4
""",
        "nested_with_run": HEADER + """
jobs:
  schema-check:
    runs-on: ubuntu-latest
    steps:
      - name: fake install
        with:
          run: python -m pip install -r requirements-dev.txt
      - name: fake guard
        with:
          run: python tools/ci_hygiene_check.py
      - name: fake schema
        with:
          run: python tools/schema_check.py
""",
        "env_run": HEADER + """
jobs:
  schema-check:
    runs-on: ubuntu-latest
    steps:
      - name: fake install
        env:
          run: python -m pip install -r requirements-dev.txt
      - name: fake guard
        env:
          run: python tools/ci_hygiene_check.py
      - name: fake schema
        env:
          run: python tools/schema_check.py
""",
        "deep_nested_run": HEADER + """
jobs:
  schema-check:
    runs-on: ubuntu-latest
    steps:
      - name: fake install
        with:
          nested:
            run: python -m pip install -r requirements-dev.txt
      - name: fake guard
        with:
          nested:
            run: python tools/ci_hygiene_check.py
      - name: fake schema
        with:
          nested:
            run: python tools/schema_check.py
""",
        "scalar_pipe": scalar_spoof("|"),
        "scalar_pipe_chomp_strip": scalar_spoof("|-"),
        "scalar_pipe_chomp_keep": scalar_spoof("|+"),
        "scalar_folded": scalar_spoof(">"),
        "scalar_folded_chomp_strip": scalar_spoof(">-"),
        "scalar_folded_chomp_keep": scalar_spoof(">+"),
        "apikey": VALID_WORKFLOW + "# apikey\n",
    }

    pass_cases = {
        "current_workflow_shape": VALID_WORKFLOW,
        "current_workflow_file": guard.WORKFLOW_PATH.read_text(encoding="utf-8"),
        "commented_secrets_is_ignored": VALID_WORKFLOW + "# secrets: inherit\n",
        "scalar_secrets_is_ignored": VALID_WORKFLOW + "\n# proof scalar\nenv: |\n  secrets: inherit\n",
        "second_job_steps_valid": HEADER + """
jobs:
  first-job:
    runs-on: ubuntu-latest
    steps:
      - name: Checkout
        uses: actions/checkout@v4
  second-job:
    runs-on: ubuntu-latest
    steps:
      - run: python -m pip install -r requirements-dev.txt
      - run: python tools/ci_hygiene_check.py
      - run: python tools/schema_check.py
""",
    }

    failed = False
    for name, text in fail_cases.items():
        failures = workflow_failures(text)
        if failures:
            print(f"PASS {name}: rejected")
            print(f"  FAIL {failures[0]}")
        else:
            failed = True
            print(f"FAIL {name}: unexpectedly accepted")

    for name, text in pass_cases.items():
        failures = workflow_failures(text)
        if failures:
            failed = True
            print(f"FAIL {name}: unexpectedly rejected")
            for failure in failures:
                print(f"  FAIL {failure}")
        else:
            print(f"PASS {name}: accepted")

    return 1 if failed else 0


if __name__ == "__main__":
    raise SystemExit(main())
