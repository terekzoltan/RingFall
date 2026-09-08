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

Wave 4 A4-E deterministic Aster candidate smoke:

```powershell
$env:PYTHONPATH="src/ringfall-brain"
$outputDir = Join-Path $env:TEMP "ringfall-a4e-candidates"
python -m ringfall_brain.cli mock aster-actions --context "src/ringfall-brain/examples/aster-a1-context.example.json" --pulse "src/ringfall-brain/examples/aster-a1-pulse.example.json" --pulse-schema "src/ringfall-contracts/schemas/packets/avatar-pulse-packet.schema.json" --tool-schema "src/ringfall-contracts/schemas/packets/tool-action-request.schema.json" --work-order-schema "src/ringfall-contracts/schemas/packets/work-order-request.schema.json" --output-dir $outputDir
```

The command consumes the accepted A4-D A1 context and pulse fixtures and writes exactly `tool-action-request.json` and `work-order-request.json`. Its compact JSON summary has exactly `candidate_only`, `packet_ids`, `schema_valid`, `status`, and `written_files`; `candidate_only` is always `true` for this path.

Output is fail-closed and no-clobber. Both candidates pass source and schema validation before output begins. The command records the real output-directory identity, reserves both exact final names with exclusive creation, and retains both read/write descriptors through complete-write loops, `fsync`, same-descriptor read-back, exact length and SHA-256 checks, and descriptor/path identity checks. Either final filename already existing causes the command to fail without replacing or deleting it. Final names can be visible while an invocation is still writing and verifying them; consumers must treat only a successful command result as verified candidate output.

On a handled failure, cleanup deletes an invocation-owned entry only while the recorded output-directory identity, file identity, and observed owned bytes still match. An observed replacement, same-entry mutation, directory replacement, unavailable identity primitive, or ambiguous check stops cleanup through that directory, preserves the observed content, and reports the directory as untrusted. Exclusive reservation prevents cooperative A4-E invocations from clobbering each other, but portable pathname cleanup cannot atomically defeat an uncooperative writer racing the final compare-and-unlink window. A successful return is a descriptor-held verification point, not a perpetual immutability guarantee. A process crash can leave reserved files; a later invocation treats those stale entries as collisions rather than overwriting them.

These files are deterministic dev/mock proposals. Schema validity does not mean that Core authorized or executed either request. A4-F/Core remains the authority-validation boundary, and A4-G owns later execution and state-diff behavior. This command makes no provider or network call and writes no trace, cost, result, or state artifact.

This package does not provide an installed console script, package install contract, CI lane, real provider/API call path, prompt runtime, Core mutation path, Unity/client integration, Refinery/solver runtime, or committed generated runtime artifacts.
