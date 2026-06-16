# Ringfall Schema Groups

This directory groups Ringfall JSON Schema files. W1-S1 created the group layout; W1-S2 introduced the first packet schema drafts. W1-S3 added the C1-E institution and council packet schema drafts. W1-S4 adds trace, state, and memory schema drafts for C1-F/C1-G. W1-S5 adds Track D CostEvent and Track E EvalSummary schema drafts for C1-H cost/eval readiness.

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

The existing `.gitkeep` files may remain beside schema files. Examples and validation tooling are deferred to later gated Wave 1 steps.

Future C1-I/C1-J validation work must include invalid fixtures for packet-type constants, schema-version constants, action-type conditional requirements, work-order targeting, hidden-effect completeness, institution brief withholding boundaries, recommended-order traceability, institution-order authority fields, doctrine-shift numeric bounds, macro-order policy-level constraints, emergency-measure bounded duration/sunset constraints, empty run tracks, invalid/quarantined MemoryUpdate states, field-specific trace ref_type integrity, ActionTrace.validation vs execution_status consistency, StateDiff.path format and path/visibility consistency, RunManifest.tracks duplicate track IDs and role constraints, CostEvent token/cost bounds, CostEvent free/fallback consistency, CostEvent cognition/cost ref integrity, CostEvent provider/model evidence consistency, CostEvent cost estimate status consistency, EvalSummary gate/count consistency, EvalSummary hard-gate failure consistency, empty EvalSummary by_level arrays, missing EvalSummary gate reasons for held runs, source_ref ref_type vocabulary integrity, missing trace refs, empty state changes, empty memory updates, hidden-leak detection, rumor/fact contamination, invalid visibility values, invalid run mode/status values, missing source refs, malformed IDs/refs, invalid enums, and out-of-range numeric values.
