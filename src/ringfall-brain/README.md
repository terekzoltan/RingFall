# Ringfall Brain

This package is the Wave 3 brain skeleton for local CLI, model-policy fixture checks, deterministic mock packet output, and local schema validation.

Supported invocation contracts for this Wave 3 scaffold:

- From the package root: `cd src/ringfall-brain`; `python -m ringfall_brain.cli --help`
- From the repository root with explicit import context: `$env:PYTHONPATH="src/ringfall-brain"; python -m ringfall_brain.cli --help`

Step 2 local mock/validation smoke:

- From the package root: `python -m ringfall_brain.cli mock pulse --schema "../ringfall-contracts/schemas/packets/avatar-pulse-packet.schema.json"`
- From the repository root: `$env:PYTHONPATH="src/ringfall-brain"; python -m ringfall_brain.cli mock pulse --schema "src/ringfall-contracts/schemas/packets/avatar-pulse-packet.schema.json"`

This package does not provide an installed console script, package install contract, CI lane, provider/API call path, credential handling, prompt runtime, trace writer, cost writer, or generated runtime artifacts.
