# Ringfall Contracts

`src/ringfall-contracts/` is the canonical home for Ringfall external artifact contracts.

W1-S1 created layout and versioning notes only. W1-S2 introduced the first core packet schema drafts under `schemas/packets/`. W1-S3 added institution and council packet schema drafts for C1-E. W1-S4 added run, cognition, action, state-diff, claim, and memory-update schema drafts. W1-S5 adds CostEvent and EvalSummary schema drafts for C1-H. W1-S6 adds contract examples and dev-only schema validation tooling for C1-I/C1-J.

JSON Schema is canonical for external artifact validation. W1 external artifact schemas use JSON Schema Draft 2020-12 and `schema_version` `0.1`.

W1 schemas are draft contract surfaces. Structural validation is available through `tools/schema_check.py` and manifest-backed examples under `examples/`. Deeper runtime authority, hidden-truth, replay, cost/eval, and memory validation remains gated to later implementation work.

Future execution-impacting model outputs must become strict typed packets before they can affect the simulation. Contracts must not allow direct LLM world-state mutation or authoritative truth edits.

The approved Refinery direction does not replace JSON Schema. JSON Schema remains the external artifact shape contract; future Refinery models may add bounded formal intervention gates for named candidate-output families after schema validation and before Core authority validation. A formal gate must return `valid`, `invalid`, `repairable`, `unsupported`, or `fallback` evidence and must not directly write runtime state. Full-world formal validation is explicitly out of scope.

## Runtime Next Steps Remain Blocked

C#/.NET runtime code, Python runtime code, provider/model behavior, Unity work, scenarios, generated artifacts, eval runner logic, runtime cost collection, and simulation logic remain out of scope.

Refinery runtime integration, formal gate CI, bridge mapping, and generated formal evidence are also out of scope until a later reviewed implementation gate opens them.
