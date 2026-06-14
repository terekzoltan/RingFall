# Ringfall Wave 1 Planning Brief v0.1

## Status

**META-GATED for implementation entry:** yes.

Wave 1 Step 1 `W1-S1-C1-A-C1-B-contract-layout-and-schema-skeleton` is complete and accepted for Track B.

This brief now serves as W1-S1 closeout evidence and the handoff guardrail for preparing W1-S2/C1-C,C1-D under a separate Meta-gated implementation plan.

It does not approve packet schemas, trace schemas, examples, validation tooling, C#/.NET, Python brain, Unity, model calls, provider behavior, scenario content, or simulation logic.

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
2. `W1-S2 / C1-C,C1-D` — Track B drafts core L1/action packet schemas. **Next gated planning target.**
3. `W1-S3 / C1-E` — Track C reviews packet usability; Track B adds institution/council packet shapes.
4. `W1-S4 / C1-F,C1-G` — Track B drafts run/cognition/action/state/memory trace schemas.
5. `W1-S5 / C1-H` — Track D/E review cost-event and eval-summary surfaces.
6. `W1-S6 / C1-I,C1-J` — Track E adds valid/invalid examples and schema validation tooling.
7. `W1-S7 / C1-K` — Meta runs cross-track contract handoff gate.

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
