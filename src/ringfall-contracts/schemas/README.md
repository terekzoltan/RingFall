# Ringfall Schema Groups

This directory groups Ringfall JSON Schema files. W1-S1 created the group layout; W1-S2 introduced the first packet schema drafts. W1-S3 adds the C1-E institution and council packet schema drafts.

Group purposes:

- `state/`: future world and state export schemas.
- `packets/`: execution-impacting typed packet schemas.
- `traces/`: future run, cognition, action, and state-diff trace schemas.
- `memory/`: future memory, belief, and claim trace schemas.
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

The existing `.gitkeep` files may remain beside schema files. Examples and validation tooling are deferred to later gated Wave 1 steps.

Future C1-I/C1-J validation work must include invalid fixtures for packet-type constants, schema-version constants, action-type conditional requirements, work-order targeting, hidden-effect completeness, institution brief withholding boundaries, recommended-order traceability, institution-order authority fields, doctrine-shift numeric bounds, macro-order policy-level constraints, emergency-measure bounded duration/sunset constraints, malformed IDs/refs, invalid enums, and out-of-range numeric values.
