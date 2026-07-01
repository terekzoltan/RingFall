# Ringfall Contract Examples

These examples are W1-S6 contract fixtures for C1-I/C1-J validation. They are illustrative JSON Schema fixtures, not canonical generated run artifacts.

Placeholder IDs, refs, and artifact URIs are contract references only. They are not real run evidence, provider output, simulation output, or replay artifacts.

The fixture manifest is `manifest.json`. It records each fixture path, target schema path, expected result, validation layer, and reason code for invalid fixtures. `tools/schema_check.py` uses the manifest as the source of truth; fixture-to-schema mapping is not inferred from filenames alone.

## Fixture Layout

- `valid/`: exactly one minimal valid fixture for each current W1 schema.
- `invalid/`: targeted invalid fixtures for W1-routed schema and semantic validation debt.

Invalid fixtures are intentionally small and focused. Each invalid fixture should exercise one reason code.

Current W1-S6 static fixtures cover the routed generic schema categories, including type/schema-version constants, required fields, action conditionals, work-order targeting, hidden-effect completeness, invalid enums, numeric bounds, empty arrays where forbidden, malformed IDs/refs, and scoped source-ref vocabulary integrity.

## Validation Scope

The schema checker is dev-only contract tooling. It does not implement a runtime validation pipeline, provider/model routing, eval runner behavior, world simulation behavior, scenario generation, or canonical artifact production.

It also does not implement Refinery formal intervention gates. Future Refinery fixtures, if added, must live behind a reviewed formal-family gate and must clearly distinguish `proved_by_refinery`, `guarded_by_core_validator`, `schema_only`, `observability_only`, and `unsupported` claims.

Static semantic checks are limited to W1-S2 through W1-S5 routed validation debt that can be inspected inside example JSON files.

`source_ref_vocabulary` validates `ref_type` only inside known reference structures such as `source_refs`, `system_refs`, `artifact_refs`, and explicit `*_ref` fields that use source-reference objects. It does not define a global `ref_type` namespace for arbitrary fixture objects. Bundle-level reference existence and cross-artifact consistency remain deferred to runtime/artifact bundle validation.

## Deferred Debt

Some routed validation debt remains outside W1-S6 static fixture/tool scope and should be reviewed later when runtime or richer artifact context exists:

Deferred runtime/artifact-context debts are routed to the W1-S7/C1-K contract handoff classification gate, then to the first runtime/artifact bundle validation gate before canonical evidence. Track B owns packet/state/memory semantic review, Track D owns CostEvent provider/model evidence semantic review, and Track E owns eval/replay validation tooling.

- recommended-order traceability beyond fixture-local structural fields;
- institution-order runtime authority against real seats and institutions;
- macro-order policy-level consequences beyond bounded fixture fields;
- full StateDiff path and visibility consistency against an actual state tree;
- CostEvent provider/model evidence consistency against real provider request records;
- missing trace refs across a real artifact bundle;
- complete hidden-leak detection against prompt/context artifacts;
- complete rumor/fact contamination detection against claim graph state.
- positive memory examples for rumor, belief, official_line, and withheld_item, routed to the first role/memory implementation or Wave 2/3 prompt-memory fixture expansion gate with Track C ownership and Track B/E support if schemas, examples, or tooling are touched;
- a connected manifest-to-trace-to-state/eval artifact bundle graph, routed to the first artifact bundle validation or Track A loader-planning gate with Track A ownership and Track B/E support if contract validation is touched;
- real CostEvent provider/model evidence reconciliation against provider request/response records, routed to the first Track D provider/runtime implementation or runtime artifact bundle validation gate;
- EvalEvent and eval-runner behavior decisions, routed to later Track E eval/replay schema and runtime artifact validation gates.
- bounded Refinery formal intervention models and differential harnesses, routed to the later formal-family gates documented in `docs/design/Formal-Intervention-Gates-Refinery.md`.
