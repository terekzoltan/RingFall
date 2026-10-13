# Ringfall

Ringfall is a replayable, inspectable, LLM-heavy civilization simulation set in a fractured orbital ring. It is built around deterministic simulation boundaries, typed action/tool contracts, memory/belief/truth separation, institutional distortion, council doctrine, OpenRouter-based model orchestration, and a Unity-rendered observer client.

Current phase: Wave 0 bootstrap and Wave 1 contract handoff are closed through W1-S7/C1-K. Wave 1.5 contract CI readiness is accepted through CI15-A/CI15-B/CI15-C/CI15-D/CI15-E/CI15-F with no recorded CI-debt exception. Wave 2 is closed through K2-A/K2-B/K2-C/K2-D/K2-E/K2-F/K2-G/K2-H and the Wave 2 Meta closeout gate: the no-LLM Aster seed run is deterministic and writes smoke-validated manifest/snapshot/event/state-diff artifacts. Wave 3 is closed through B3-A/B/C/D/E/F/G/H and the Wave 3 Meta closeout gate: the Python brain scaffold emits deterministic schema-valid L1 packet/trace/cost evidence without a Core mutation path, and Track E validates that bundle. Wave 4 scoped implementation is integrated into `main`; this is not full Wave 4 acceptance. This repository is not ready for public use. The detailed design canon, world bible, implementation plan, and private coordination notes are local/private until an explicit export review approves public release material.

Public status verified against `main` at [`d208c7fb9656c4dfe4f34eb30e629a59bd20c875`](https://github.com/terekzoltan/RingFall/commit/d208c7fb9656c4dfe4f34eb30e629a59bd20c875) on 2026-10-13:

- **Merged and CI-verified:** [PR #6](https://github.com/terekzoltan/RingFall/pull/6), merged on 2026-10-04, integrates Track B's scoped gated A1 → F1 → Core execution, Track D's offline, transport-injected request client, and the separately reviewed CRLF/LF portability repair. Exact-merge [Runtime CI](https://github.com/terekzoltan/RingFall/actions/runs/37225765241) and [Contract CI](https://github.com/terekzoltan/RingFall/actions/runs/37225765291) succeeded; PR #6 records Core 362/362 and Brain 138/138 tests passing.
- **R0 remains separate:** the public delivery record reports `R0_LOCAL_PASS / EXACT_MAIN_ADMISSION_PENDING`, with 153/153 selected Core tests and five bounded named contexts, including materialized A1 and four permitted empty contexts. This does not establish five actively deciding actors or final exact-main R0 admission; that assessment remains a separate Meta continuation.
- **Limits unchanged:** Wave 4 Step 8 remains **NOT_READY/unopened**, with five unchecked mandatory outputs and five holds still open. Track B retains its `schema_validation=not_run`, reroute and limited wait-control disclosures. Track D is cooperative, offline and in-memory, without a live HTTP adapter or live model/provider/billing certification. Integration and CI do not imply Wave 4 gate acceptance, Wave 5 dispatch, live/paid calls or deployment.

See the [public-safe B/D delivery note](docs/plans/Wave4-PreStep8-BD-Delivery-20261002.md) for retained scope and limitations. Dated integration-pending and next-action text in [PROJECT_STATE](ops/PROJECT_STATE.md) is historical where superseded by PR #6's verified integration; it is not a fresh execution instruction. This README summarizes public evidence only and does not reconcile inaccessible local Canon or operational state.

## First Playable Scope

The first playable target is **Ringfall FP1: Aster/Vireo/Black Seam Slice**.

FP1 is intended to prove that a headless, artifact-driven L1/L2/L3 LLM society simulation can run, act, remember, distort information, escalate decisions, create cascades, and be replayed/evaluated from artifacts.

Initial FP1 scope:

- Aster Industrial / Utility Sector
- Vireo Bio-Life Support Sector
- Morrow Chain mobile habitat / convoy
- Black Seam deepworks branch
- House Cerulean merit-house / academy subsystem
- 50 named L1 embodied LLM avatars
- 21 named L2 institutional LLM seats across 7 embodied institutions
- 7 named L3 council/doctrine LLM seats
- 78 total named LLM actors/seats

Primary FP1 demos are Aster Heat Alarm, Vireo Symptom Cluster, Black Seam Protocol Access, Morrow Chain Manifest Fracture, and House Cerulean Patronage Pressure.

## Architecture Stance

Ringfall is headless-first and artifact-first. The authoritative simulation must run without UI, produce inspectable artifacts, and support replay/eval before any visual layer becomes mandatory.

Ringfall also adopts a bounded formal intervention gate direction for future execution-impacting LLM proposals. The approved approach is not a full formal world model: Refinery, if introduced, must model only a named intervention family such as Aster L1 tool/work-order proposals, memory/visibility updates, institution orders, or council doctrine decisions. Unsupported surfaces must be diagnosed as unsupported rather than treated as valid. See `docs/design/Formal-Intervention-Gates-Refinery.md`.

Core boundaries:

- **Core:** C#/.NET deterministic simulation core and headless runner own authoritative world truth, validation, state transitions, event logs, state diffs, and snapshots.
- **Brain:** Python LLM orchestration service/CLI owns prompts, OpenRouter calls, model policy, strict packet generation, retries/fallbacks, cost events, cognition traces, and memory retrieval context.
- **Contracts:** JSON Schema is the canonical cross-language contract surface, with C#/Python bindings or DTOs added later.
- **Client:** Unity 6.3 LTS 3D-rendered 2.5D observer visualizes runs, replays, inspectors, timelines, and overlays, but does not own truth.
- **Batch/replay/eval:** scenario runners and tooling consume artifacts as evidence.
- **Formal intervention gates:** future Refinery models may check bounded candidate facts between JSON Schema validation and Core authority validation; they never own truth and never directly mutate runtime state.

Architectural guardrails:

- LLM minds, deterministic hands.
- Core is authoritative.
- Brain never directly mutates world state.
- Contracts are canonical.
- Formal gates are bounded by intervention family; no full-world formal validation claim is allowed.
- Unity observes; it does not own truth.
- Artifacts are evidence.
- OpenRouter-only for V1/V2 model execution.
- Model policy is free-first where reliable, with `deepseek/deepseek-v4-flash` as the required low-cost paid fallback.
- FAL compatibility is artifact-level first, not a runtime dependency.

## Repository Layout

| Path | Purpose |
|---|---|
| `docs/` | Public-safe placeholders; private canon is ignored until export review. |
| `src/` | Source code, including the Wave 1 contract layout/schema drafts, Wave 2 C# deterministic core/headless runner, and Wave 3 Python brain skeleton. |
| `client/` | Future client code. Empty in this skeleton. |
| `configs/` | Example configs only; no local secrets. |
| `scenarios/` | Future scenario packs. Empty in this skeleton. |
| `data/` | Local/generated run artifacts; ignored except `.gitkeep`. |
| `tests/` | Future tests. Empty in this skeleton. |
| `tools/` | Developer utilities, including the Wave 1 contract schema checker and Wave 2 core artifact smoke validator. |
| `infra/` | Optional deployment/remote-run material later. Empty in this skeleton. |

## Current Non-Goals

- no full Wave 4 LLM society simulation or Wave 4 gate acceptance; current execution evidence is scoped to the integrated A1 → F1 → Core path
- no completeness claim beyond the accepted deterministic Core and scoped Wave 4 integration
- no live Python Brain provider integration; the integrated request client is offline and transport-injected
- no production runtime schema service or generated contract bindings; current validation tooling is developer-only
- no exhaustive formal-gate certification; the integrated scoped F1 path and retained local Refinery evidence do not prove all Wave 4 hard gates
- no full formal world model
- no Unity project yet
- no live OpenRouter/model execution
- no real provider credentials or local secrets
- no FAL runtime dependency
- no public release quality claim
- no private design canon in public history

## Source Of Truth

The detailed source-of-truth documents are private/local for now and are excluded by `.gitignore`, except for explicitly committed public-safe planning material. Public documentation should be added only after a deliberate sanitization/export review.

## Privacy And Local State

- `.fal/` is local/private FAL coordination state and is ignored.
- `.opencode/`, `.swarm/`, and `kontext/` are local tool/session state and are ignored.
- `data/runs/`, traces, replays, eval outputs, local configs, and secrets are ignored by default.
- `ringfall-canonical-docs-clean.zip` is a local source archive; live canon is markdown under `docs/`.
- `docs/**/*Ringfall-*.md` and the private Combined plan are ignored until public export review.
