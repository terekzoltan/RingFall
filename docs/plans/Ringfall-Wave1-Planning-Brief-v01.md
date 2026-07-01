# Ringfall Wave 1 Planning Brief v0.1

## Status

**META-GATED for implementation entry:** yes.

Wave 1 Step 1 `W1-S1-C1-A-C1-B-contract-layout-and-schema-skeleton`, Step 2 `W1-S2-C1-C-C1-D-core-packet-schemas`, Step 3 `W1-S3-C1-E`, Step 4 `W1-S4-C1-F-C1-G`, Step 5 `W1-S5-C1-H`, Step 6 `W1-S6-C1-I-C1-J`, and Step 7 `W1-S7-C1-K` are complete and accepted.

This brief now serves as W1-S1/W1-S2/W1-S3/W1-S4/W1-S5/W1-S6/W1-S7 closeout evidence and the handoff guardrail for preparing Wave 2 under a separate Meta-gated implementation plan.

It does not approve runtime code, C#/.NET, Python brain, Unity, model calls, provider behavior, scenario content, or simulation logic. Post-closeout pre-Wave-2 cleanup may harden handoff docs and dev-only contract tooling without reopening Wave 1.

## Purpose

Wave 1 creates the contract and artifact spine that later tracks consume. The first implementation step must establish the canonical contract home and versioning notes before any packet or artifact semantics are written.

## Source Documents

Use this source order for Wave 1 planning and implementation handoffs:

1. Current repository implementation and committed files
2. `docs/ops/Ringfall-Design-Canon-and-Decision-Log-v01.md`
3. `docs/ops/Ringfall-Meta-Coordinator-Handoff-Brief-v01.md`
4. `docs/plans/Combined-Execution-Sequencing-Plan.md`
5. `docs/plans/Ringfall-Implementation-Wave-Plan-v01.md`
6. `docs/design/Ringfall-Action-and-Tool-Contract-v01.md`
7. `docs/design/Ringfall-Replay-and-Eval-Protocol-v01.md`
8. `docs/design/Ringfall-Agent-Memory-and-Belief-Model-v01.md`
9. `docs/design/Ringfall-Model-Policy-and-Cost-Architecture-v01.md`
10. `docs/design/Ringfall-Architecture-and-Repo-Plan-v01.md`

## Wave 1 Objective

Create the canonical JSON Schema contract surface and minimum artifact bundle shape that all implementation tracks consume.

Wave 1 preserves these architecture rules:

- JSON Schema is canonical for external artifacts.
- Internal domain code may remain code-first later.
- LLM/model outputs that affect execution must become strict typed packets.
- Contracts must not allow direct LLM world-state mutation.
- Artifact evidence must support replay/eval.
- Later model routing must remain OpenRouter-only, free-first where reliable, with `deepseek/deepseek-v4-flash` as the required low-cost paid fallback.
- FAL compatibility remains artifact-level; Ringfall must not depend on FAL runtime.

## Track Ownership

- Track B owns the contract home, schema layout, and primary schema implementation.
- Track E owns validation/examples/eval readiness when schemas exist.
- Track C reviews packet, role, prompt, and memory usability after drafts exist.
- Track D reviews cognition/cost/provider trace sufficiency after relevant draft surfaces exist.
- Track A reviews artifact readability and future Unity loader assumptions after artifact schemas exist.
- Meta owns sequencing, gate decisions, cross-track handoff, and scope protection.

## Wave 1 Step Sequence

1. `W1-S1 / C1-A,C1-B` — Track B creates contract layout and versioning/readme notes. **Complete.**
2. `W1-S2 / C1-C,C1-D` — Track B drafts core L1/action packet schemas. **Complete.**
3. `W1-S3 / C1-E` — Track C reviews packet usability; Track B adds institution/council packet shapes. **Complete.**
4. `W1-S4 / C1-F,C1-G` — Track B drafts run/cognition/action/state/memory trace schemas. **Complete.**
5. `W1-S5 / C1-H` — Track D/E review cost-event and eval-summary surfaces. **Complete.**
6. `W1-S6 / C1-I,C1-J` — Track E adds valid/invalid examples and schema validation tooling. **Complete.**
7. `W1-S7 / C1-K` — Meta runs cross-track contract handoff gate. **Complete.**

## W1-S1 Greenlight And Closeout

Track B implemented only `W1-S1-C1-A-C1-B-contract-layout-and-schema-skeleton`.

### In Scope

- `src/ringfall-contracts/README.md`
- `src/ringfall-contracts/docs/Contract-Versioning.md`
- `src/ringfall-contracts/schemas/README.md`
- `src/ringfall-contracts/schemas/state/.gitkeep`
- `src/ringfall-contracts/schemas/packets/.gitkeep`
- `src/ringfall-contracts/schemas/traces/.gitkeep`
- `src/ringfall-contracts/schemas/memory/.gitkeep`
- `src/ringfall-contracts/schemas/eval/.gitkeep`
- `src/ringfall-contracts/schemas/config/.gitkeep`

### Out Of Scope

- `*.schema.json` files
- valid/invalid examples
- `tools/schema_check.py`
- C#/.NET projects or source
- Python packages or scripts
- Unity `Assets/` or `ProjectSettings/`
- configs, scenarios, generated artifacts, replay data, provider/model calls
- packet/action semantics beyond noting that they are future work
- direct implementation of any Wave 1 step after C1-A/C1-B

## W1-S1 Acceptance Criteria

- ✅ `src/ringfall-contracts/` exists as the canonical contract home.
- ✅ Schema group folders exist and are tracked: `state`, `packets`, `traces`, `memory`, `eval`, `config`.
- ✅ Contract README states that W1-S1 is layout/versioning only.
- ✅ Contract README states JSON Schema is canonical for external artifacts.
- ✅ Schema README lists group purposes without defining real schemas.
- ✅ Versioning doc states initial schema version is `0.1`.
- ✅ Versioning doc states future schemas must carry `schema_version`.
- ✅ No `*.schema.json` files are created in W1-S1.
- ✅ No executable code, validation tool, examples, configs, scenarios, model calls, Unity, or simulation logic are added.

## Hold Conditions

Hold W1-S1 if any of these happen:

- A schema body is added before schema design review.
- A schema or doc allows LLM output to mutate world state directly.
- Track B adds examples or validation tooling and claims C1-I/C1-J progress.
- Track B defines provider/model runtime behavior.
- Track B defines Unity loader behavior beyond future-consumer notes.
- Product code, generated artifacts, or local secrets are added.

## Verification Matrix

Run after W1-S1 implementation:

```text
git status --short --untracked-files=all
git diff -- src/ringfall-contracts
git diff --check -- src/ringfall-contracts
```

Also verify:

- `src/ringfall-contracts/**` contains only approved layout/docs files.
- `src/**/*.schema.json` returns no files.
- `src/ringfall-core/**`, `client/**`, `tools/**`, `tests/**`, `configs/**`, and `scenarios/**` have no unintended changes.
- Model-policy references, if mentioned, preserve free-first plus `deepseek/deepseek-v4-flash` fallback and do not introduce runtime behavior.
- No secrets are present in added docs.

No build or runtime test is expected for W1-S1 because it is docs/layout only.

## Required Handoff After W1-S1

Track B closeout states:

- C1-A/C1-B created layout/versioning only.
- No schema bodies were added.
- C1-C/C1-D remain the next schema definition step.
- Track C/D/E/A are not activated until their prerequisite schema drafts exist.

Closeout evidence, 2026-06-14:
- Approved inventory: `src/ringfall-contracts/README.md`, `src/ringfall-contracts/docs/Contract-Versioning.md`, `src/ringfall-contracts/schemas/README.md`, and `.gitkeep` files under `state`, `packets`, `traces`, `memory`, `eval`, and `config`.
- Step review verdict: GREEN.
- Verification covered file inventory, no `*.schema.json`, out-of-scope diff, content checks, and secrets scan.

## Meta Gate Decision

Meta accepts `W1-S1-C1-A-C1-B-contract-layout-and-schema-skeleton` as complete for Track B under the exact scope above.

Any W1-S2/C1-C,C1-D expansion requires a new Meta review before implementation.

## W1-S2 Closeout

Track B implemented only `W1-S2-C1-C-C1-D-core-packet-schemas`.

### In Scope

- `src/ringfall-contracts/README.md`
- `src/ringfall-contracts/docs/Contract-Versioning.md`
- `src/ringfall-contracts/schemas/README.md`
- `src/ringfall-contracts/schemas/packets/avatar-pulse-packet.schema.json`
- `src/ringfall-contracts/schemas/packets/scene-action-packet.schema.json`
- `src/ringfall-contracts/schemas/packets/work-order-request.schema.json`
- `src/ringfall-contracts/schemas/packets/tool-action-request.schema.json`
- `src/ringfall-contracts/schemas/packets/execution-result.schema.json`

### W1-S2 Acceptance Criteria

- ✅ Exactly five packet schema drafts exist under `src/ringfall-contracts/schemas/packets/`.
- ✅ All W1-S2 schemas use JSON Schema Draft 2020-12.
- ✅ All W1-S2 schemas carry `schema_version.const = "0.1"` and `packet_type.const`.
- ✅ `SceneActionPacket.actions[]` has action-type conditional requirements.
- ✅ `WorkOrderRequest` requires either `target_crew_id` or `target_crew_pool_id`.
- ✅ `ExecutionResult.hidden_effects` is internal-only evidence and hidden-effect entries require core fields.
- ✅ No C1-E institution/council schemas, examples, validation tooling, runtime code, configs, scenarios, provider/model calls, Unity, or simulation logic were added.

### W1-S2 Review Evidence

- Step review verdict: GREEN after Swarm findings were fixed.
- Swarm review findings fixed before commit: incomplete scene actions, empty hidden effects, missing work-order target, and empty observable result objects.
- Verification covered exact schema inventory, JSON parse/structure checks, no forbidden mutation fields, no C1-E identifiers, out-of-scope diff, and secrets scan.
- Future C1-I/C1-J validation work must include invalid fixtures for action conditionals, work-order targeting, hidden-effect completeness, packet constants, malformed IDs/refs, invalid enums, and numeric bounds.

## Next Meta Gate Decision

Meta accepts `W1-S2-C1-C-C1-D-core-packet-schemas` as complete for Track B.

Any W1-S3/C1-E expansion requires a new Meta review before implementation.

## W1-S3 Closeout

Track C implemented only the packet-usability review/handoff artifact, and Track B implemented only the C1-E institution/council packet schema drafts.

### In Scope

- `docs/plans/W1-S3-C1-E-Track-C-Packet-Usability-Review.md`
- `src/ringfall-contracts/README.md`
- `src/ringfall-contracts/docs/Contract-Versioning.md`
- `src/ringfall-contracts/schemas/README.md`
- `src/ringfall-contracts/schemas/packets/institution-brief.schema.json`
- `src/ringfall-contracts/schemas/packets/institution-order.schema.json`
- `src/ringfall-contracts/schemas/packets/council-doctrine-packet.schema.json`

### W1-S3 Acceptance Criteria

- ✅ Track C review artifact exists and preserves schema ownership boundaries.
- ✅ Exactly eight packet schema drafts exist under `src/ringfall-contracts/schemas/packets/` after C1-E.
- ✅ C1-E schemas use JSON Schema Draft 2020-12 and `schema_version.const = "0.1"`.
- ✅ `InstitutionBrief` enforces withholding traceability when `withheld_items_count >= 1`.
- ✅ `InstitutionOrder` and `CouncilDoctrinePacket` preserve authority boundaries without direct world-state mutation fields.
- ✅ No examples, validation tooling, runtime code, configs, scenarios, provider/model calls, Unity, trace schemas, memory schemas, eval schemas, cost schemas, or simulation logic were added.

### W1-S3 Review Evidence

- Step review verdict: GREEN after Swarm findings were fixed or routed.
- Swarm review finding fixed before commit: `InstitutionBrief` positive withheld counts require `withholding_record_refs`.
- Semantic validation debt for recommended-order traceability, macro-order policy-level constraints, and emergency-measure bounded duration/sunset constraints is routed to future C1-I/C1-J invalid fixture work.
- Verification covered exact schema inventory, JSON parse/structure checks, no forbidden mutation fields, no forbidden adjacent packet names, out-of-scope diff, and secrets scan.

## Next Meta Gate Decision

Meta accepts `W1-S3-C1-E` as complete for Track C and Track B.

Any W1-S4/C1-F,C1-G expansion requires a new Meta review before implementation.

## W1-S4 Closeout

Track B implemented only `W1-S4-C1-F-C1-G` trace, state, and memory schema drafts.

### In Scope

- `src/ringfall-contracts/README.md`
- `src/ringfall-contracts/docs/Contract-Versioning.md`
- `src/ringfall-contracts/schemas/README.md`
- `src/ringfall-contracts/schemas/traces/run-manifest.schema.json`
- `src/ringfall-contracts/schemas/traces/cognition-trace.schema.json`
- `src/ringfall-contracts/schemas/traces/action-trace.schema.json`
- `src/ringfall-contracts/schemas/state/state-diff.schema.json`
- `src/ringfall-contracts/schemas/memory/claim-record.schema.json`
- `src/ringfall-contracts/schemas/memory/memory-update.schema.json`

### W1-S4 Acceptance Criteria

- ✅ Exactly three trace schema drafts exist under `src/ringfall-contracts/schemas/traces/`.
- ✅ Exactly one state schema draft exists under `src/ringfall-contracts/schemas/state/`.
- ✅ Exactly two memory schema drafts exist under `src/ringfall-contracts/schemas/memory/`.
- ✅ The repository contains exactly fourteen schema drafts after W1-S4: eight packet, three trace, one state, and two memory schemas.
- ✅ All W1-S4 schemas use JSON Schema Draft 2020-12 and `schema_version.const = "0.1"`.
- ✅ `RunManifest`, `CognitionTrace`, `ActionTrace`, `StateDiff`, `ClaimRecord`, and `MemoryUpdate` preserve artifact evidence boundaries without provider/runtime behavior.
- ✅ `ClaimRecord.claim_type` matches the canonical memory taxonomy and does not include non-canon `order_context`.
- ✅ No examples, validation tooling, runtime code, configs, scenarios, provider/model calls, Unity, cost schemas, eval schemas, or simulation logic were added.

### W1-S4 Review Evidence

- Initial step review verdict: RED because `ClaimRecord.claim_type` drifted from the canonical memory taxonomy.
- Review-fix verdict: GREEN after Track B aligned `claim_type` with `fact`, `observation`, `belief`, `rumor`, `inference`, `official_line`, `withheld_item`, `secret`, `doctrine`, `forecast`, and `memory_summary`.
- Semantic validation debt for invalid/quarantined memory updates, field-specific trace ref integrity, action validation/execution-status consistency, state path/visibility consistency, and run track uniqueness/role constraints is routed to future C1-I/C1-J invalid fixture and validation-tooling work.
- Verification covered exact schema inventory, JSON parse/structure checks, W1-S4 targeted regression checks, no forbidden mutation/provider/C1-H fields, out-of-scope diff, whitespace check, and secrets scan.

## Next Meta Gate Decision

Meta accepts `W1-S4-C1-F-C1-G` as complete for Track B.

Any W1-S5/C1-H expansion requires a new Meta review before implementation.

## W1-S5 Closeout

Track D implemented only the C1-H CostEvent schema side, and Track E implemented only the C1-H EvalSummary schema side plus shared schema README reconciliation.

### In Scope

- `src/ringfall-contracts/schemas/README.md`
- `src/ringfall-contracts/schemas/traces/cost-event.schema.json`
- `src/ringfall-contracts/schemas/eval/eval-summary.schema.json`

### W1-S5 Acceptance Criteria

- ✅ Exactly one CostEvent schema draft exists under `src/ringfall-contracts/schemas/traces/`.
- ✅ Exactly one EvalSummary schema draft exists under `src/ringfall-contracts/schemas/eval/`.
- ✅ The repository contains exactly sixteen schema drafts after W1-S5.
- ✅ C1-H schemas use JSON Schema Draft 2020-12 and `schema_version.const = "0.1"`.
- ✅ CostEvent records cost/model/provider evidence without runtime provider routing or OpenRouter behavior.
- ✅ EvalSummary records replay/eval summary evidence without EvalEvent, eval-runner behavior, examples, or validation tooling.
- ✅ Shared schema README documents both split-lane C1-H schemas and routes semantic validation debt to C1-I/C1-J.
- ✅ No examples, validation tooling, runtime code, configs, scenarios, provider/model calls, Unity, runtime cost collection, eval runner code, or simulation logic were added.

### W1-S5 Review Evidence

- Initial step review verdict: RED because shared `src/ringfall-contracts/schemas/README.md` documented only the Track E EvalSummary lane while both C1-H split-lane schemas were present.
- Review-fix verdict: GREEN after Track E reconciled the shared README to document Track D CostEvent and Track E EvalSummary together.
- Semantic validation debt for CostEvent fallback/cost consistency, EvalSummary gate/count consistency, source-ref vocabulary integrity, and held-run gate reasons is routed to future C1-I/C1-J invalid fixture and validation-tooling work.
- Verification covered exact 16-schema inventory, JSON parse checks, CostEvent/EvalSummary targeted assertions, no forbidden mutation/provider/runtime fields, out-of-scope diff, whitespace check, and secrets scan.

## Next Meta Gate Decision

Meta accepts `W1-S5-C1-H` as complete for Track D and Track E.

Any W1-S6/C1-I,C1-J expansion required a new Meta review before implementation and is now accepted under the exact W1-S6 scope.

## W1-S6 Closeout

Track E implemented only `W1-S6-C1-I-C1-J` valid/invalid examples and dev-only schema validation tooling.

### In Scope

- `requirements-dev.txt`
- `tools/schema_check.py`
- `src/ringfall-contracts/README.md`
- `src/ringfall-contracts/schemas/README.md`
- `src/ringfall-contracts/examples/README.md`
- `src/ringfall-contracts/examples/manifest.json`
- `src/ringfall-contracts/examples/valid/**`
- `src/ringfall-contracts/examples/invalid/**`

### W1-S6 Acceptance Criteria

- ✅ Exactly one valid fixture exists for each of the sixteen current schema drafts.
- ✅ Invalid fixtures cover routed W1 schema and fixture-local semantic validation debt without runtime/provider/model behavior.
- ✅ `tools/schema_check.py` validates Draft 2020-12 schemas, manifest consistency, valid fixtures, invalid fixtures, and semantic reason isolation.
- ✅ `requirements-dev.txt` provides the dev-only `jsonschema` dependency surface for the checker.
- ✅ Shared contract docs distinguish W1-S6 static fixture/tool coverage from deferred runtime/artifact-context validation debt.
- ✅ No `*.schema.json` bodies were edited.
- ✅ No runtime code, provider/model behavior, Unity work, scenarios, generated artifacts, eval runner code, runtime cost collection, or simulation logic were added.

### W1-S6 Review Evidence

- Initial step review verdict: YELLOW because one CostEvent invalid fixture triggered both `cost_fallback_reason_required` and `cost_free_zero_consistency`.
- Review-fix verdict: GREEN after Track E added `free_model_used: true` to `fallback-missing-reason.json` and hardened `tools/schema_check.py` to fail semantic invalid fixtures on missing or extra semantic reason codes.
- Verification covered `python tools/schema_check.py`, `--valid-only`, `--invalid-only`, semantic overlap proof with `overlap_count=0`, manifest counts, no schema body diffs, whitespace check, forbidden-scope check, and secrets scan.
- Residual packet/state/memory and CostEvent provider/model evidence semantics are routed to Track B and Track D review at W1-S7/C1-K before final Wave 1 closeout.

## Next Meta Gate Decision

Meta accepts `W1-S6-C1-I-C1-J` as complete for Track E within the exact contract-example and dev-tooling scope above.

Any W1-S7/C1-K expansion required a new Meta review before implementation and is now accepted under the exact C1-K review/handoff scope.

## W1-S7 Closeout

Meta implemented only the `W1-S7-C1-K` cross-track contract handoff review and Wave 1 gate publication.

### In Scope

- `docs/plans/W1-S7-C1-K-Contract-Handoff-Review-Packet.md`
- `README.md`
- `docs/plans/Combined-Execution-Sequencing-Plan.md`
- `docs/plans/Ringfall-Wave1-Planning-Brief-v01.md`

### W1-S7 Acceptance Criteria

- ✅ Required Track B review completed with no blocker; the low-risk `ref_type` checker-scope concern was resolved by post-closeout pre-Wave-2 cleanup.
- ✅ Required Track D review approved CostEvent static evidence and provider/model wording.
- ✅ Required Track E review approved validation evidence, manifest coverage, semantic isolation, and EvalSummary coverage.
- ✅ Optional Track C review approved role/prompt usability with routed future positive-memory-example concerns.
- ✅ Optional Track A review approved artifact readability with routed future bundle-loader graph concerns.
- ✅ `python tools/schema_check.py` passed against 16 schemas and 41 fixture entries.
- ✅ No accepted finding lacks an owner and future gate.
- ✅ No runtime code, provider/model behavior, Unity work, scenarios, generated artifacts, eval runner code, runtime cost collection, or simulation logic were added.

### W1-S7 Review Evidence

- Track B verdict: CONCERNS / LOW; C1-K may proceed with routed concerns.
- Track D verdict: APPROVE / LOW; C1-K may proceed.
- Track E verdict: APPROVE / LOW; C1-K may proceed.
- Track C optional verdict: APPROVE / LOW; C1-K may proceed with routed concerns.
- Track A optional verdict: APPROVE / LOW; C1-K may proceed with routed concerns.
- Accepted routed concerns: Track B `ref_type` checker scope, Track C positive memory example breadth, Track A full observer-loader artifact graph, Track D real provider request consistency, and Track E EvalEvent/eval-runner runtime behavior.
- Post-closeout pre-Wave-2 cleanup resolved the Track B `ref_type` checker-scope concern by scoping `source_ref_vocabulary` to known reference structures rather than arbitrary objects with `ref_type`. Broader bundle-level reference existence and cross-artifact consistency remain deferred to the first runtime/artifact bundle validation gate.
- Remaining active routes: Track C owns positive memory examples for rumor, belief, official_line, and withheld_item at the first role/memory implementation or Wave 2/3 prompt-memory fixture expansion gate, with Track B/E support if schemas/examples/tooling are touched; Track A owns a connected artifact bundle graph at the first artifact bundle validation or loader-planning gate, with Track B/E support if bundle validation touches contracts; Track D owns CostEvent reconciliation against real provider request/response evidence at the first provider/runtime implementation or runtime artifact bundle validation gate; Track E owns the EvalEvent/eval-runner decision at later eval/replay schema and runtime artifact validation gates.

## Next Meta Gate Decision

Meta accepts `W1-S7-C1-K` and Wave 1 contract handoff as complete. The immediate post-Wave-1 frontier is Wave 1.5 contract CI readiness, followed by Wave 2 deterministic core/headless shell planning under a new Meta-gated implementation plan or an explicit Meta CI-debt exception.

Wave 1 remains closed after the post-closeout pre-Wave-2 cleanup. No runtime work is approved by this cleanup.

Post-Wave-1 formal-intervention gate direction is tracked outside Wave 1 scope in `docs/design/Formal-Intervention-Gates-Refinery.md`: Refinery may later be used as a bounded family-by-family formal gate, starting with Aster L1 tool/work-order proposals, but Wave 1 acceptance does not approve Refinery tooling, solver CI, runtime bridge code, or a full formal world model.
