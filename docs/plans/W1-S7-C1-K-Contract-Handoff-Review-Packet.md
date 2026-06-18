# W1-S7 / C1-K Contract Handoff Review Packet

**Target:** W1-S7-C1-K cross-track contract handoff review
**Owner:** Meta Coordinator
**Status:** complete / accepted after required and optional Track fan-in
**Baseline commit:** `fba9293 contracts: add Wave 1 examples validation tooling`

## Purpose

This packet is the shared review input for C1-K. It lets each Track review the same accepted Wave 1 contract surface before Meta publishes the Wave 1 handoff status for Wave 2/3 planning.

C1-K is a review and gate step. It must not add runtime code, provider/model behavior, Unity work, scenarios, generated artifacts, eval runner logic, runtime cost collection, simulation logic, or schema body edits unless Meta explicitly opens a fix loop.

## Current Evidence Snapshot

- W1-S1 through W1-S6 are accepted in the public planning docs.
- Latest accepted commit is `fba9293 contracts: add Wave 1 examples validation tooling`.
- `src/ringfall-contracts/schemas/` contains exactly 16 schema drafts.
- `src/ringfall-contracts/examples/manifest.json` contains 41 fixture entries.
- Valid fixtures: 16, exactly one per current schema.
- Invalid fixtures: 25, including 15 JSON Schema invalid fixtures and 10 static semantic invalid fixtures.
- `tools/schema_check.py` validates schemas, manifest consistency, valid fixtures, invalid fixtures, and semantic reason isolation.
- `requirements-dev.txt` declares the dev-only `jsonschema>=4.18,<5` dependency.

Latest checker evidence:

```text
python tools/schema_check.py
schemas: 16 Draft 2020-12 schema files passed metaschema checks
manifest: 41 entries passed consistency checks
fixtures: 41 selected entries validated successfully
```

Additional closeout checks from W1-S6:

```text
python tools/schema_check.py --valid-only   # 16 selected entries validated successfully
python tools/schema_check.py --invalid-only # 25 selected entries validated successfully
semantic overlap proof                     # overlap_count=0, missing_count=0
secretscan src/ringfall-contracts          # 0 findings
```

## Schema Inventory

```text
src/ringfall-contracts/schemas/eval/eval-summary.schema.json
src/ringfall-contracts/schemas/memory/claim-record.schema.json
src/ringfall-contracts/schemas/memory/memory-update.schema.json
src/ringfall-contracts/schemas/packets/avatar-pulse-packet.schema.json
src/ringfall-contracts/schemas/packets/council-doctrine-packet.schema.json
src/ringfall-contracts/schemas/packets/execution-result.schema.json
src/ringfall-contracts/schemas/packets/institution-brief.schema.json
src/ringfall-contracts/schemas/packets/institution-order.schema.json
src/ringfall-contracts/schemas/packets/scene-action-packet.schema.json
src/ringfall-contracts/schemas/packets/tool-action-request.schema.json
src/ringfall-contracts/schemas/packets/work-order-request.schema.json
src/ringfall-contracts/schemas/state/state-diff.schema.json
src/ringfall-contracts/schemas/traces/action-trace.schema.json
src/ringfall-contracts/schemas/traces/cognition-trace.schema.json
src/ringfall-contracts/schemas/traces/cost-event.schema.json
src/ringfall-contracts/schemas/traces/run-manifest.schema.json
```

## Review Dispatch Sequence

### 0. Meta Prep

This packet is the Meta prep output. Do not start Track review from stale context; reviewers should use this packet plus the current repository at or after `fba9293`.

### 1. Required Parallel Lanes

Run these in parallel from this same packet:

- Track B: contract, packet, state, and memory semantics review.
- Track D: CostEvent, model-policy, provider/model evidence semantics review.
- Track E: validation evidence confirmation.

C1-K cannot be GREEN until required B/D/E outputs are accepted or explicitly routed by Meta.

### 2. Optional Parallel Lanes

These may run in parallel if capacity is available:

- Track C: role, prompt, belief, and memory usability review.
- Track A: future artifact-loader and observer readability assumptions.

Track A/C findings should be treated as handoff-quality input unless they reveal a contract blocker. If Track B or Track D force fixture/schema changes, optional Track A/C findings may need re-check.

### 3. Meta Fan-In

Meta waits for required B/D/E outputs, then synthesizes:

- accepted blockers;
- downgraded or rejected findings;
- deferred findings with active gates;
- ownership conflicts;
- final Wave 1 handoff readiness for Wave 2/3 planning.

## Required Review Lanes

### Track B Required Review

Focus:

- packet fixtures under `src/ringfall-contracts/examples/valid/packets/` and `invalid/*packet*` / packet-named invalid folders;
- state and memory fixtures under `valid/state/`, `valid/memory/`, `invalid/state-diff/`, `invalid/memory-update/`, and `invalid/claim-record/`;
- semantic checks in `tools/schema_check.py` that touch Track B-owned packet/state/memory meaning;
- whether examples or checker logic accidentally redefine schema semantics instead of testing accepted contracts.

Questions to answer:

- Are the packet, state, and memory fixtures semantically usable as contract examples?
- Do any fixtures leak hidden truth into public/user-facing fields?
- Does any static semantic check overstep Track B schema ownership?
- Is any issue blocking C1-K, or should it be deferred to a later runtime/artifact validation gate?

Expected output:

- APPROVE / CONCERNS / REJECT.
- Findings with file paths and severity.
- Explicit statement whether C1-K may proceed from Track B perspective.

### Track D Required Review

Focus:

- `src/ringfall-contracts/schemas/traces/cost-event.schema.json` as accepted schema context only;
- `src/ringfall-contracts/examples/valid/traces/cost-event.json`;
- `src/ringfall-contracts/examples/invalid/cost-event/*.json`;
- CostEvent semantic checks in `tools/schema_check.py`;
- provider/model evidence wording in `src/ringfall-contracts/examples/README.md` and `src/ringfall-contracts/schemas/README.md`.

Questions to answer:

- Are CostEvent fixtures sufficient as static contract evidence without pretending to implement provider runtime behavior?
- Do the free/mock-zero and fallback semantic checks match W1 design intent?
- Does any doc claim provider/model behavior, runtime cost collection, or OpenRouter routing that does not exist?
- Is any issue blocking C1-K, or should it be deferred to provider/runtime implementation gates?

Expected output:

- APPROVE / CONCERNS / REJECT.
- Findings with file paths and severity.
- Explicit statement whether C1-K may proceed from Track D perspective.

### Track E Required Review

Focus:

- `tools/schema_check.py`;
- `requirements-dev.txt`;
- `src/ringfall-contracts/examples/manifest.json`;
- all valid and invalid fixture coverage;
- eval fixtures and EvalSummary semantic checks.

Questions to answer:

- Does `python tools/schema_check.py` pass from a clean checkout after installing dev requirements?
- Does every schema have exactly one valid fixture?
- Do invalid fixtures have manifest-backed reason codes and no extra semantic reason hits?
- Is EvalSummary coverage adequate for W1 C1-K handoff without adding EvalEvent or eval-runner behavior?
- Is any issue blocking C1-K, or should it be deferred to later eval/replay gates?

Expected output:

- APPROVE / CONCERNS / REJECT.
- Verification commands and outputs.
- Explicit statement whether C1-K may proceed from Track E perspective.

## Optional Review Lanes

### Track C Optional Review

Focus:

- packet and memory usability for future L1/L2/L3 role/prompt work;
- hidden-truth, rumor, belief, withholding, and institution/council examples;
- readability of examples as prompt-adjacent contract artifacts.

Expected output:

- usability findings only;
- blocker only if a contract artifact would mislead future role/prompt work.

### Track A Optional Review

Focus:

- readability of `run-manifest`, trace, state-diff, and eval-summary fixtures for future Unity/observer loaders;
- whether artifact examples expose enough stable IDs/refs for future loader assumptions;
- no Unity implementation or loader code.

Expected output:

- artifact-loader assumption notes;
- blocker only if a contract artifact is unreadable or inconsistent enough to block future loader planning.

## Deferred Debt Already Routed

The following are not W1-S6 static fixture/tool blockers unless a reviewer finds that the current docs overclaim them:

- recommended-order traceability beyond fixture-local structural fields;
- institution-order runtime authority against real seats and institutions;
- macro-order policy-level consequences beyond bounded fixture fields;
- full StateDiff path and visibility consistency against an actual state tree;
- CostEvent provider/model evidence consistency against real provider request records;
- missing trace refs across a real artifact bundle;
- complete hidden-leak detection against prompt/context artifacts;
- complete rumor/fact contamination detection against claim graph state.

These are routed to W1-S7/C1-K classification and then to the first runtime/artifact bundle validation gate before canonical evidence.

## C1-K Acceptance Checklist

Meta may mark C1-K GREEN only when:

- required Track B review is APPROVE or all accepted findings are fixed/routed;
- required Track D review is APPROVE or all accepted findings are fixed/routed;
- required Track E review is APPROVE or all accepted findings are fixed/routed;
- `python tools/schema_check.py` passes;
- no schema examples fail;
- no accepted finding lacks an active owner and future gate;
- no runtime/provider/model/Unity/scenario/simulation implementation slipped into Wave 1;
- final handoff status is published for Wave 2/3 consumers.

## Final Fan-In Synthesis

**Overall verdict:** GREEN with routed concerns.
**Meta decision:** C1-K may proceed and Wave 1 contract handoff is accepted for Wave 2/3 planning.

Required lanes:

| Track | Verdict | Risk | C1-K Proceed? | Result |
|---|---|---:|---|---|
| Track B | CONCERNS | LOW | YES WITH ROUTED CONCERNS | Packet, state, and memory fixtures are usable; one `ref_type` checker-scope concern is routed. |
| Track D | APPROVE | LOW | YES | CostEvent static evidence and docs are sufficient; real-provider consistency remains later work. |
| Track E | APPROVE | LOW | YES | Checker, manifest, fixture counts, semantic isolation, and EvalSummary coverage pass. |

Optional lanes:

| Track | Verdict | Risk | C1-K Proceed? | Result |
|---|---|---:|---|---|
| Track C | APPROVE | LOW | YES WITH ROUTED CONCERNS | Role/prompt-facing examples are usable; broader positive memory examples are deferred. |
| Track A | APPROVE | LOW | YES WITH ROUTED CONCERNS | Artifact examples are readable enough for future loader planning; full bundle graph is deferred. |

## Accepted Routed Concerns

### Track B: `ref_type` checker scope

`tools/schema_check.py` currently applies `source_ref_vocabulary` recursively to any object containing `ref_type`. This is acceptable for W1 static fixtures, but it could accidentally define a global `ref_type` namespace beyond explicit schemas.

- Status: not a C1-K blocker.
- Owner: Track B for contract/tooling scope, with Track E if implemented in validation tooling.
- Route: first runtime/artifact bundle validation gate or next Track B schema/tooling pass.
- Future resolution: scope the check to known source-ref/system-ref structures or promote the vocabulary into explicit schema/docs.

### Track C: positive memory example breadth

Valid memory coverage currently shows an observation claim, while rumor, belief, official-line, and withheld-item semantics are represented by schema enums or invalid semantic fixtures.

- Status: not a C1-K blocker.
- Owner: Track C, with Track B/E if fixture/schema/tooling changes are required.
- Route: Wave 2/3 prompt/memory fixture expansion or first role/memory implementation gate.

### Track A: full observer-loader artifact graph

Valid run/trace/state/eval examples are readable but intentionally minimal; they do not yet demonstrate a complete observer-loader bundle graph from manifest to cognition/action/state/eval.

- Status: not a C1-K blocker.
- Owner: Track A.
- Route: first runtime/artifact bundle validation gate or Track A loader-planning fixture work once richer artifact bundles exist.

### Track D: real provider request consistency

CostEvent provider/model consistency against real provider request records is intentionally out of W1 static fixture scope.

- Status: not a C1-K blocker.
- Owner: Track D.
- Route: first runtime/artifact bundle validation gate before canonical evidence.

### Track E: EvalEvent and eval-runner runtime behavior

EvalEvent and runtime replay/eval behavior remain intentionally outside W1 C1-K evidence.

- Status: not a C1-K blocker.
- Owner: Track E.
- Route: later eval/replay schema and runtime artifact validation gates.

## Final Handoff Status

Wave 1 contract handoff is accepted for Wave 2/3 planning with the routed concerns above. The accepted surface includes schemas, examples, dev-only validation tooling, and cross-track review evidence. It does not include runtime code, provider/model execution, Unity work, scenarios, generated artifacts, eval-runner behavior, runtime cost collection, or simulation logic.
