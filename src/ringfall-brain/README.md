# Ringfall Brain

This package is the Wave 3 brain skeleton for local CLI, model-policy fixture checks, deterministic mock packet output, and local schema validation.

Supported invocation contracts for this Wave 3 scaffold:

- From the package root: `cd src/ringfall-brain`; `python -m ringfall_brain.cli --help`
- From the repository root with explicit import context: `$env:PYTHONPATH="src/ringfall-brain"; python -m ringfall_brain.cli --help`

Step 2 local mock/validation smoke:

- From the package root: `python -m ringfall_brain.cli mock pulse --schema "../ringfall-contracts/schemas/packets/avatar-pulse-packet.schema.json"`
- From the repository root: `$env:PYTHONPATH="src/ringfall-brain"; python -m ringfall_brain.cli mock pulse --schema "src/ringfall-contracts/schemas/packets/avatar-pulse-packet.schema.json"`

Step 3 local shell/trace smoke:

- OpenRouter shell check: `$env:PYTHONPATH="src/ringfall-brain"; python -m ringfall_brain.cli provider openrouter check-env`
- Mock cognition without writing files: `$env:PYTHONPATH="src/ringfall-brain"; python -m ringfall_brain.cli mock cognition --packet-schema "src/ringfall-contracts/schemas/packets/avatar-pulse-packet.schema.json" --cognition-schema "src/ringfall-contracts/schemas/traces/cognition-trace.schema.json" --cost-schema "src/ringfall-contracts/schemas/traces/cost-event.schema.json"`
- Mock cognition with explicit dev/mock artifacts: add `--output-dir <temp-or-local-output-dir>`.

The OpenRouter shell check only validates process environment shape. It does not read `.env` files, make network calls, create a provider client, or print credentials. The default model metadata is `openrouter/manual-unconfigured` when `OPENROUTER_MODEL` is absent.

Mock cognition trace/cost output is dev/mock evidence only, not canonical provider evidence. Files are written only when `--output-dir` is explicitly supplied.

This package does not provide an installed console script, package install contract, CI lane, real provider/API call path, prompt runtime, Core mutation path, Unity/client integration, Refinery/solver runtime, or committed generated runtime artifacts.
