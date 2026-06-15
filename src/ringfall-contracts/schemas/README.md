# Ringfall Schema Groups

This directory groups Ringfall JSON Schema files. W1-S1 created the group layout; W1-S2 introduced the first packet schema drafts. W1-S3 added the C1-E institution and council packet schema drafts. W1-S4 adds trace, state, and memory schema drafts for C1-F/C1-G.

Group purposes:

- `state/`: future world and state export schemas.
- `packets/`: execution-impacting typed packet schemas.
- `traces/`: run manifest, cognition trace, and action trace schemas. W1-S4 keeps `RunManifest` here as an artifact manifest surface without creating a separate manifest group.
- `memory/`: memory, belief, and claim schemas.
- `eval/`: future evaluation event and summary schemas.
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

The existing `.gitkeep` files may remain beside schema files. Examples and validation tooling are deferred to later gated Wave 1 steps.

Future C1-I/C1-J validation work must include invalid fixtures for packet-type constants, schema-version constants, action-type conditional requirements, work-order targeting, hidden-effect completeness, institution brief withholding boundaries, recommended-order traceability, institution-order authority fields, doctrine-shift numeric bounds, macro-order policy-level constraints, emergency-measure bounded duration/sunset constraints, empty run tracks, invalid/quarantined MemoryUpdate states, field-specific trace ref_type integrity, ActionTrace.validation vs execution_status consistency, StateDiff.path format and path/visibility consistency, RunManifest.tracks duplicate track IDs and role constraints, missing trace refs, empty state changes, empty memory updates, hidden-leak detection, rumor/fact contamination, invalid visibility values, invalid run mode/status values, missing source refs, malformed IDs/refs, invalid enums, and out-of-range numeric values.
