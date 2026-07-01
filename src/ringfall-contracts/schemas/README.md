# Ringfall Schema Groups

This directory groups Ringfall JSON Schema files. W1-S1 created the group layout; W1-S2 introduced the first packet schema drafts. W1-S3 added the C1-E institution and council packet schema drafts. W1-S4 adds trace, state, and memory schema drafts for C1-F/C1-G. W1-S5 adds Track D CostEvent and Track E EvalSummary schema drafts for C1-H cost/eval readiness. W1-S6 adds manifest-backed contract examples and dev-only schema validation tooling for C1-I/C1-J.

Group purposes:

- `state/`: future world and state export schemas.
- `packets/`: execution-impacting typed packet schemas.
- `traces/`: run manifest, cognition trace, action trace, and cost event schemas. W1-S4 keeps `RunManifest` here as an artifact manifest surface without creating a separate manifest group.
- `memory/`: memory, belief, and claim schemas.
- `eval/`: evaluation summary schemas. EvalEvent remains deferred to later eval schema work.
- `config/`: future exported configuration schema surfaces.

W1-S2 packet schemas:

- `packets/avatar-pulse-packet.schema.json`
- `packets/scene-action-packet.schema.json`
- `packets/work-order-request.schema.json`
- `packets/tool-action-request.schema.json`
- `packets/execution-result.schema.json`

W1-S3 packet schemas:

- `packets/institution-brief.schema.json`
- `packets/institution-order.schema.json`
- `packets/council-doctrine-packet.schema.json`

W1-S4 trace, state, and memory schemas:

- `traces/run-manifest.schema.json`
- `traces/cognition-trace.schema.json`
- `traces/action-trace.schema.json`
- `state/state-diff.schema.json`
- `memory/claim-record.schema.json`
- `memory/memory-update.schema.json`

W1-S5 C1-H cost/eval schemas:

- `traces/cost-event.schema.json`
- `eval/eval-summary.schema.json`

The existing `.gitkeep` files may remain beside schema files. Contract examples live under `src/ringfall-contracts/examples/`; dev-only validation tooling lives at `tools/schema_check.py`.

W1-S6 static examples and `tools/schema_check.py` cover packet-type constants, schema-version constants, action-type conditional requirements, work-order targeting, hidden-effect completeness, institution brief withholding boundaries, doctrine-shift numeric bounds, emergency-measure bounded duration/sunset structural constraints, empty run tracks, fixture-local MemoryUpdate hidden-leak and rumor/fact flags, scoped source_ref ref_type vocabulary integrity, ActionTrace.validation vs execution_status consistency, RunManifest duplicate track IDs, CostEvent token/cost bounds, CostEvent free/fallback fixture consistency, EvalSummary gate/count consistency, EvalSummary hard-gate failure consistency, empty EvalSummary by_level arrays, missing EvalSummary gate reasons for held runs, malformed IDs/refs, invalid enums, and out-of-range numeric values.

The `source_ref_vocabulary` semantic check is scoped to known reference structures and explicit source-reference object fields. It does not make every object property named `ref_type` part of a global namespace.

Refinery formal intervention gates are a later, separate consistency layer. They must not be represented as schema coverage unless a named formal family, fixtures, bridge mapper, and differential harness exist. JSON Schema validates artifact shape; Refinery, when introduced, validates only bounded candidate intervention graphs; Core remains authoritative for runtime mutation.

Remaining deferred validation debt is routed to W1-S7/C1-K contract handoff classification and later runtime/artifact validation gates: recommended-order traceability, institution-order runtime authority fields, macro-order policy-level consequences, invalid/quarantined MemoryUpdate semantics beyond fixture-local flags, full StateDiff path and visibility consistency against an actual state tree, CostEvent cognition/cost ref integrity, CostEvent provider/model evidence consistency, CostEvent cost estimate status consistency against real provider records, missing trace refs across an artifact bundle, and complete hidden-leak or rumor/fact contamination detection against prompt/context and claim graph artifacts.

Post-W1 C1-K routed concerns remain active for later gates: Track C owns positive memory examples for rumor, belief, official_line, and withheld_item at the first role/memory implementation or Wave 2/3 prompt-memory fixture expansion gate; Track A owns a connected artifact bundle graph example at the first artifact bundle validation or loader-planning gate; Track D owns real CostEvent provider/model evidence reconciliation at the first provider/runtime or runtime artifact bundle validation gate; Track E owns the later EvalEvent/eval-runner decision at eval/replay schema and runtime artifact validation gates.

The formal intervention gate roadmap is tracked in `docs/design/Formal-Intervention-Gates-Refinery.md` and the Combined Execution Sequencing Plan. It is not part of W1 schema acceptance and does not approve a full formal world model.
