# Combined Execution Sequencing Plan

**Project:** Ringfall  
**Owner:** Meta Coordinator  
**Scope:** Track-level execution ordering for Ringfall FP1 — Aster/Vireo/Black Seam Slice  
**Intent:** turn the Ringfall design canon into an actually executable Meta + Track wave plan  
**Status:** active planning document / v04 controlled-parallelism + formal-intervention-gate revision
**Baseline source:** `Ringfall-Implementation-Wave-Plan-v01.md`  
**Expected location:** `docs/plans/Combined-Execution-Sequencing-Plan.md`  

**v04 revision note:** This version preserves the v03 controlled side-lane structure and integrates the approved Refinery direction as bounded formal intervention gates. Refinery is not a full-world model and is not part of Wave 1.5 contract CI; it becomes a family-by-family safety gate starting with Aster L1 tool/work-order proposals.

---

## 1. Why this document exists

The Ringfall design canon defines a large, layered, LLM-heavy civilization simulation.  
This document defines the **execution order**.

It answers:

- what starts first
- what can run in parallel
- what must wait
- which track owns what
- which artifacts must exist before the next wave opens
- what gates close each wave
- which review/eval evidence is required
- how the project grows from **planning canon** → **headless core** → **LLM verticals** → **FP1 canonical build** → **Unity observer** → **remote/demo readiness**

This is the central sequencing reference for:

- Ringfall Meta Coordinator session
- Track A/B/C/D/E implementation sessions
- review and gate sessions
- future FAL target-project coordination
- sprint kickoff and fork/merge handoffs

This document is intentionally more operational than the design docs.  
If a track asks “what do I do next?”, this plan should give the first answer.

---

## 2. Planning stance

Ringfall is intentionally ambitious, but implementation must remain staged.

The execution stance is:

```text
architecture may be huge
implementation must prove verticals
```

Ringfall is not built by starting with the full world, full UI, or full model policy.  
It is built by proving narrow verticals that preserve the whole architecture.

The execution ladder is:

1. **Canon and skeleton** — docs, repo, contracts, artifact shape
2. **Headless truth** — deterministic sim core and no-LLM state transitions
3. **Cognition boundary** — brain emits typed packets, never truth
4. **Formal intervention safety** — named Refinery families check bounded candidate facts before runtime apply
5. **First L1 vertical** — Aster Heat Alarm proves LLM minds / deterministic hands
6. **Memory and replay** — beliefs, rumors, visibility, and hard gates
7. **L2 institutions** — real control rooms and withholding
8. **L3 doctrine** — council, Charter, emergency powers
9. **Cross-institution FP1** — Morrow, Ration, House, public narrative
10. **Canonical hardening** — replay/eval/cost/regression
11. **Unity observer** — visual legibility from artifacts
12. **Remote/demo readiness** — safe curated package

The sequencing rule is:

> Generality is allowed in design, but must be earned in implementation.

---

## 3. Status markers and gate language

### Epic status markers

- `⬜` = not started
- `🔄` = in progress
- `✅` = completed and accepted
- `⏸` = intentionally paused
- `🚫` = blocked by unmet prerequisite, design conflict, or external dependency

### Execution-step status markers

Execution steps use the same markers as epics:

- `⬜` = no row in the step has started
- `🔄` = at least one row is in progress and step is not complete
- `✅` = all required rows complete and accepted
- `⏸` = paused by Meta
- `🚫` = blocked by unmet prerequisite or guardrail breach

### Gate language

- `NOT READY` = prerequisite is not accepted; the track must not implement the blocked item
- `READY` = prerequisites are accepted; track may begin
- `HANDOFF READY` = producing track explicitly provides contract/artifact usable by consumer track
- `VERIFY FIRST` = implementation may exist, but dependent work waits for smoke/schema/replay verification
- `FORMAL READY` = a named bounded intervention family has fixtures, Refinery model evidence, bridge/core differential checks, and no unsupported silent accepts
- `CANONICAL READY` = artifact-backed, hard-gate checked, and acceptable for canonical evidence
- `DEV ONLY` = usable for local/dev experimentation but not canonical evidence

### Acceptance vocabulary

- `acceptance gate` = local criteria for epic are met
- `smoke gate` = minimal runnable path passes
- `contract gate` = schemas/examples/consumers agree
- `artifact gate` = required manifest/traces/diffs/cost/eval exist
- `replay gate` = artifact or deterministic replay works
- `formal intervention gate` = a bounded Refinery family checks candidate facts for a named intervention surface; it is not a full-world correctness claim
- `wave gate` = mandatory epics complete and risks documented
- `canonical gate` = hard evals pass and run mode is canonical

### Ownership vs execution assignment

- `Owner` = accountable track for result and closeout
- `Execution assignment` = concrete order where multiple tracks touch one scope
- If an epic is co-owned, the sprint must define track order
- No multi-owner epic may remain without execution assignment

### Session labels used in execution tables

- `Meta Coordinator session` = planning, sequencing, review, gate, source-of-truth
- `Track A session` = Unity / client / observer / UX / visualization
- `Track B session` = C# core / contracts / schemas / state / validation
- `Track C session` = agent roles / prompts / memory / beliefs / L1/L2/L3 semantics
- `Track D session` = provider/runtime / OpenRouter / model policy / cost / brain service
- `Track E session` = eval / replay / artifact validation / cost / quality
- `Scenario/Analyst session` = optional later cascade/scenario/cost analysis

### Canonical execution-table protocol

Execution tables are the chronological contract for Meta and Track sessions. They must be unambiguous enough that a new session can tell what starts next, what may run in parallel, and what must wait.

Hard formatting rules:

- one execution-table row equals one concrete session assignment for one epic or one named gate;
- do not put comma-separated epic IDs in a single `Epic(s)` cell;
- a closeout, review fan-in, or wave-gate decision is also a single row, not bundled with implementation epics;
- a numbered step is a chronological barrier: all rows in step N must be startable from the same accepted prerequisite frontier;
- multiple rows inside the same numbered step mean explicit parallelism;
- rows inside the same numbered step must use distinct `Session` labels;
- rows inside the same numbered step must not depend on another row in that same step;
- if one row needs another row's artifact, review, hygiene check, or gate result, put it in a later numbered step;
- if the same session owns two epics, split them into separate numbered steps even when the files do not conflict;
- same-session forked parallelism is not part of the current workflow and requires an explicit future workflow change before use;
- notes may mention related epics, but must not hide execution order by implying two epics are done in one row;
- if an older planned section still has bundled legacy rows, Meta must normalize that section to this protocol before opening it for implementation.

Good patterns:

```text
Step N
| Track B session | C2-A | Wave 1.5 ✅ | Build core shell. |
| Track E session | E2-A | Wave 1.5 ✅ | Prepare independent verification fixture. |
```

The two rows above may run in parallel only because they are different sessions and neither row waits for the other.

```text
Step N
| Track E session | CI15-B | CI15-A ✅ | Add contract CI workflow. |

Step N+1
| Track E session | CI15-C | CI15-B ✅ | Add hygiene/leak guard for that workflow. |
```

The two rows above are separate steps because the same session owns both epics and the second should inspect the first workflow artifact.

Bad patterns:

```text
| Track E session | EPIC-B, EPIC-C | EPIC-A ✅ | Add workflow and hygiene guard. |
| Meta Coordinator session | EPIC-D, EPIC-F | EPIC-B ✅ + EPIC-C ✅ | Record future slots. |
```

Both rows hide chronology by bundling multiple epics into one row. Split them before implementation starts.

### Optional Parallel Side-Lane rule

v03 introduces **Optional Parallel Side-Lanes**.

These are not required numbered steps. They are explicitly marked work lanes that may run while the main step proceeds, if Meta has spare session capacity and the prerequisite state is stable.

A side-lane is allowed only when it:

- does not block the main numbered step,
- does not require another row in the same step to finish first,
- does not modify shared contracts without Meta approval,
- can return a clean `FORK MERGE` brief,
- has a clear artifact or draft output,
- and can be abandoned without invalidating the main wave.

Side-lane outputs are not automatically absorbed. The Meta Coordinator must review them and decide whether they become:

```text
accepted into main path
deferred to later wave
kept as reference only
discarded
```

Recommended side-lane notation:

```text
OSL-W<Wave>-<Letter>
```

Example:

```text
OSL-W2-A — Track D brain skeleton scaffold while Track B builds deterministic core.
```

Side-lanes are especially useful for:

- Track A artifact-loader scaffolding,
- Track D provider/runtime skeletons,
- Track E eval fixtures and validators,
- Track C role cards or prompt-law drafts,
- later Track E review forks,
- later Track A UI forks.

Side-lanes are risky when they touch:

- shared schemas,
- source-of-truth docs,
- canonical state semantics,
- model policy locks,
- hidden/visible boundaries,
- or FP1 scope.

If uncertain, do not start a side-lane. Ask Meta to sequence it as a numbered step.

---



## 4. Global sequencing axioms

### Axiom 1 — Track B is the ground

Track B owns the deterministic core, contracts, state, action validation, state diffs, snapshots, and replay foundation.

If there is a dispute about `WorldState`, packet schemas, action validation, or state-diff truth, **Track B wins by default** until Meta decides otherwise.

### Axiom 2 — Track D and C are productive after contracts, not before them

Track C may draft role semantics and prompts early.  
Track D may scaffold model/provider runtime early.  
But neither may lock production behavior before Track B publishes the required schemas and artifact shapes.

### Axiom 3 — Track E starts early because Ringfall can self-delude easily

Eval/replay is not a late QA layer.  
The earlier the system becomes LLM-heavy, the more Track E matters.

### Axiom 4 — Track A consumes artifacts, not fantasies

Unity observer can scaffold early, but it must consume versioned artifacts, not invented state.

### Axiom 5 — FP1 verticals beat broad feature surfaces

Whenever there is a conflict between “more systems” and “make Aster/Vireo/Black Seam verticals real,” choose the vertical.

### Axiom 6 — Hidden truth separation is a hard invariant

No implementation shortcut may leak hidden truth to L1/L2/L3/public contexts incorrectly.

### Axiom 7 — Cost is designed, not discovered accidentally

Every model call should eventually produce cost and trace artifacts.  
No model-policy drift without evidence.

### Axiom 8 — FAL is future leverage, not a prerequisite

Ringfall should become a strong FAL target-project.  
But Ringfall must run independently.

---

## 5. Dependency logic in execution form

### Canonical dependency tree

```text
Track B — contracts/core/artifacts
├── Track D — provider/runtime brain
├── Track C — agent semantics/memory/prompts
├── Track E — eval/replay/cost/quality
└── Track A — Unity observer/client
```

### Operational interpretation

- **Track B** starts first and remains shared-boundary authority.
- **Track D** can scaffold configs/mock provider after schemas exist.
- **Track C** can draft role cards, but production outputs must match schemas.
- **Track E** can draft evals after schemas and must validate every vertical.
- **Track A** can start artifact-loader UI after manifest/event schemas stabilize.

### Practical unlocked order

```text
Wave 0: Meta + B first
Wave 1: B/E first, C/D/A review
Wave 2: B core
Wave 3: D/C brain path
Wave 4: B/C/D/E Aster vertical
Wave 5: C/E/B memory/replay/eval
Wave 6: C/B/D/E L2
Wave 7: C/B/D/E L3
Wave 8: all tracks cross-institution
Wave 9: E/Meta hardening
Wave 10: A observer
Wave 11: D/E/Meta remote/demo
```

---

## 6. Merge-risk zones

| Zone | Main risk | Why sequencing matters | Mitigation |
|---|---|---|---|
| MR-1 | Packet schema churn | C/D/E/A all consume packet shapes | Wave 1 contract gate before production consumers |
| MR-2 | Run manifest / artifact layout drift | Unity, replay, batch and review depend on it | Track B/E own schemas; A consumes only versioned artifacts |
| MR-3 | Core-brain boundary erosion | Brain may accidentally own truth | Track B validates all execution-impacting packets |
| MR-4 | Hidden truth leak via context builder | L1/L2/L3 collapse into omniscience | Track C/E hidden leak eval before canonical gate |
| MR-5 | Cost/model-policy drift | Always-on roster can silently become expensive | Track D/E cost traces and lane summaries |
| MR-6 | Unity truth ownership | Pretty UI may invent or simplify truth | Track A artifact-only rule; Truth View labels |
| MR-7 | L2 behavior collapse | Institutions become bland summaries or cartoon villains | Track C role/eval review; withholding and bias fields |
| MR-8 | L3 micromanagement | Council bypasses L1/L2 | L3 packet schema + authority eval |
| MR-9 | Memory contamination | rumor/belief/official line becomes fact | Claim taxonomy and memory evals |
| MR-10 | FP1 scope creep | Too many new systems before verticals prove | Meta guardrail and Risk Register |
| MR-11 | Formal-model false assurance | Refinery model drifts from Core or silently treats unsupported surfaces as valid | bounded family ids, no-overclaim matrix, differential harness vs Core validator |

---

## 7. Wave turn-gate protocol

These rules are mandatory.

1. Before starting a wave or sprint, the active session reads:
   - `docs/ops/Ringfall-Design-Canon-and-Decision-Log-v01.md`
   - `docs/ops/Ringfall-Meta-Coordinator-Handoff-Brief-v01.md`
   - `docs/plans/Combined-Execution-Sequencing-Plan.md`
   - specific subsystem docs referenced by the sprint

2. If prerequisites are not accepted, the track reports:
   - `NOT READY`
   - blocking prerequisite
   - allowed fallback work, if any

3. If prerequisites are met, the track reports:
   - `READY`
   - epic ids
   - expected touched files/areas
   - tests/evals/artifacts it will produce

4. When implementation begins:
   - epic status changes `⬜ -> 🔄`

5. When implementation is locally complete:
   - run local acceptance gate
   - if shared contract/artifact changed, request cross-track verification
   - only then mark `✅`

6. A consuming track must not silently adapt to a broken producer contract.
   - schema mismatch requires Meta-visible note
   - contract changes require affected-track list

7. Any shared schema change requires:
   - schema version note
   - valid/invalid example update
   - affected tracks listed
   - replay/eval impact noted

8. A wave is not complete until all mandatory epics are `✅` and risks are documented.

9. Public/demo packaging is never implied by implementation completion.

10. Canonical evidence requires artifacts.

---

## 8. Execution philosophy by wave

### Wave 0 — Repo/doc skeleton

Create the control surface and source-of-truth layout.

### Wave 1 — Contracts and artifact spine

Define the language all tracks speak.

### Wave 1.5 — Contract CI readiness

Protect the accepted Wave 1 contract/schema spine with a narrow CI gate before runtime implementation grows.

### Wave 2 — Deterministic core

Prove the world exists without LLMs.

### Wave 3 — Brain/model path

Prove cognition emits packets, not truth.

Formal-gate prep: output must be packet/candidate-fact shaped so later Refinery gates can consume it without trusting free-form prose.

### Wave 4 — L1 Aster vertical

Prove LLM minds / deterministic hands through the first bounded formal intervention family: Aster L1 tool/work-order proposals.

### Wave 5 — Memory/replay/eval hard gates

Prove the system can remember and replay safely, with memory/visibility formal-gate expansion if the Aster family has stabilized.

### Wave 6 — L2 institutions

Prove the middle layer is an actual institutional layer.

### Wave 7 — L3 council/doctrine

Prove macro governance without micromanagement.

Formal-gate expectation: L3 doctrine/emergency checks must remain macro-only and must not become a full governance/world model.

### Wave 8 — Cross-institution scenarios

Prove Ringfall is a coupled civilization sim, not isolated demos.

### Wave 9 — Canonical hardening

Prove FP1 headless is trustworthy.

### Wave 10 — Unity observer

Make the simulation legible and watchable.

### Wave 11 — Remote/demo readiness

Prepare practical larger runs and curated evidence-backed demos.

---

# 9. Detailed wave plan

---

## Wave 0 — Repo/doc skeleton and source-of-truth import

**Wave goal:** Create the Ringfall monorepo skeleton, canonical documentation layout, and initial coordination surface before any simulation logic is implemented.

**Primary value:** Make the future implementation track-safe: docs in correct folders, top-level repo shape present, generated data gitignored, and Wave 1 contracts unblocked.

**Primary owner(s):** Meta Coordinator + Track B

**Secondary support:** Track A/C/D/E review after skeleton exists

### Mandatory outputs
- ✅ top-level monorepo skeleton exists
- ✅ canonical docs imported into `docs/design`, `docs/ops`, `docs/plans`, `docs/creative`
- ✅ initial README states Ringfall identity, FP1 scope, and architecture stance
- ✅ `.gitignore` excludes generated runs, caches, secrets, local configs
- ✅ example config files exist without secrets

### Sprint breakdown

#### Sprint W0-S1 — Monorepo skeleton

**Owner priority:** Meta + Track B

Epics:
- ✅ **R0-A** Create top-level repo folders — **Owner: Meta/Track B**
- ✅ **R0-B** Create `.gitignore` and generated-data exclusions — **Owner: Track B**
- ✅ **R0-C** Create docs subfolders and placeholder indexes — **Owner: Meta**

Step 1 verification note, 2026-06-13:
- Meta verified the repo skeleton in verification-only mode because the folders were already present.
- Required top-level folders exist: `docs/`, `src/`, `client/`, `configs/`, `scenarios/`, `data/`, `tests/`, `tools/`, `infra/`.
- Required docs subfolders exist: `docs/design/`, `docs/ops/`, `docs/plans/`, `docs/creative/`.
- No premature implementation was found: no C# solution/projects, no Python brain project, no Unity project, no model/provider code.
- Secrets scan reported zero findings for the tracked/public skeleton surface.
- Gate result: **PASS WITH WARNINGS**. Step 2 may begin.
- Warnings carried forward: `.gitignore` has a pending local change for `.opencode-router/`; root-level `ops/` is untracked and outside the planned top-level layout; canonical Ringfall docs are present locally but ignored by git and must be handled by Step 2 import/tracking policy.

#### Sprint W0-S2 — Canonical doc import

**Owner priority:** Meta

Epics:
- ✅ **R0-D** Copy all 18 canonical docs into correct folders — **Owner: Meta**
- ✅ **R0-E** Remove/ignore duplicate `Ringfall-State-and-Observation-Model-v01 (1).md` — **Owner: Meta**
- ✅ **R0-F** Verify source-of-truth order and doc paths — **Owner: Meta**

Step 2 Meta verification note, 2026-06-13:
- Meta verified R0-D/R0-E/R0-F in verification-only mode.
- Canonical docs are physically present in the expected layout: `docs/design/`, `docs/ops/`, `docs/plans/`, and `docs/creative/`.
- Present canonical set includes the Design Canon, Meta Handoff, Implementation Wave Plan, Risk Register, World Bible, First Playable Slice, Architecture Plan, Unity UX, and the subsystem design docs.
- Duplicate `Ringfall-State-and-Observation-Model-v01 (1).md` was not found; only `docs/design/Ringfall-State-and-Observation-Model-v01.md` is present.
- Source-of-truth paths match the current canon and handoff docs.
- Current tracking policy: canonical Ringfall docs remain local/ignored by `.gitignore`; this is accepted for this Step 2 verification but remains a Meta-owned policy decision before public/export work.

#### Sprint W0-S3 — Initial README and configs

**Owner priority:** Meta + Track B/D

Epics:
- ✅ **R0-G** Write initial project README — **Owner: Meta**
- ✅ **R0-H** Add `configs/*.example.yaml` files — **Owner: Track D**
- ✅ **R0-I** Add `data/.gitkeep` and local override convention — **Owner: Track B**

### Execution Steps

**✅ Step 1**

| Session | Epic(s) | Prereq | Notes |
|---|---|---|---|
| Meta Coordinator session | R0-A, R0-C | none | Verified existing top-level skeleton and docs layout; no code logic present. PASS WITH WARNINGS on 2026-06-13. |

**✅ Step 2**

| Session | Epic(s) | Prereq | Notes |
|---|---|---|---|
| Track B session | R0-B, R0-I | R0-A ✅ | Completed. `.gitignore` covers `.opencode/`, `.opencode-router/`, `.swarm/`, `kontext/`, generated run artifacts, caches, secrets and local config overrides. `data/README.md` documents generated-data conventions. |
| Meta Coordinator session | R0-D, R0-E, R0-F | docs folders exist | Verified. Canonical docs are present in expected folders; duplicate state doc absent; source-of-truth paths match canon. |

Step 2 handoff notes:
- Treat Step 1 as accepted with warnings, not as a reason to rebuild the skeleton.
- Canonical Ringfall docs currently remain ignored/local; this is acceptable for Step 2 verification but remains a Meta-owned policy decision before public/export work.
- Root-level `ops/` runbook was routed into ignored `.swarm/runbooks/`; no tracked `ops/` folder remains.
- `configs/provider.example.yaml` is still absent and belongs to Wave 0 config completion, not Step 1 acceptance.

Step 2 gate result, 2026-06-13: **PASS WITH WARNINGS**. Step 3 may begin after commit if desired. Remaining carry-forward warning after R0-G: `configs/provider.example.yaml` remains Track D/R0-H. Resolved by R0-H on 2026-06-14.

R0-G completion note, 2026-06-14:
- README now states Ringfall identity, FP1 Aster/Vireo/Black Seam scope, headless/artifact-first architecture stance, core/brain/contracts/client/batch boundaries, guardrails, and current non-goals.
- No simulation, model provider, config, Unity, or contract implementation was added.
- R0-H remained open at this point for example config files.

R0-H completion note, 2026-06-14:
- `configs/model-policy.example.yaml`, `configs/provider.example.yaml`, and `configs/runtime.example.yaml` are present as example-only config files.
- Example defaults are mock-first: `default_provider: mock`, `openrouter.enabled: false`, and `allow_real_provider_calls: false`.
- No API keys or real provider credentials are present; provider auth is documented only through `api_key_env: OPENROUTER_API_KEY`.
- The model policy example reflects the current lane decision: use free OpenRouter models first where reliable, require `deepseek/deepseek-v4-flash` as the low-cost paid fallback, and keep helper-only small models out of authoritative decisions unless explicitly gated.

**✅ Step 3**

| Session | Epic(s) | Prereq | Notes |
|---|---|---|---|
| Meta Coordinator session | R0-G | R0-D/F ✅ | Completed. README states identity/scope/architecture and non-goals. |
| Track D session | R0-H | R0-A ✅ | Completed. Example configs only; no API keys or real provider wiring. |

**✅ Step 4**

| Session | Epic(s) | Prereq | Notes |
|---|---|---|---|
| Meta Coordinator session | Wave 0 closeout | R0-A..R0-I ✅ | Completed. Wave 0 repo skeleton, docs, README, generated-data policy, and example configs are in place. |

Wave 0 closeout note, 2026-06-14:
- All Wave 0 epics R0-A through R0-I are complete.
- Repo remains bootstrap-only: no C#/.NET solution, Python brain service, Unity project, JSON Schema implementation, model provider implementation, or simulation logic was introduced.
- Verification evidence: YAML lint passed for all three config examples; config secrets scan found zero findings; diff whitespace check passed with only CRLF normalization warnings.
- Wave 1 may start after this closeout commit, subject to Meta coordination and the canonical-doc tracking/export policy remaining explicit.

Wave 0 gate result, 2026-06-14: **PASS**.
### Optional parallel side-lanes

No implementation side-lane is recommended in Wave 0.

Allowed only as light review/reference:
- **OSL-W0-A — Track A/C/D/E doc orientation review**
  - may read the Handoff, Design Canon, Architecture Plan and this Combined Plan
  - may report path/doc issues
  - must not edit shared docs or create implementation artifacts

Reason: Wave 0 is small and source-of-truth sensitive. More parallelism would create coordination overhead without meaningful acceleration.

### Wave gate

Wave 0 passes when the repo skeleton exists, docs are placed correctly, generated data/secrets are excluded, and no implementation work has started prematurely.

### Hold conditions
- canonical docs missing or misplaced
- duplicate state doc copied as canon
- secrets committed
- simulation/model/Unity work started before skeleton closeout

---

## Wave 1 — Contracts and artifact spine

**Wave goal:** Create the canonical JSON Schema contract surface and minimum artifact bundle shape that all implementation tracks consume.

**Primary value:** Prevent downstream track chaos by locking the basic packet, trace, memory, cost, eval, and manifest shapes before runtime behavior grows.

**Primary owner(s):** Track B + Track E

**Secondary support:** Track C/D/A review against schemas

### Mandatory outputs
- ✅ `src/ringfall-contracts/schemas/` exists
- ✅ minimum packet schemas exist
- ✅ minimum trace/artifact schemas exist
- ✅ valid/invalid schema examples exist
- ✅ schema validation tool exists
- ✅ contract review notes from C/D/E/A are recorded

### Sprint breakdown

#### Sprint W1-S1 — Contract layout and schema skeleton

**Owner priority:** Track B

Epics:
- ✅ **C1-A** Create schema folder structure — **Owner: Track B**
- ✅ **C1-B** Create versioning/readme notes — **Owner: Track B**

W1-S1 closeout note, 2026-06-14:
- Track B created the approved `src/ringfall-contracts/` layout, schema group folders, and versioning/readme notes only.
- No `*.schema.json` files, schema bodies, examples, validation tools, runtime code, configs, scenarios, Unity work, provider/model calls, or simulation logic were added.
- Step review verdict: GREEN. C1-A/C1-B are accepted as the Wave 1 contract layout baseline.
- C1-C/C1-D remain the next schema definition step; Track C/D/E/A are not activated until their prerequisite schema drafts exist.

#### Sprint W1-S2 — Core packet schemas

**Owner priority:** Track B + Track C

Epics:
- ✅ **C1-C** AvatarPulsePacket / SceneActionPacket — **Owner: Track B/C**
- ✅ **C1-D** WorkOrderRequest / ToolActionRequest / ExecutionResult — **Owner: Track B**
- ✅ **C1-E** InstitutionBrief/Order and CouncilDoctrinePacket — **Owner: Track B/C**

W1-S2 closeout note, 2026-06-14:
- Track B added five Draft 2020-12 packet schema drafts under `src/ringfall-contracts/schemas/packets/`: `AvatarPulsePacket`, `SceneActionPacket`, `WorkOrderRequest`, `ToolActionRequest`, and `ExecutionResult`.
- Step review plus Swarm review found under-constrained action/result/work-order shapes; Track B fixed those before commit with action-type conditionals, work-order target requirements, hidden-effect required fields, and observable summary requirements.
- No C1-E institution/council schemas, examples, validation tooling, runtime code, configs, scenarios, Unity work, provider/model calls, or simulation logic were added.
- C1-I/C1-J must later include invalid fixtures for packet constants, action conditionals, work-order targeting, hidden-effect completeness, malformed IDs/refs, invalid enums, and numeric bounds.
- W1-S3/C1-E was the next gated target after W1-S2 and is now accepted.

W1-S3 closeout note, 2026-06-14:
- Track C added `docs/plans/W1-S3-C1-E-Track-C-Packet-Usability-Review.md` as the packet-usability review and handoff artifact.
- Track B added three Draft 2020-12 packet schema drafts under `src/ringfall-contracts/schemas/packets/`: `InstitutionBrief`, `InstitutionOrder`, and `CouncilDoctrinePacket`.
- Step review plus Swarm review found a withholding traceability gap in `InstitutionBrief`; Track B fixed it before commit with a conditional `withholding_record_refs` requirement when `withheld_items_count >= 1`.
- Semantic validation debt for recommended-order traceability, macro-order policy-level constraints, and emergency-measure bounded duration/sunset constraints is routed to C1-I/C1-J invalid fixtures.
- No examples, validation tooling, runtime code, configs, scenarios, Unity work, provider/model calls, trace schemas, memory schemas, eval schemas, or simulation logic were added.
- W1-S4/C1-F,C1-G was the next gated target after W1-S3 and is now accepted.

W1-S4 closeout note, 2026-06-15:
- Track B added six Draft 2020-12 schema drafts under `src/ringfall-contracts/schemas/`: `RunManifest`, `CognitionTrace`, `ActionTrace`, `StateDiff`, `ClaimRecord`, and `MemoryUpdate`.
- Step review plus Swarm review found a canonical claim taxonomy drift in `ClaimRecord`; Track B fixed it before commit by aligning `claim_type` with the memory canon and removing non-canon `order_context`.
- Semantic validation debt for quarantined memory updates, field-specific trace ref integrity, action validation/execution-status consistency, state path/visibility consistency, and run track uniqueness/role constraints is routed to C1-I/C1-J invalid fixture and validation-tooling work.
- No examples, validation tooling, runtime code, configs, scenarios, Unity work, provider/model calls, cost schemas, eval schemas, or simulation logic were added.
- W1-S5/C1-H was the next gated target after W1-S4 and is now accepted.

W1-S5 closeout note, 2026-06-15:
- Track D added the Draft 2020-12 `CostEvent` schema under `src/ringfall-contracts/schemas/traces/cost-event.schema.json` for cost/model/provider evidence.
- Track E added the Draft 2020-12 `EvalSummary` schema under `src/ringfall-contracts/schemas/eval/eval-summary.schema.json` for replay/eval summary evidence.
- Step review plus Swarm review found a shared README inventory drift after split-lane implementation; Track E reconciled `src/ringfall-contracts/schemas/README.md` to document both C1-H lanes before commit.
- Semantic validation debt for CostEvent fallback/cost consistency, EvalSummary gate/count consistency, source-ref vocabulary integrity, and held-run gate reasons is routed to C1-I/C1-J fixture and validation-tooling work.
- No examples, validation tooling, runtime code, configs, scenarios, Unity work, provider/model calls, runtime cost collection, eval runner code, or simulation logic were added.
- W1-S6/C1-I,C1-J was the next gated target after W1-S5 and is now accepted.

W1-S6 closeout note, 2026-06-17:
- Track E added manifest-backed valid/invalid contract fixtures under `src/ringfall-contracts/examples/`, with exactly one valid fixture per current schema and targeted invalid fixtures for routed W1 validation debt.
- Track E added `tools/schema_check.py` plus `requirements-dev.txt` for dev-only Draft 2020-12 schema and fixture validation.
- Step review found a CostEvent semantic overlap in `fallback-missing-reason.json`; Track E fixed the fixture and hardened `tools/schema_check.py` so semantic invalid fixtures fail on missing or extra semantic reason codes.
- Shared contract docs now distinguish W1-S6 static fixture/tool coverage from deferred runtime/artifact-context validation debt and route residual semantic review to W1-S7/C1-K plus later runtime/artifact validation gates.
- No schema body edits, runtime code, provider/model behavior, Unity work, scenarios, generated artifacts, eval runner code, runtime cost collection, or simulation logic were added.
- W1-S7/C1-K is the next gated target for cross-track contract handoff review.

W1-S7 closeout note, 2026-06-18:
- Meta published `docs/plans/W1-S7-C1-K-Contract-Handoff-Review-Packet.md` as the shared C1-K review packet and final handoff synthesis.
- Required parallel reviews completed: Track B returned low-risk routed concerns, Track D approved, and Track E approved.
- Optional parallel reviews completed: Track C approved with routed memory-example breadth concerns, and Track A approved with routed future loader-bundle concerns.
- All accepted C1-K concerns are routed to named owners and future gates; no Track returned a blocking finding.
- Wave 1 contract handoff is accepted for Wave 2/3 planning without adding runtime code, provider/model behavior, Unity work, scenarios, generated artifacts, eval runner code, runtime cost collection, or simulation logic.

Post-W1 pre-Wave-2 cleanup note:
- Track B resolved the C1-K `ref_type` checker-scope concern by scoping `tools/schema_check.py` source-ref vocabulary validation to known reference structures instead of arbitrary objects with `ref_type`.
- Wave 1 remains closed and accepted; this cleanup hardens handoff quality before Wave 2 and does not approve runtime/provider/model/Unity/scenario/eval-runner/simulation work.
- Remaining C1-K routes stay active: Track C positive memory examples, Track A connected artifact bundle graph, Track D real CostEvent provider evidence reconciliation, and Track E EvalEvent/eval-runner decision.

#### Sprint W1-S3 — Trace, memory, cost, eval schemas

**Owner priority:** Track B + Track E + Track D

Epics:
- ✅ **C1-F** RunManifest / CognitionTrace / ActionTrace — **Owner: Track B/E**
- ✅ **C1-G** StateDiff / MemoryUpdate / ClaimRecord — **Owner: Track B/C/E**
- ✅ **C1-H** CostEvent / EvalSummary schemas — **Owner: Track D/E**

#### Sprint W1-S4 — Examples and validation tool

**Owner priority:** Track E + Track B

Epics:
- ✅ **C1-I** Valid/invalid examples — **Owner: Track E/B**
- ✅ **C1-J** `tools/schema_check.py` — **Owner: Track E**
- ✅ **C1-K** Cross-track contract review — **Owner: Meta + all tracks**

### Execution Steps

**✅ Step 1**

| Session | Epic(s) | Prereq | Notes |
|---|---|---|---|
| Track B session | C1-A, C1-B | Wave 0 ✅ | Complete: contract home and versioning/readme notes created without packet details. |

**✅ Step 2**

| Session | Epic(s) | Prereq | Notes |
|---|---|---|---|
| Track B session | C1-C, C1-D | C1-A/B ✅ | Complete: five core packet schema drafts accepted after review fixes. |

**✅ Step 3**

| Session | Epic(s) | Prereq | Notes |
|---|---|---|---|
| Track C session | review L1/action packet draft | Step 2 packet draft accepted by Track B | Complete: packet usability review/handoff artifact added. |
| Track B session | C1-E | C1-C/D ✅ | Complete: institution/council packet schema drafts accepted after review fix. |

**✅ Step 4**

| Session | Epic(s) | Prereq | Notes |
|---|---|---|---|
| Track B session | C1-F, C1-G | C1-C/D/E ✅ | Complete: six trace/state/memory schema drafts accepted after review fix. |

**✅ Step 5**

| Session | Epic(s) | Prereq | Notes |
|---|---|---|---|
| Track D session | C1-H cost-event side | C1-F/G draft accepted | Complete: CostEvent schema accepted after shared README reconciliation. |
| Track E session | C1-H eval-summary side | C1-F/G draft accepted | Complete: EvalSummary schema accepted after shared README reconciliation. |

**✅ Step 6**

| Session | Epic(s) | Prereq | Notes |
|---|---|---|---|
| Track E session | C1-I, C1-J | schemas exist and C1-H reviewed ✅ | Complete: fixtures and validation tool accepted after semantic-isolation review fix. |

**✅ Step 7**

| Session | Epic(s) | Prereq | Notes |
|---|---|---|---|
| Meta Coordinator session | C1-K + Wave 1 gate | schema check passes | Complete: contract handoff accepted for Wave 2/3 planning with routed concerns. |
### Optional parallel side-lanes

These may run after the relevant schema draft exists.

- **OSL-W1-A — Track A artifact-loader assumptions draft**
  - prereq: `run-manifest` and `event_log` schema draft exists
  - output: short note on what Unity artifact loader will need
  - must not modify schemas

- **OSL-W1-B — Track C role/prompt schema usability review**
  - prereq: L1/action packet schema draft exists
  - output: role/prompt usability findings
  - must not fork packet formats

- **OSL-W1-C — Track D model-policy/cost-event review**
  - prereq: `cost-event` and `cognition-trace` schemas exist
  - output: provider/runtime field sufficiency notes
  - must not add provider-specific behavior to core contracts without Track B/Meta approval

- **OSL-W1-D — Track E eval fixture planning**
  - prereq: eval summary schema draft exists
  - output: list of first invalid examples needed for E0/E1/E2 checks
  - can be absorbed in Step 6

### Wave gate

Wave 1 passes when minimum schemas and examples validate, and downstream tracks can implement against a documented artifact surface.

### Hold conditions
- packet schema allows direct world mutation
- trace/cost/memory source refs absent
- schema examples fail
- cost/model fields contradict the free-first plus `deepseek/deepseek-v4-flash` fallback policy
- Track C/D/E/A reject handoff as unusable

---

## Wave 1.5 — Contract CI readiness

**Wave goal:** Add the first official Ringfall CI spine around the accepted Wave 1 contract/schema validator, without opening runtime, provider, Unity, scenario, or simulation implementation.

**Primary value:** Make the contract layer mechanically protected before Wave 2 deterministic core work starts, so later runtime implementation does not inherit a fragile or manually-only schema gate.

**Primary owner(s):** Meta Coordinator + Track E

**Secondary support:** Track B for contract/tooling compatibility; Track D/A/C review only if CI scope touches their future surfaces.

### Mandatory outputs
- ✅ CI readiness contract for the current Ringfall skeleton (`docs/plans/Wave-1.5-CI15-A-CI-Readiness-Contract.md`)
- ✅ GitHub Actions contract CI that runs `python tools/schema_check.py` (`.github/workflows/contract-ci.yml`)
- ✅ hygiene/leak boundary guard and durable proof coverage for CI artifacts and ignored private/local state
- ✅ future runtime CI slot map for C# core, Python brain, Unity client, and scenario replay
- ✅ future formal-intervention CI slot map for Refinery families, kept inactive until a named family has fixtures and a differential harness
- ✅ coverage policy kept report-only/later until runtime modules and test corpus exist

### Sprint breakdown

#### Sprint W1.5-S1 — Contract CI scope and workflow

**Owner priority:** Meta + Track E

Epics:
- ✅ **CI15-A** CI readiness contract — **Owner: Meta/Track E**
- ✅ **CI15-B** GitHub Actions contract CI — **Owner: Track E**
- ✅ **CI15-C** CI hygiene/leak guard — **Owner: Track E/Meta**

#### Sprint W1.5-S2 — Future CI policy slots

**Owner priority:** Meta + Track E

Epics:
- ✅ **CI15-D** Future runtime CI slot map — **Owner: Meta**
- ✅ **CI15-F** Future formal-intervention CI slot map — **Owner: Meta/Track E**
- ✅ **CI15-E** Coverage policy later/report-only — **Owner: Track E**

### Execution Steps

**✅ Step 1 — Scope lock and local verify contract**

| Session | Epic(s) | Prereq | Notes |
|---|---|---|---|
| Meta Coordinator session | CI15-A | Wave 1 ✅ | Completed in `docs/plans/Wave-1.5-CI15-A-CI-Readiness-Contract.md`. Official local verify command is `python -m pip install -r requirements-dev.txt` then `python tools/schema_check.py`. This is contract CI, not runtime CI. |

CI15-A closeout note, 2026-07-01:
- Meta published `docs/plans/Wave-1.5-CI15-A-CI-Readiness-Contract.md` as the accepted local contract-CI readiness contract.
- Official local verify sequence is `python -m pip install -r requirements-dev.txt` followed by `python tools/schema_check.py`.
- CI15-A defines scope only; it does not add GitHub Actions workflow files, runtime code, provider/model behavior, Unity work, scenarios, simulation logic, coverage hard gates, artifact upload, Refinery tooling, or formal-solver CI.
- Local verification evidence at closeout: `python -m pip install -r requirements-dev.txt` confirmed the dev dependency is satisfied, and `python tools/schema_check.py` passed against 16 schemas and 41 fixture entries.
- CI15-B is unblocked for Track E to implement the first reviewed contract CI workflow; CI15-C follows in the next sequential Track E step after the workflow artifact exists.

**✅ Step 2 — First CI implementation**

| Session | Epic(s) | Prereq | Notes |
|---|---|---|---|
| Track E session | CI15-B | CI15-A ✅ | Completed in `.github/workflows/contract-ci.yml`. The workflow runs the Wave 1 contract schema checker on push/PR, installs only `requirements-dev.txt`, uses read-only repository permissions, and does not use secrets, provider calls, Unity, C# runtime, OpenCode sessions, or private artifact upload. CI15-C waits for this workflow artifact. |

CI15-B closeout note, 2026-07-01:
- Track E added `.github/workflows/contract-ci.yml` as the first reviewed contract CI workflow.
- The workflow runs on `push` and `pull_request`, checks out the repo, sets up Python 3.12, runs `python -m pip install -r requirements-dev.txt`, then runs `python tools/schema_check.py`.
- The workflow sets `permissions: contents: read` and does not configure secrets, provider/model credentials, artifact upload, OpenCode sessions, Unity, C#/.NET runtime, scenarios, simulation logic, coverage hard gates, or Refinery/formal-solver CI.
- Local verification evidence at closeout: `python -m pip install -r requirements-dev.txt` confirmed the dev dependency is satisfied, and `python tools/schema_check.py` passed against 16 schemas and 41 fixture entries.
- CI15-C is the next sequential Track E step for the CI hygiene/leak guard against this workflow artifact.

**✅ Step 3 — CI hygiene/leak guard**

| Session | Epic(s) | Prereq | Notes |
|---|---|---|---|
| Track E session | CI15-C | CI15-B ✅ | Completed in `.github/workflows/contract-ci.yml`, `tools/ci_hygiene_check.py`, and `tools/ci_hygiene_check_tests.py`. The workflow now runs the hygiene guard before `tools/schema_check.py`; the guard enforces the frozen CI15-C invariants with durable stdlib proof coverage. |

CI15-C fix-loop note, 2026-07-02:
- Track E added `tools/ci_hygiene_check.py` as a stdlib-only guard for the contract CI workflow.
- `.github/workflows/contract-ci.yml` now runs `python tools/ci_hygiene_check.py` after installing dev requirements and before `python tools/schema_check.py`.
- RED step-review found false negatives in active run command/order checks, guard self-invocation enforcement, token/model scope bans, and negative proof coverage.
- Track E fix requires active `run:` line validation, command order enforcement, broader token/model bans, and deterministic negative proofs before re-review.
- CI15-D and CI15-E remain blocked until CI15-C receives Meta GREEN; Wave 2 remains blocked until the Wave 1.5 gate or an explicit Meta CI-debt exception.

CI15-C closeout note, 2026-07-03:
- Track E hardened `tools/ci_hygiene_check.py` to enforce active workflow run order, allowed step-level actions, exact top-level `permissions: contents: read`, no job-level reusable workflow `uses`, no active `secrets:` mappings, credential/runtime hard bans, and private/local path boundaries.
- Track E added `tools/ci_hygiene_check_tests.py` as durable stdlib-only proof coverage for the accepted RED findings and prior spoof classes, including wrong-scope `steps:`, nested `with.run`/`env.run`, scalar chomping variants, `apikey`, job-level `uses`, `secrets: inherit`, and permissions failures.
- Closeout verification passed: `python -m py_compile tools/ci_hygiene_check.py tools/ci_hygiene_check_tests.py`, `python tools/ci_hygiene_check_tests.py`, `python tools/ci_hygiene_check.py`, `python tools/schema_check.py`, diff hygiene, secrets scans, SAST on the CI guard files, and generated-cache absence checks.
- CI15-D and CI15-E are unblocked as the next sequential Wave 1.5 planning targets; Wave 2 remains blocked until the Wave 1.5 gate or an explicit Meta CI-debt exception.

CI review-scope freeze note:
- For CI15-C and later narrow CI guard slices, Meta should freeze a short acceptance contract before implementation review or re-review.
- The frozen contract should record the exact invariants that must pass plus the explicit out-of-scope CI/policy surfaces for that slice.
- Step review and Swarm review should block only on a direct bypass or violation of one frozen invariant unless Meta explicitly records a contract refreeze in this Combined plan.
- Broader GitHub Actions hardening concerns should route to later CI hardening debt by default rather than silently widening the current CI15-C acceptance bar.
- After two RED review-fix loops on one narrow CI slice, Meta should either refreeze the contract or stop the loop with an explicit owner-visible decision.

**✅ Step 4 — Future runtime and coverage slots**

| Session | Epic(s) | Prereq | Notes |
|---|---|---|---|
| Meta Coordinator session | CI15-D | CI15-C ✅ | Completed as a docs-only slot map. Future runtime CI lanes are named but blocked until their runtime surfaces and local verify commands exist: `core-dotnet-ci`, `brain-python-ci`, `unity-client-ci`, and `scenario-replay-ci`. |
| Track E session | CI15-E | CI15-C ✅ | Completed as a docs-only coverage policy record. Coverage is report-only/later and must not become a hard threshold before stable runtime modules and a representative test corpus exist. |

CI15-D closeout note, 2026-07-03:
- Meta recorded future runtime CI lane slots only; no GitHub Actions workflow, runtime code, provider/model behavior, Unity project, scenario runner, replay runner, coverage gate, or simulation logic was added.
- `core-dotnet-ci` is blocked until Track B creates the deterministic C#/.NET core surface and an accepted local verify command.
- `brain-python-ci` is blocked until Track D/Track C create the Python brain/provider-runtime surface and an accepted non-secret local verify command.
- `unity-client-ci` is blocked until Track A creates the Unity client surface and an accepted local verify command.
- `scenario-replay-ci` is blocked until scenario/replay runner surfaces exist and artifact/replay evidence rules are accepted.
- Future activation of any runtime CI lane must preserve the existing contract-CI boundary: no private artifact upload, no implicit provider/model secrets, no OpenCode/FAL session execution, and no claim that green CI is domain approval.

CI15-E closeout note, 2026-07-03:
- Track E recorded coverage as report-only/later policy only; no workflow, tool, test, coverage report, badge, CI job, runtime code, provider/model behavior, Unity project, scenario runner, replay runner, simulation logic, Refinery tooling, or solver CI was added.
- Coverage absence must not fail Wave 1.5 CI.
- Coverage must not become a hard threshold before stable runtime modules and a representative test corpus exist.
- Future coverage activation is lane-by-lane after the relevant runtime lane has an accepted local verify command and stable test corpus evidence; it is not a global repository threshold.
- Green contract CI remains mechanical evidence only, not domain approval or runtime readiness.

**✅ Step 5 — Future formal-intervention CI slot**

| Session | Epic(s) | Prereq | Notes |
|---|---|---|---|
| Meta Coordinator session | CI15-F | CI15-D ✅ + CI15-E ✅ | Completed as a docs-only slot map. The future `formal-intervention-ci` lane is named but blocked until a named Refinery family has fixtures, bridge/core differential checks, and no unsupported silent accepts. |

CI15-F closeout note, 2026-07-03:
- Meta recorded the future `formal-intervention-ci` lane only; no GitHub Actions workflow, Refinery runtime integration, solver invocation, Docker/Java dependency, bridge mapper, C#/.NET runtime code, Python brain runtime code, provider/model behavior, Unity project, scenario runner, replay runner, coverage gate, or simulation logic was added.
- The lane remains blocked until a named bounded intervention family exists with accepted fixtures, JSON Schema checks, Refinery model evidence, bridge/core differential checks, and explicit unsupported-surface handling.
- First intended hardening target remains a later Aster L1 `ToolActionRequest` / `WorkOrderRequest` intervention family, not Wave 1.5 contract CI.
- Future activation must preserve the no-overclaim boundary: `Refinery valid` is never domain approval or full-world correctness, and unsupported surfaces must not be silently accepted.
- Wave 1.5 contract CI remains limited to mechanical schema/hygiene evidence around `tools/schema_check.py` and does not run formal-intervention tooling.

**⬜ Step 6 — Wave 1.5 closeout gate**

| Session | Epic(s) | Prereq | Notes |
|---|---|---|---|
| Meta Coordinator session | Wave 1.5 gate | CI15-D ✅ + CI15-E ✅ + CI15-F ✅ | Decide whether Wave 2 planning may proceed with contract CI in place or with explicit CI-debt rationale. |

### Wave gate

Wave 1.5 passes when Ringfall has a narrow contract CI workflow protecting `tools/schema_check.py`, private/local state boundaries are preserved, future runtime/formal-intervention CI lanes are documented, and no runtime/provider/Unity/simulation/formal-solver scope has opened.

### Hold conditions
- CI requires secrets or provider/model credentials
- CI runs OpenCode sessions
- CI uploads private/local FAL, `.opencode`, `.swarm`, `data/runs`, or design-canon artifacts
- CI tries to build non-existent C#/.NET, Python brain, Unity, provider, scenario, or simulation surfaces
- CI tries to run Refinery/formal intervention checks before a named family, fixtures, bridge, and differential harness exist
- coverage is introduced as a broad hard gate before runtime modules exist
- green CI is represented as domain/FAL approval instead of mechanical evidence

---

## Wave 2 — Deterministic core and first headless state

**Wave goal:** Build the first C#/.NET headless deterministic simulation core that can load a scenario, tick, write event logs, and emit state diffs without LLMs.

**Primary value:** Prove the sim exists independently of models and UI.

**Primary owner(s):** Track B

**Secondary support:** Track E for artifact smoke; Track A may start artifact-reader prep after manifest shape

### Mandatory outputs
- ⬜ C# solution with `Ringfall.Core`, `Ringfall.Headless`, tests
- ⬜ initial FP1 state subset loads
- ⬜ T0 tick executes deterministically
- ⬜ Aster Heat Alarm seed produces event/state diff
- ⬜ run manifest, initial/final snapshots, event log, state diffs written

### Sprint breakdown

#### Sprint W2-S1 — C# solution and headless runner

**Owner priority:** Track B

Epics:
- ⬜ **K2-A** Create solution/projects — **Owner: Track B**
- ⬜ **K2-B** Headless CLI skeleton — **Owner: Track B**

#### Sprint W2-S2 — FP1 state subset

**Owner priority:** Track B

Epics:
- ⬜ **K2-C** Aster R2/R5/R6/R10 state subset — **Owner: Track B**
- ⬜ **K2-D** Actor/crew basic state — **Owner: Track B**
- ⬜ **K2-E** Vireo/Morrow/Black Seam stubs — **Owner: Track B**

#### Sprint W2-S3 — Deterministic tick and artifacts

**Owner priority:** Track B + Track E

Epics:
- ⬜ **K2-F** T0 tick deterministic update — **Owner: Track B**
- ⬜ **K2-G** Event log/state diff writers — **Owner: Track B**
- ⬜ **K2-H** Core artifact smoke validation — **Owner: Track E**

### Execution Steps

**⬜ Step 1**

| Session | Epic(s) | Prereq | Notes |
|---|---|---|---|
| Track B session | K2-A, K2-B | Wave 1 ✅ + Wave 1.5 ✅ or explicit Meta CI-debt exception | Create buildable C# core/headless shell; no brain/provider/Unity refs. |

**⬜ Step 2**

| Session | Epic(s) | Prereq | Notes |
|---|---|---|---|
| Track B session | K2-C, K2-D, K2-E | K2-A ✅ | Add minimal state model and scenario loader. |

**⬜ Step 3**

| Session | Epic(s) | Prereq | Notes |
|---|---|---|---|
| Track B session | K2-F, K2-G | K2-C/D/E ✅ | Implement deterministic tick, Aster seed, and artifact writers. |

**⬜ Step 4**

| Session | Epic(s) | Prereq | Notes |
|---|---|---|---|
| Track E session | K2-H | K2-G ✅ | Validate artifact bundle against schemas. |

**⬜ Step 5**

| Session | Epic(s) | Prereq | Notes |
|---|---|---|---|
| Meta Coordinator session | Wave 2 closeout | K2-H ✅ | Confirm core is independent and Wave 3 can call into stable contexts. |
### Optional parallel side-lanes

These can run after Wave 1 contract gate, while Track B builds the deterministic core.

- **OSL-W2-A — Track D brain skeleton scaffold**
  - prereq: Wave 1 contract gate
  - output: Python project skeleton notes or non-invasive scaffold plan
  - must not depend on core runtime
  - should not make real OpenRouter calls

- **OSL-W2-B — Track E artifact bundle validator draft**
  - prereq: artifact schemas exist
  - output: validator/test plan against sample bundles
  - may use fixtures only
  - must carry the Track A C1-K route forward if it touches bundle examples: before serious loader work, a connected sample bundle should demonstrate manifest -> cognition/action -> state/eval relationships

- **OSL-W2-C — Track A Unity artifact-loader skeleton planning**
  - prereq: manifest/event schema stable enough
  - output: view-model and loader assumptions
  - no Unity sim truth, no gameplay
  - must carry the Track A C1-K route forward: connected artifact bundle graph assumptions need Track B/E review if they affect contracts or validation

- **OSL-W2-D — Track B/E Refinery formal-gate spike planning**
  - prereq: Wave 1.5 future formal-intervention CI slot recorded; no runtime dependency required
  - output: design-only candidate for the F0 artifact-bundle graph family and F1 Aster L1 action/work-order family
  - must not add Refinery runtime dependency, Docker/Java wiring, solver CI, C# bridge, Python bridge, or schema-body changes
  - must classify every proposed check as `proved_by_refinery`, `guarded_by_core_validator`, `schema_only`, `observability_only`, or `unsupported`

These side-lanes are useful because they touch separate paths. They must return merge briefs before being absorbed into Wave 3/10 work.

### Wave gate

Wave 2 passes when a no-LLM Aster seed run is deterministic and writes valid manifest/snapshot/event/state-diff artifacts.

### Hold conditions
- core references brain/OpenRouter/Unity
- state cannot serialize
- determinism not controlled
- no artifact output
- artifact bundle validation ignores the routed C1-K loader-graph concern without marking it in-scope, not-yet-in-scope, or already resolved
- Refinery/formal-gate planning claims full-world validation or creates a hard Wave 2 dependency

---

## Wave 3 — Brain service, model policy, mock/OpenRouter cognition path

**Wave goal:** Create Python brain service/CLI with model policy, mock provider, strict JSON parsing, cognition traces, and optional OpenRouter path.

**Primary value:** Prove cognition can happen without touching sim truth directly.

**Primary owner(s):** Track D + Track C

**Secondary support:** Track B schema compatibility; Track E trace/cost checks

### Mandatory outputs
- ⬜ Python brain project skeleton
- ⬜ model policy loader
- ⬜ mock provider
- ⬜ OpenRouter provider shell
- ⬜ strict JSON parser/validator
- ⬜ designated output-area discipline for future formal intervention gates: model output is packet/candidate-fact shaped, not direct patches
- ⬜ cognition trace writer
- ⬜ cost tracker
- ⬜ L1 pulse prompt/context template

### Sprint breakdown

#### Sprint W3-S1 — Brain skeleton and model policy

**Owner priority:** Track D

Epics:
- ⬜ **B3-A** Python project + CLI — **Owner: Track D**
- ⬜ **B3-B** Model policy loader and run mode handling — **Owner: Track D**

#### Sprint W3-S2 — Provider and strict output

**Owner priority:** Track D

Epics:
- ⬜ **B3-C** Mock provider returns valid packet — **Owner: Track D**
- ⬜ **B3-D** Strict JSON schema parser/validator — **Owner: Track D**
- ⬜ **B3-E** OpenRouter provider shell/env handling — **Owner: Track D**

#### Sprint W3-S3 — First L1 cognition semantics

**Owner priority:** Track C + Track D/E

Epics:
- ⬜ **B3-F** L1 pulse context/prompt template — **Owner: Track C**
- ⬜ **B3-G** Cognition trace + cost event — **Owner: Track D**
- ⬜ **B3-H** Brain artifact smoke — **Owner: Track E**

### Execution Steps

**⬜ Step 1**

| Session | Epic(s) | Prereq | Notes |
|---|---|---|---|
| Track D session | B3-A, B3-B | Wave 1 ✅ | Create brain project and load lane policy; no real API required. |

**⬜ Step 2**

| Session | Epic(s) | Prereq | Notes |
|---|---|---|---|
| Track D session | B3-C, B3-D | B3-A/B ✅ | Mock provider + strict JSON validation first. |
| Track C session | B3-F draft | packet schema exists | Draft L1 pulse context with no hidden truth. |

**⬜ Step 3**

| Session | Epic(s) | Prereq | Notes |
|---|---|---|---|
| Track D session | B3-E, B3-G | B3-D ✅ and B3-F draft accepted | Add OpenRouter shell and trace/cost logging; real call optional/manual. |

**⬜ Step 4**

| Session | Epic(s) | Prereq | Notes |
|---|---|---|---|
| Track E session | B3-H | B3-G ✅ | Validate cognition trace/cost event shape. |

**⬜ Step 5**

| Session | Epic(s) | Prereq | Notes |
|---|---|---|---|
| Meta Coordinator session | Wave 3 closeout | B3-H ✅ | Confirm cognition path emits packets only and cannot mutate state. |
### Optional parallel side-lanes

- **OSL-W3-A — Track B core context export prep**
  - prereq: Wave 2 core state subset exists
  - output: note/draft for how core will expose observable context packets
  - useful for Wave 4
  - must not change schemas without Wave 1 contract protocol

- **OSL-W3-B — Track C A1 role-card draft**
  - prereq: actor census and packet schema exists
  - output: A1 senior grid runner draft role card
  - can feed Wave 4 Step 1

- **OSL-W3-C — Track E cognition trace eval smoke draft**
  - prereq: cognition trace schema exists
  - output: deterministic checks for schema/cost trace presence

Wave 3 carry-forward routes:
- Track D owns the C1-K CostEvent provider/model evidence route. At the first provider/runtime implementation or runtime artifact bundle validation gate, CostEvent must be reconcilable with real provider request/response evidence before canonical provider evidence.
- Track C owns the C1-K positive memory example route if Wave 3 expands prompt-memory fixtures. Positive examples for rumor, belief, official_line, and withheld_item must be added or explicitly judged not yet in scope, with Track B/E review if schemas, examples, or tooling are touched.
- Track D/C own the future formal-gate output-area discipline: brain responses must remain strict packet/candidate-fact outputs that can be rejected, repaired, or classified unsupported before any Core apply.

Do not start full L1 scene semantics before B3-F draft and strict JSON path are reviewed.

### Wave gate

Wave 3 passes when mock cognition creates a schema-valid L1 pulse and a cognition/cost trace; OpenRouter path exists but may remain manual.

### Hold conditions
- brain mutates world state
- hidden truth appears in L1 context
- no cost trace
- strict JSON not enforced
- model output bypasses the future formal-gate-compatible candidate output area by emitting direct world patches or unbounded commands
- real API key committed
- real/manual provider evidence cannot be reconciled to CostEvent records once provider calls are enabled

---

## Wave 4 — L1 action/tool/crew vertical: Aster Heat Alarm

**Wave goal:** Build the first real Ringfall vertical: A1 sees an Aster heat alarm, emits cognition, uses tool/crew packets, core validates/executes, and artifacts prove it.

**Primary value:** Prove LLM minds + deterministic hands in a live FP1 scenario.

**Primary owner(s):** Track B + Track C + Track D + Track E

**Secondary support:** Track A may consume artifacts in parallel

### Mandatory outputs
- ⬜ Aster actor/context data
- ⬜ A1/A2/A4/A6/A9 minimal actors
- ⬜ crew_aster_repair_02
- ⬜ local_grid_panel + maintenance_console minimal
- ⬜ WorkOrderRequest/ToolAction execution
- ⬜ Aster F1 formal intervention gate for L1 ToolAction/WorkOrder candidate facts, report-only during integration and hard before Wave 4 closeout
- ⬜ ExecutionResult/VisibilityPartition
- ⬜ Aster cognition/action/state/memory/cost traces

### Sprint breakdown

#### Sprint W4-S1 — Aster actor and tool context

**Owner priority:** Track B + Track C

Epics:
- ⬜ **A4-A** Aster actor records and local observations — **Owner: Track C/B**
- ⬜ **A4-B** crew_aster_repair_02 and crew state — **Owner: Track B**
- ⬜ **A4-C** local_grid_panel / maintenance_console minimal contracts — **Owner: Track B**

#### Sprint W4-S2 — L1 scene cognition and action validation

**Owner priority:** Track C + Track D + Track B

Epics:
- ⬜ **A4-D** A1 pulse/scene prompt and context — **Owner: Track C**
- ⬜ **A4-E** Brain emits ToolAction/WorkOrder packets — **Owner: Track D**
- ⬜ **A4-F** Core validates authority/tool/crew packets — **Owner: Track B**
- ⬜ **A4-J** Aster F1 formal intervention gate — **Owner: Track B/E**

#### Sprint W4-S3 — End-to-end Aster Heat Alarm

**Owner priority:** Track B/D/E

Epics:
- ⬜ **A4-G** Execute tool/crew effects and state diff — **Owner: Track B**
- ⬜ **A4-H** Write cognition/action/cost artifacts — **Owner: Track D/B**
- ⬜ **A4-I** Aster vertical eval smoke — **Owner: Track E**

### Execution Steps

**⬜ Step 1**

| Session | Epic(s) | Prereq | Notes |
|---|---|---|---|
| Track C session | A4-A | Wave 2/3 ✅ | Define A1/Aster context against actor census and hidden truth boundaries. |
| Track B session | A4-B, A4-C | Wave 2 ✅ | Implement crew and tool surfaces, but no LLM logic. |

**⬜ Step 2**

| Session | Epic(s) | Prereq | Notes |
|---|---|---|---|
| Track C session | A4-D | A4-A ✅ | Write L1 prompt/context rules. |
| Track B session | A4-F, A4-J draft | A4-B/C ✅ | Implement authority/tool/crew validation and the Aster F1 formal intervention family in report-only mode; reject invalid L1 macro actions through Core regardless of solver status. |

**⬜ Step 3**

| Session | Epic(s) | Prereq | Notes |
|---|---|---|---|
| Track D session | A4-E | A4-D ✅ and Wave 3 ✅ | Emit strict ToolAction/WorkOrder packets through brain. |

**⬜ Step 4**

| Session | Epic(s) | Prereq | Notes |
|---|---|---|---|
| Track B session | A4-G | A4-E/F ✅ | Execute limited reroute/work order and generate state diff. |
| Track D session | A4-H cognition side | A4-E ✅ | Ensure model/cost traces complete. |

**⬜ Step 5**

| Session | Epic(s) | Prereq | Notes |
|---|---|---|---|
| Track E session | A4-I, A4-J evidence | A4-G/H ✅ | Run hard gates: schema, authority, hidden leak, and Aster F1 formal-gate differential evidence before Wave 4 closeout. |

**⬜ Step 6**

| Session | Epic(s) | Prereq | Notes |
|---|---|---|---|
| Meta Coordinator session | Wave 4 closeout | A4-I ✅ + A4-J evidence accepted | Decide whether Wave 5 memory/replay can start. |
### Optional parallel side-lanes

Wave 4 is the first real vertical, so parallelism must stay conservative.

- **OSL-W4-A — Track C adjacent Aster actor drafts**
  - prereq: A1 context stable
  - scope: A2/A4/A6/A9 mini role cards only
  - no production prompt changes until Meta accepts

- **OSL-W4-B — Track E Aster eval fixture draft**
  - prereq: action/tool schemas and Aster scenario assumptions
  - output: hidden leak / authority / tool misuse fixture notes
  - can be absorbed into Step 5

- **OSL-W4-C — Track A event timeline fixture prep**
  - prereq: event/state diff examples
  - output: UI assumptions only
  - no Unity truth logic

Avoid extra same-track forks here unless the Meta Coordinator has a clear merge plan. The Aster vertical is architecture-proving work.

### Wave gate

Wave 4 passes when Aster Heat Alarm runs end-to-end with L1 cognition, tool/crew execution, state diff, hard-gate smoke artifacts, and Aster F1 formal intervention gate evidence that agrees with Core authority validation for the scoped fixtures/artifacts.

### Hold conditions
- L1 can issue L2/L3 action
- LLM directly mutates state
- hidden thermal debt in L1 context
- tool/crew execution not traceable
- Aster F1 formal intervention gate silently accepts unsupported candidate facts or disagrees with Core without a blocking diagnostic

---

## Wave 5 — Memory/belief + visibility + eval/replay hard gates

**Wave goal:** Add the cognitive safety layer to Aster: claims, beliefs, rumors, visibility partitions, artifact replay, and hard evals.

**Primary value:** Prevent the first vertical from becoming untrustworthy as more agents are added.

**Primary owner(s):** Track C + Track E + Track B

**Secondary support:** Track D for cognition refs

### Mandatory outputs
- ⬜ ClaimRecord/MemoryUpdate storage
- ⬜ A1 belief update
- ⬜ public rumor minimal
- ⬜ positive memory examples or explicit not-yet-in-scope decision for rumor, belief, official_line, and withheld_item
- ⬜ visibility partition validation
- ⬜ F2 memory/visibility formal-gate expansion or explicit unsupported/no-hard-gate decision if F1 is not stable enough
- ⬜ hidden truth leak eval
- ⬜ rumor/fact contamination eval
- ⬜ artifact replay for Aster
- ⬜ cost/eval summary

### Sprint breakdown

#### Sprint W5-S1 — Memory contracts and A1 belief

**Owner priority:** Track C + Track B

Epics:
- ⬜ **M5-A** ClaimRecord/MemoryUpdate implementation — **Owner: Track B/C**
- ⬜ **M5-B** A1 maintenance-debt belief update — **Owner: Track C**
- ⬜ **M5-C** source refs/confidence validation — **Owner: Track E/B**

#### Sprint W5-S2 — Public rumor and visibility

**Owner priority:** Track C + Track E

Epics:
- ⬜ **M5-D** Utility coverup rumor/narrative state — **Owner: Track C**
- ⬜ **M5-E** VisibilityPartition eval — **Owner: Track E**
- ⬜ **M5-F** hidden truth leak regression case — **Owner: Track E**
- ⬜ **M5-J** F2 memory/visibility formal-gate expansion decision — **Owner: Track C/E/B**

#### Sprint W5-S3 — Artifact replay

**Owner priority:** Track E + Track B

Epics:
- ⬜ **M5-G** artifact replay runner for Aster — **Owner: Track E/B**
- ⬜ **M5-H** eval-only replay — **Owner: Track E**
- ⬜ **M5-I** Wave 5 review package — **Owner: Track E/Meta**

### Execution Steps

**⬜ Step 1**

| Session | Epic(s) | Prereq | Notes |
|---|---|---|---|
| Track B session | M5-A core storage | Wave 4 ✅ | Implement minimal JSON/JSONL memory update path. |

**⬜ Step 2**

| Session | Epic(s) | Prereq | Notes |
|---|---|---|---|
| Track C session | M5-B | M5-A draft accepted | Define belief update semantics for A1. |
| Track E session | M5-C | M5-A draft accepted | Add source/confidence validation scaffolding. |

**⬜ Step 3**

| Session | Epic(s) | Prereq | Notes |
|---|---|---|---|
| Track C session | M5-D | M5-B ✅ | Add public rumor as rumor, not fact. |

**⬜ Step 4**

| Session | Epic(s) | Prereq | Notes |
|---|---|---|---|
| Track E session | M5-E, M5-F, M5-J evidence | M5-D ✅ | Add VisibilityPartition and hidden truth leak checks; decide whether F2 memory/visibility formal-gate checks are hard, report-only, or explicitly unsupported for this wave. |

**⬜ Step 5**

| Session | Epic(s) | Prereq | Notes |
|---|---|---|---|
| Track E session | M5-G, M5-H | Aster artifact bundle exists and M5-E/F ✅ | Replay saved Aster run and run eval-only checks. |

**⬜ Step 6**

| Session | Epic(s) | Prereq | Notes |
|---|---|---|---|
| Meta Coordinator session | M5-I + Wave 5 gate | M5-G/H ✅ | Review whether Aster is safe enough to build L2 on top. |
### Optional parallel side-lanes

- **OSL-W5-A — Track A artifact replay loader prep**
  - prereq: Aster artifact bundle shape exists
  - output: loader/timeline implementation assumptions
  - no dependency on live sim

- **OSL-W5-B — Track D cost summary enhancement draft**
  - prereq: cost events exist
  - output: cost-by-lane/model summary shape
  - must not change model policy

- **OSL-W5-C — Track C relationship/identity example drafts**
  - prereq: ClaimRecord/MemoryUpdate shape accepted
  - output: example RelationshipEdge/IdentityDrift cases
  - not part of canonical behavior until reviewed
  - must also classify the C1-K Track C positive-memory-example concern for rumor, belief, official_line, and withheld_item as in-scope, not-yet-in-scope, or already resolved

Wave 5 carry-forward routes:
- Track C owns the C1-K positive memory example route if it was not closed in Wave 3. Positive examples for rumor, belief, official_line, and withheld_item must be added or explicitly judged not yet in scope before memory semantics are treated as complete.
- Track E owns the C1-K EvalEvent/eval-runner decision. Later eval/replay schema and runtime artifact validation gates must decide whether EvalEvent is required before implementing eval-runner or replay evidence expansion.
- Track C/E/B own the F2 memory/visibility formal-family decision. If it is implemented, it must classify rumor, belief, official_line, withheld_item, fact, hidden visibility, and source-ref relationships with a no-overclaim matrix before canonical memory claims.

Memory/replay work is subtle. Do not run side-lanes that alter claim semantics without Meta and Track E review.

### Wave gate

Wave 5 passes when Aster artifact replay works, memory/visibility hard gates pass, and the F2 memory/visibility formal-family decision is accepted as hard, report-only, or explicitly unsupported/not-yet-in-scope.

### Hold conditions
- rumor stored as fact
- memory update lacks source refs
- hidden truth leak
- artifact replay cannot reproduce downstream effects
- role/memory implementation proceeds without resolving or explicitly deferring the C1-K positive memory example concern
- memory/visibility formal-gate expansion treats natural-language truth or unmodeled claim graph semantics as formally validated

---

## Wave 6 — L2 institution verticals

**Wave goal:** Add embodied institutions, starting with Utility Board and Bio-Life Directorate, then skeletons for the rest of the FP1 L2 set.

**Primary value:** Prove L2 is not a neutral summary layer but an institutional decision/distortion layer.

**Primary owner(s):** Track C + Track B + Track D + Track E

**Secondary support:** Track A may prepare institution inspector against artifacts

### Mandatory outputs
- ⬜ InstitutionProfile/Seat/Dashboard
- ⬜ InstitutionBrief/Order/WithholdingRecord/EscalationRequest
- ⬜ F3 institution-order formal intervention gate for bounded L2 order/withholding/escalation surfaces, hard only after Utility Board fixtures and Core differential evidence exist
- ⬜ Utility Board full vertical
- ⬜ Bio-Life Directorate vertical
- ⬜ Dock/Warden/Security/Ration/House skeletons
- ⬜ InterInstitutionMessage minimal

### Sprint breakdown

#### Sprint W6-S1 — Utility Board full vertical

**Owner priority:** Track C/B/D/E

Epics:
- ⬜ **L6-A** Utility Board profile/seats/dashboard — **Owner: Track B/C**
- ⬜ **L6-B** L2 seat packets + chair synthesis — **Owner: Track C/D**
- ⬜ **L6-C** WithholdingRecord and InstitutionOrder execution — **Owner: Track B/C**
- ⬜ **L6-L** F3 institution-order formal intervention gate — **Owner: Track B/E/C**
- ⬜ **L6-D** Utility L2 eval — **Owner: Track E**

#### Sprint W6-S2 — Bio-Life Directorate vertical

**Owner priority:** Track C/B/D/E

Epics:
- ⬜ **L6-E** Vireo symptom/bio data and actors — **Owner: Track B/C**
- ⬜ **L6-F** Bio-Life seat/chair packets — **Owner: Track C/D**
- ⬜ **L6-G** quarantine/health advisory scoped actions — **Owner: Track B**
- ⬜ **L6-H** rumor/fact eval for symptom cluster — **Owner: Track E**

#### Sprint W6-S3 — Core L2 skeleton set

**Owner priority:** Track B/C

Epics:
- ⬜ **L6-I** Dock/Warden/Security/Ration/House profiles load — **Owner: Track B/C**
- ⬜ **L6-J** InterInstitutionMessage / conflict artifact minimal — **Owner: Track B**
- ⬜ **L6-K** L2 cross-track review — **Owner: Meta + E**

### Execution Steps

**⬜ Step 1**

| Session | Epic(s) | Prereq | Notes |
|---|---|---|---|
| Track B session | L6-A contracts/state side | Wave 5 ✅ | Add institution profile/dashboard state. |
| Track C session | L6-A/L6-B semantics | Wave 5 ✅ | Define Utility Board stance and seat packet prompts. |

**⬜ Step 2**

| Session | Epic(s) | Prereq | Notes |
|---|---|---|---|
| Track D session | Utility L2 runtime integration | Utility prompt/schema handoff accepted | Run L2 seat/chair lane through brain. |
| Track B session | L6-C, L6-L draft | L6-A ✅ | Execute InstitutionOrder/WithholdingRecord artifacts and draft the F3 institution-order formal gate for bounded authority/withholding/escalation checks. |

**⬜ Step 3**

| Session | Epic(s) | Prereq | Notes |
|---|---|---|---|
| Track E session | L6-D, L6-L evidence | L6-B/C ✅ | Check no neutral summary collapse / no omniscient dashboard; compare F3 formal verdicts against Core authority/eval verdicts if F3 is active. |

**⬜ Step 4**

| Session | Epic(s) | Prereq | Notes |
|---|---|---|---|
| Track B/C sessions | L6-E | Utility vertical stable | Build Vireo/Bio-Life data, actors, and symptom-cluster state. |

**⬜ Step 5**

| Session | Epic(s) | Prereq | Notes |
|---|---|---|---|
| Track C/D session | L6-F | L6-E ✅ | Run Bio-Life seat/chair packet path. |
| Track B session | L6-G | L6-E ✅ | Implement scoped quarantine/health advisory actions. |

**⬜ Step 6**

| Session | Epic(s) | Prereq | Notes |
|---|---|---|---|
| Track E session | L6-H | L6-F/G ✅ | Run rumor/fact and quarantine authority evals. |

**⬜ Step 7**

| Session | Epic(s) | Prereq | Notes |
|---|---|---|---|
| Track B/C session | L6-I, L6-J | Utility+Bio stable | Add remaining L2 skeletons and inter-institution message. |

**⬜ Step 8**

| Session | Epic(s) | Prereq | Notes |
|---|---|---|---|
| Meta Coordinator session | L6-K + Wave 6 gate | L6-I/J ✅ | Confirm L2 core set readiness for L3/Black Seam. |
### Optional parallel side-lanes

These are useful but must not hide dependencies.

- **OSL-W6-A — Bio-Life preparation during Utility Board implementation**
  - may start after Utility Board L2 contract/state shape is accepted
  - Track C drafts Bio-Life role/seat stances
  - Track B may add Vireo data stubs
  - runtime integration waits until Utility Board L2 path is stable

- **OSL-W6-B — Track A institution inspector mock**
  - prereq: InstitutionBrief/Order examples exist
  - output: UI view-model assumptions
  - no Unity truth ownership

- **OSL-W6-C — Track E L2 behavior eval draft**
  - prereq: Utility Board seat output examples
  - output: neutral-summary/villain-collapse/dash-omniscience checks

The main numbered steps still control readiness. Bio-Life runtime work should not leap ahead of Utility Board proof.

### Wave gate

Wave 6 passes when Utility Board and Bio-Life verticals work, all 7 L2 institutions exist at least as loadable skeletons, and any active F3 institution-order formal gate has no Core/Refinery drift or unsupported silent accepts.

### Hold conditions
- L2 gets true hidden state
- L2 emits L3 authority
- no withholding trace
- L2 outputs all neutral/generic
- Bio symptom cluster becomes pathogen fact prematurely
- L2 formal gate validates institution/world behavior beyond the bounded order/withholding/escalation family

---

## Wave 7 — L3 Council/Doctrine + Black Seam Protocol Access

**Wave goal:** Implement L3 Council/Doctrine and demonstrate it through Black Seam controlled protocol access.

**Primary value:** Prove macro doctrine, emergency powers, Charter constraints, and Deepworks escalation without micromanagement.

**Primary owner(s):** Track C + Track B + Track D + Track E

**Secondary support:** Track A may prepare Council Inspector

### Mandatory outputs
- ⬜ DoctrineState
- ⬜ EmergencyPowersLedger
- ⬜ CouncilSeatPacket
- ⬜ CouncilChairSynthesis
- ⬜ CharterAuditPacket
- ⬜ CouncilDoctrinePacket/NoActionPacket
- ⬜ F4 council-doctrine formal intervention gate for macro-only doctrine/emergency decisions
- ⬜ Black Seam state/actors
- ⬜ Deepworks Warden vertical
- ⬜ Demo C L3 decision

### Sprint breakdown

#### Sprint W7-S1 — L3 contract/state foundation

**Owner priority:** Track B + Track C

Epics:
- ⬜ **G7-A** DoctrineState / EmergencyPowersLedger — **Owner: Track B**
- ⬜ **G7-B** Council packet schemas and validators — **Owner: Track B**
- ⬜ **G7-C** 7 L3 seat stances/prompts — **Owner: Track C**

#### Sprint W7-S2 — Black Seam/Warden path

**Owner priority:** Track B/C/D

Epics:
- ⬜ **G7-D** Black Seam scenario data and actors — **Owner: Track B/C**
- ⬜ **G7-E** Deepworks Warden Desk full vertical — **Owner: Track C/D/B**
- ⬜ **G7-F** Security/Archive access interplay — **Owner: Track B/C**

#### Sprint W7-S3 — Council decision vertical

**Owner priority:** Track C/D/E/B

Epics:
- ⬜ **G7-G** L3 seat calls + chair synthesis — **Owner: Track C/D**
- ⬜ **G7-H** Charter Auditor and sunset eval — **Owner: Track C/E**
- ⬜ **G7-K** F4 council-doctrine formal intervention gate — **Owner: Track B/E/C**
- ⬜ **G7-I** Controlled excerpt access decision effects — **Owner: Track B**
- ⬜ **G7-J** L3 review package — **Owner: Track E/Meta**

### Execution Steps

**⬜ Step 1**

| Session | Epic(s) | Prereq | Notes |
|---|---|---|---|
| Track B session | G7-A, G7-B | Wave 6 ✅ | Add doctrine/emergency/council contracts and validators. |
| Track C session | G7-C | Council doc available | Draft seat stances; enforce no micromanagement. |

**⬜ Step 2**

| Session | Epic(s) | Prereq | Notes |
|---|---|---|---|
| Track B/C sessions | G7-D | G7-A/B/C draft accepted | Add Black Seam scenario data and actors. |

**⬜ Step 3**

| Session | Epic(s) | Prereq | Notes |
|---|---|---|---|
| Track C/D/B sessions | G7-E, G7-F | G7-D ✅ | Build Warden Desk and Security/Archive access path. |

**⬜ Step 4**

| Session | Epic(s) | Prereq | Notes |
|---|---|---|---|
| Track D session | G7-G runtime | G7-C ✅ and model lanes ready | Run L3 calls via brain. |

**⬜ Step 5**

| Session | Epic(s) | Prereq | Notes |
|---|---|---|---|
| Track E/C session | G7-H, G7-K evidence | G7-G ✅ | Evaluate sunset/Charter/no-micromanagement; run F4 formal-gate evidence if active; refine Charter prompt if needed. |

**⬜ Step 6**

| Session | Epic(s) | Prereq | Notes |
|---|---|---|---|
| Track B session | G7-I, G7-K mapper/core comparison | G7-G/H ✅ | Apply controlled excerpt access effects through core only after F4 formal/core verdicts agree or unsupported surfaces are blocked. |

**⬜ Step 7**

| Session | Epic(s) | Prereq | Notes |
|---|---|---|---|
| Track E + Meta | G7-J + Wave 7 gate | Demo C artifacts | Gate L3 vertical. |
### Optional parallel side-lanes

Wave 7 has good parallelism potential because L3 prompt law, Black Seam data and L3 evals can be drafted independently.

- **OSL-W7-A — Track C generic L3 prompt law**
  - prereq: Council/Doctrine doc and L3 packet schemas
  - output: seat stance/prompt-law draft
  - should stay generic, not overfit to Black Seam

- **OSL-W7-B — Track B/C Black Seam data prep**
  - prereq: Wave 6 L2 skeletons and Deepworks/Warden docs
  - output: Black Seam actor/location/hidden-seed data draft
  - must not force L3 model behavior

- **OSL-W7-C — Track E L3 eval skeleton**
  - prereq: CouncilDoctrinePacket and CharterAuditPacket schemas
  - output: no-micromanagement, sunset condition and Charter flag checks

- **OSL-W7-D — Track A council inspector concept**
  - prereq: L3 output examples
  - output: UI concept only, no implementation dependency

Meta must merge these before the final Black Seam L3 vertical gate.

### Wave gate

Wave 7 passes when Black Seam Protocol Access produces a valid L3 council decision with Charter conditions, no local micromanagement, and F4 council-doctrine formal-gate evidence for bounded macro-only doctrine/emergency checks.

### Hold conditions
- L3 orders local repair/crew action
- emergency measure lacks sunset
- Deepworks hidden truth leaks
- Charter ignored
- Qwen/model-bakeoff reopened without Meta decision
- F4 formal model validates local repair/crew actions or other non-macro surfaces

---

## Wave 8 — Cross-institution scenarios: Morrow, Ration, House

**Wave goal:** Complete the remaining FP1 demo set and demonstrate cross-institution conflict, resource tension, convoy autonomy, public narrative, and light merit-house dynamics.

**Primary value:** Show Ringfall as a system of coupled institutions, not isolated demos.

**Primary owner(s):** All tracks

**Secondary support:** Scenario/Analyst lane optional

### Mandatory outputs
- ⬜ Morrow Chain Manifest Fracture
- ⬜ Ration Office vertical
- ⬜ Dock/Convoy Operations vertical
- ⬜ House Cerulean lightweight vertical
- ⬜ cross-institution conflict artifact
- ⬜ five demo headless run set

### Sprint breakdown

#### Sprint W8-S1 — Morrow/Dock/Ration logistics

**Owner priority:** Track B/C/D/E

Epics:
- ⬜ **X8-A** Dock/Convoy Operations scenario data — **Owner: Track B/C**
- ⬜ **X8-B** Dock manifest discrepancy path — **Owner: Track B/C**
- ⬜ **X8-C** Ration Office supply pressure — **Owner: Track B/C**
- ⬜ **X8-D** InterInstitution conflict artifact — **Owner: Track B/E**

#### Sprint W8-S2 — House Cerulean lightweight vertical

**Owner priority:** Track C/B/E

Epics:
- ⬜ **X8-E** House actor/context data — **Owner: Track C/B**
- ⬜ **X8-F** House Command minimal L2 cycle — **Owner: Track C/D**
- ⬜ **X8-G** patronage/public resentment memory/narrative — **Owner: Track C/E**

#### Sprint W8-S3 — Five-demo run set

**Owner priority:** Meta + E + all

Epics:
- ⬜ **X8-H** Run demos A/B/C/D/E — **Owner: Track E**
- ⬜ **X8-I** scenario summaries/cascade notes — **Owner: Track E/Meta**
- ⬜ **X8-J** cross-demo review — **Owner: Meta**

### Execution Steps

**⬜ Step 1**

| Session | Epic(s) | Prereq | Notes |
|---|---|---|---|
| Track B/C sessions | X8-A, X8-B, X8-C | Wave 7 ✅ or L2 stable | Build Morrow/Ration data and actions. |
| Track E session | X8-D eval shape | schemas ready | Ensure conflict artifact is reviewable. |

**⬜ Step 2**

| Session | Epic(s) | Prereq | Notes |
|---|---|---|---|
| Track C/B sessions | X8-E | Wave 6 L2 skeletons | Add House Cerulean actor/context data only. |

**⬜ Step 3**

| Session | Epic(s) | Prereq | Notes |
|---|---|---|---|
| Track C/D session | X8-F | X8-E ✅ | Add House Command minimal L2 cycle. |

**⬜ Step 4**

| Session | Epic(s) | Prereq | Notes |
|---|---|---|---|
| Track E/C session | X8-G | X8-F ✅ | Check patronage rumor remains systemic, not free roleplay. |

**⬜ Step 5**

| Session | Epic(s) | Prereq | Notes |
|---|---|---|---|
| Track E session | X8-H, X8-I | A/B/C/D/E runnable | Run and summarize demo set. |

**⬜ Step 6**

| Session | Epic(s) | Prereq | Notes |
|---|---|---|---|
| Meta Coordinator session | X8-J + Wave 8 gate | X8-H/I ✅ | Decide if FP1 canonical hardening can begin. |
### Optional parallel side-lanes

Wave 8 is the strongest safe parallelization zone.

- **OSL-W8-A — Morrow/Ration logistics lane**
  - Track B/C/D focus on Dock/Convoy Operations, Ration Office, manifest discrepancy and resource conflict
  - produces Demo D artifacts

- **OSL-W8-B — House Cerulean lightweight lane**
  - Track C/B focus on House actor/context data and House Command minimal L2 cycle
  - produces Demo E artifacts
  - must keep House secondary and not expand academy systems

- **OSL-W8-C — Track E cross-institution eval lane**
  - builds conflict artifact checks, resource conservation checks and public narrative checks
  - can run while Demo D/E implementation proceeds if schemas are stable

This is the first wave where same-wave multi-lane execution is strongly recommended if session capacity exists. Meta must still merge outputs before the five-demo run set.

### Wave gate

Wave 8 passes when all five FP1 demos run headlessly with artifact bundles and no critical hard-gate failures.

### Hold conditions
- resource conservation broken
- cross-institution conflict not artifacted
- House dominates scope
- public narrative missing
- demos feel disconnected from same world

---

## Wave 9 — FP1 canonical hardening

**Wave goal:** Convert working demos into a credible FP1 headless canonical build with strict run modes, replay, eval, cost, and manual review.

**Primary value:** Separate believable prototype from canonical evidence.

**Primary owner(s):** Track E + Meta

**Secondary support:** all tracks fix findings

### Mandatory outputs
- ⬜ canonical/dev cheap mode separation
- ⬜ hard gate eval suite
- ⬜ EvalEvent/eval-runner expansion decision before runtime eval evidence broadens beyond EvalSummary
- ⬜ artifact replay for Aster/Vireo/Black Seam
- ⬜ cost summary by lane/model
- ⬜ manual findings-first review package
- ⬜ FP1 headless gate report

### Sprint breakdown

#### Sprint W9-S1 — Run mode and eval hardening

**Owner priority:** Track E/D/B

Epics:
- ⬜ **H9-A** manifest run_mode enforcement — **Owner: Track E/B**
- ⬜ **H9-B** hard gate evals complete — **Owner: Track E**
- ⬜ **H9-C** model/cost summary by lane/model — **Owner: Track D/E**

#### Sprint W9-S2 — Replay and regression

**Owner priority:** Track E/B

Epics:
- ⬜ **H9-D** artifact replay for Aster/Vireo/Black Seam — **Owner: Track E/B**
- ⬜ **H9-E** deterministic core replay for Aster — **Owner: Track E/B**
- ⬜ **H9-F** scenario regression notes — **Owner: Track E**

#### Sprint W9-S3 — Manual gate

**Owner priority:** Meta + all

Epics:
- ⬜ **H9-G** findings-first review package — **Owner: Track E/Meta**
- ⬜ **H9-H** fix-forward plan for findings — **Owner: all**
- ⬜ **H9-I** FP1 headless gate decision — **Owner: Meta**

### Execution Steps

**⬜ Step 1**

| Session | Epic(s) | Prereq | Notes |
|---|---|---|---|
| Track E/D/B sessions | H9-A, H9-B, H9-C | Wave 8 ✅ | Lock run mode, eval, cost evidence. This is a coordinated multi-track hardening step. |

**⬜ Step 2**

| Session | Epic(s) | Prereq | Notes |
|---|---|---|---|
| Track E/B sessions | H9-D, H9-E, H9-F | H9-A/B ✅ | Replay and regression evidence. |

**⬜ Step 3**

| Session | Epic(s) | Prereq | Notes |
|---|---|---|---|
| Track E + Meta | H9-G | H9-D/E ✅ | Prepare findings-first review. |

**⬜ Step 4**

| Session | Epic(s) | Prereq | Notes |
|---|---|---|---|
| All relevant tracks | H9-H | review findings | Fix blockers only; Meta assigns exact track based on finding. |

**⬜ Step 5**

| Session | Epic(s) | Prereq | Notes |
|---|---|---|---|
| Meta Coordinator session | H9-I + Wave 9 gate | blocking findings resolved or accepted | Gate FP1 headless canonical. |
### Optional parallel side-lanes

Wave 9 can benefit from controlled Track E forks because hardening work is naturally separable.

- **OSL-W9-A — Track E replay/regression fork**
  - artifact replay, deterministic core replay, regression comparison

- **OSL-W9-B — Track E cost/model-policy fork**
  - cost summaries, DeepSeek escalation counts, free quota usage, retry/fallback reports

- **OSL-W9-C — Track E hidden leak / authority fork**
  - E1/E2/E3 checks, hidden truth leak review, authority violation review

- **OSL-W9-D — Meta review synthesis**
  - not a forked implementation lane
  - absorbs all Track E fork outputs into one findings-first gate

Use same-track forks here only if there are enough artifacts to justify them. Every fork must return a clean findings summary and not independently declare the gate.

### Wave gate

Wave 9 passes when core FP1 demos are canonical-evaluable, replayable, cost-accounted, and manually reviewed with pass/pass_with_warnings.

### Hold conditions
- hidden leak
- authority violation
- unreplayable canonical run
- cost unbounded
- dev cheap mislabeled as canonical
- eval/replay hardening expands runtime evidence while leaving the C1-K EvalEvent/eval-runner decision unclassified

---

## Wave 10 — Unity observer FP1

**Wave goal:** Build Unity artifact observer capable of loading and inspecting saved FP1 run bundles without owning truth.

**Primary value:** Make Ringfall visible, legible, and portfolio-showable without compromising the headless artifact truth.

**Primary owner(s):** Track A

**Secondary support:** Track B/E provide artifacts; Track C/D provide view fields

### Mandatory outputs
- ⬜ Unity project skeleton
- ⬜ artifact loader
- ⬜ run manifest view
- ⬜ event timeline/feed
- ⬜ sector/ring placeholder map
- ⬜ actor/institution/council inspectors
- ⬜ eval/cost panel
- ⬜ truth/perception toggle basic

### Sprint breakdown

#### Sprint W10-S1 — Unity shell and artifact loader

**Owner priority:** Track A

Epics:
- ⬜ **U10-A** Unity project skeleton — **Owner: Track A**
- ⬜ **U10-B** RunManifest/EventLog loader — **Owner: Track A**
- ⬜ **U10-C** basic app/top bar state — **Owner: Track A**

#### Sprint W10-S2 — Timeline, map, inspectors

**Owner priority:** Track A + B/E

Epics:
- ⬜ **U10-D** timeline/event feed — **Owner: Track A**
- ⬜ **U10-E** Aster/Vireo/Morrow/Black Seam/House map nodes — **Owner: Track A**
- ⬜ **U10-F** actor/institution/council inspectors — **Owner: Track A**

#### Sprint W10-S3 — Eval/cost and view modes

**Owner priority:** Track A + E

Epics:
- ⬜ **U10-G** eval/cost panel — **Owner: Track A/E**
- ⬜ **U10-H** Truth vs Actor/Public/Institution mode labels — **Owner: Track A**
- ⬜ **U10-I** observer review — **Owner: Meta/E/A**

### Execution Steps

**⬜ Step 1**

| Session | Epic(s) | Prereq | Notes |
|---|---|---|---|
| Track A session | U10-A, U10-B, U10-C | stable manifest/event artifacts | Create artifact-reader shell; no live sim dependency. |

**⬜ Step 2**

| Session | Epic(s) | Prereq | Notes |
|---|---|---|---|
| Track A session | U10-D, U10-E | U10-B ✅ | Add timeline and map placeholders. |

**⬜ Step 3**

| Session | Epic(s) | Prereq | Notes |
|---|---|---|---|
| Track A session | U10-F | actor/institution/council summaries available | Add inspectors with raw JSON dev tab optional. |

**⬜ Step 4**

| Session | Epic(s) | Prereq | Notes |
|---|---|---|---|
| Track A/E sessions | U10-G, U10-H | eval/cost summaries available and U10-F ✅ | Surface gate/cost and truth/perception boundary. |

**⬜ Step 5**

| Session | Epic(s) | Prereq | Notes |
|---|---|---|---|
| Meta + Track E | U10-I + Wave 10 gate | saved FP1 run loads | Review observer for legibility and truth safety. |
### Optional parallel side-lanes

Unity observer can be parallelized after artifact contracts are stable.

- **OSL-W10-A — Track A artifact loader + timeline**
  - owns manifest/event loading and replay clock

- **OSL-W10-B — Track A inspectors**
  - owns actor/institution/council/event inspector panels
  - depends on summary/artifact shapes, not live sim

- **OSL-W10-C — Track A overlays/map**
  - owns sector nodes, marker system and basic overlays

- **OSL-W10-D — Track E UI truth-safety review**
  - checks hidden/visible distinction and eval/cost display

This is a good place for same-track Track A forks, but only after Track A defines scene/prefab/viewmodel ownership to avoid Unity merge conflicts.

### Wave gate

Wave 10 passes when Unity loads a saved FP1 run, shows timeline/inspectors/eval/cost, and clearly separates truth/perception views.

### Hold conditions
- Unity invents truth
- hidden truth unlabeled
- no timeline/inspector
- pretty UI but no trace breadcrumbs

---

## Wave 11 — Remote-run readiness and demo packaging

**Wave goal:** Prepare simple remote run workflow, artifact packaging, sanitized demo export, and curated FP1 run bundle.

**Primary value:** Make Ringfall practical to run beyond a local dev shell and safe to show in curated form.

**Primary owner(s):** Track D + Track E + Meta

**Secondary support:** Track A for demo load

### Mandatory outputs
- ⬜ remote README/scripts
- ⬜ artifact packaging script
- ⬜ public/demo export script
- ⬜ curated FP1 run bundle
- ⬜ cost/eval report
- ⬜ Unity demo load path
- ⬜ sanitization checks

### Sprint breakdown

#### Sprint W11-S1 — Remote run basics

**Owner priority:** Track D

Epics:
- ⬜ **P11-A** remote README and env instructions — **Owner: Track D**
- ⬜ **P11-B** headless run script — **Owner: Track D/B**
- ⬜ **P11-C** artifact path/config snapshot guidance — **Owner: Track D/E**

#### Sprint W11-S2 — Packaging and sanitization

**Owner priority:** Track E + Meta

Epics:
- ⬜ **P11-D** artifact bundle validator/exporter — **Owner: Track E**
- ⬜ **P11-E** public/private sanitization checklist — **Owner: Meta/E/D**
- ⬜ **P11-F** curated run selection criteria — **Owner: Meta/E**

#### Sprint W11-S3 — Demo load and gate

**Owner priority:** Track A/E/Meta

Epics:
- ⬜ **P11-G** Unity loads exported bundle — **Owner: Track A**
- ⬜ **P11-H** final demo cost/eval report — **Owner: Track E**
- ⬜ **P11-I** Wave 11 gate — **Owner: Meta**

### Execution Steps

**⬜ Step 1**

| Session | Epic(s) | Prereq | Notes |
|---|---|---|---|
| Track D session | P11-A, P11-B, P11-C | FP1 headless stable | Keep remote simple: single VM/scripts/env; no Kubernetes. |

**⬜ Step 2**

| Session | Epic(s) | Prereq | Notes |
|---|---|---|---|
| Track E session | P11-D | valid artifact bundles | Package/export tooling. |

**⬜ Step 3**

| Session | Epic(s) | Prereq | Notes |
|---|---|---|---|
| Meta/E/D | P11-E, P11-F | P11-D ✅ | Sanitize and choose curated evidence-backed run. |

**⬜ Step 4**

| Session | Epic(s) | Prereq | Notes |
|---|---|---|---|
| Track A session | P11-G | exported bundle | Unity can load demo. |
| Track E session | P11-H | curated run selected | Cost/eval report. |

**⬜ Step 5**

| Session | Epic(s) | Prereq | Notes |
|---|---|---|---|
| Meta Coordinator | P11-I + Wave 11 gate | P11-G/H ✅ | Declare remote/demo readiness. |
### Optional parallel side-lanes

- **OSL-W11-A — Track D remote script lane**
  - simple VM scripts, env config, no Kubernetes

- **OSL-W11-B — Track E export/evidence lane**
  - artifact packaging, eval/cost report, replay verification

- **OSL-W11-C — Track A demo-load lane**
  - Unity loads exported bundle and verifies public/demo viewer path

- **OSL-W11-D — Meta public/private claim review**
  - checks sanitized export and portfolio claims

Parallelism is useful here because remote, export and observer packaging are separate surfaces, but the final demo readiness gate must be one Meta synthesis.

### Wave gate

Wave 11 passes when a curated sanitized FP1 run can be packaged, validated, loaded by Unity, and explained with cost/eval evidence.

### Hold conditions
- secrets/private prompts leaked
- artifact bundle invalid
- remote run docs missing
- Unity cannot load export
- public claims exceed artifact evidence


---

# 10. Wave and sprint matrix

| Wave | Sprint count | Primary track(s) | First unblock | Closeout evidence |
|---|---:|---|---|---|
| Wave 0 | 3 | Meta/B | repo skeleton | docs + README + configs |
| Wave 1 | 4 | B/E | schemas | schema smoke + examples |
| Wave 1.5 | 2 | Meta/E | schema checker | contract CI + CI policy slots |
| Wave 2 | 3 | B | C# solution | deterministic no-LLM run |
| Wave 3 | 3 | D/C | model policy/mock | cognition trace |
| Wave 4 | 3 | B/C/D/E | Aster context | L1 action/tool/crew vertical + F1 formal gate |
| Wave 5 | 3 | C/E/B | memory contracts | artifact replay + hard gates + F2 decision |
| Wave 6 | 3 | C/B/D/E | Utility Board | L2 Utility/Bio vertical + F3 evidence if active |
| Wave 7 | 3 | C/B/D/E | doctrine contracts | Black Seam L3 decision + F4 evidence |
| Wave 8 | 3 | all | Morrow/Ration/House data | five-demo headless set |
| Wave 9 | 3 | E/Meta | eval hardening | FP1 canonical gate |
| Wave 10 | 3 | A | artifact loader | Unity observer loads FP1 |
| Wave 11 | 3 | D/E/Meta | remote scripts | curated demo package |

## Controlled parallelism summary

The main numbered steps remain the canonical next-action flow. Optional side-lanes are capacity accelerators.

Best side-lane zones:

| Zone | Recommended parallelism | Why it is safe |
|---|---|---|
| Wave 2/3 | Track B core + Track D brain skeleton + Track E eval skeleton + Track A loader planning + Track B/E formal-gate spike planning | separate folders, contracts already gated, formal spike is design-only |
| Wave 6 | Bio-Life prep while Utility Board runtime stabilizes | prep only; runtime waits for Utility proof |
| Wave 7 | L3 prompt law + Black Seam data + L3 eval skeleton | independent surfaces, merged before L3 vertical |
| Wave 8 | Morrow/Ration and House lanes | strongest safe feature parallelism |
| Wave 9 | Track E replay/cost/hidden-leak forks | review/eval work naturally separable |
| Wave 10 | Track A loader/inspectors/overlays forks | safe after artifact contracts stable |
| Wave 11 | remote/export/demo-load lanes | separate packaging surfaces |

Weak side-lane zones:

| Zone | Why not aggressively parallelize |
|---|---|
| Wave 0 | small and source-of-truth sensitive |
| Wave 1 | shared contract authoring is collision-prone |
| Wave 4 | first architecture-proving L1 vertical |
| Wave 5 | memory/visibility/replay semantics are subtle |


---

# 11. Hero vertical unlock order

## Hero 1 — Aster Heat Alarm

Unlocks:
- L1 actor context
- tool/action/work order path
- hidden thermal debt
- action trace/state diff
- first memory and replay path

Must be real before L2 expansion becomes credible.

## Hero 2 — Vireo Symptom Cluster

Unlocks:
- symptom vs pathogen distinction
- public rumor / official line tension
- Bio-Life Directorate
- quarantine authority
- truth policy pressure

Must be real before claiming public-health and rumor systems work.

## Hero 3 — Black Seam Protocol Access

Unlocks:
- Deepworks secrecy
- Warden withholding
- Security suspicion
- L3 Council/Doctrine
- Charter Auditor

Must be real before claiming L3 governance works.

## Hero 4 — Morrow Manifest Fracture

Unlocks:
- convoy logistics
- manifest discrepancy
- ration/utility/bio cross-institution conflict
- mobile-vs-static legitimacy tension

## Hero 5 — House Cerulean Patronage Pressure

Unlocks:
- lightweight merit-house layer
- patronage and public resentment
- academy/prestige dynamics without making Ringfall a school sim

---

# 12. Decision policy for orchestration patterns

## Early default

Use simple explicit orchestration:

```text
Meta Coordinator plans
Track sessions implement
Track E reviews
Meta gates
```

Do not introduce autonomous session-to-session automation yet.

## Practical rule

A workflow optimization is allowed only if it:

- preserves artifacts,
- preserves review/gate visibility,
- does not bypass Meta source-of-truth,
- does not require FAL runtime for Ringfall V1.

## Optional side-lane policy

Optional side-lanes are allowed when they improve throughput without making the numbered step ambiguous.

Meta must track:

- side-lane id,
- owning track/session,
- exact scope,
- prerequisite state,
- expected output,
- merge-back rule,
- whether it can be discarded without blocking the main path.

A side-lane must never be treated as silently accepted. It returns to Meta through a `FORK MERGE`-style brief.

## Future FAL insertion

FAL can later review Ringfall artifacts or orchestrate track handoffs.  
Ringfall must first produce stable artifacts independently.

---

# 13. Immediate next execution order

## Current frontier

The project is post-Wave-1 and pre-Wave-2 runtime implementation. Wave 0 repo/docs bootstrap is closed with a 2026-06-14 **PASS** gate, and Wave 1 contract/artifact spine is accepted through W1-S7/C1-K. The accepted Wave 1 surface now includes schema drafts, valid/invalid examples, `tools/schema_check.py`, and cross-track contract handoff review. No C#/.NET solution, Python brain service, Unity project, model provider implementation, scenarios, or simulation logic has started. Wave 1.5 contract CI readiness is active: CI15-A, CI15-B, CI15-C, CI15-D, CI15-E, and CI15-F are accepted. The Wave 1.5 closeout gate is the next immediate sequence frontier.

The target-side MetaOps source-of-truth sync lane is complete. `RF-STATUS-SYNC-01` aligned post-Wave-0 status/frontier docs, and `RF-GUARDRAIL-SYNC-01` aligned the Design Canon guardrail summary with the Risk Register G1-G10 list. The separate Wave 1 planning brief is present at `docs/plans/Ringfall-Wave1-Planning-Brief-v01.md`; W1-S1 through W1-S7 are accepted, and `docs/plans/W1-S7-C1-K-Contract-Handoff-Review-Packet.md` is the shared Wave 1 handoff/gate artifact for the transition into Wave 1.5 and later Wave 2 planning.

## Immediate sequence

1. `CI15-A` is accepted in `docs/plans/Wave-1.5-CI15-A-CI-Readiness-Contract.md`: RingFall's official contract-CI scope and local verify contract are defined.
2. `CI15-B` is accepted: `.github/workflows/contract-ci.yml` runs the contract schema checker on push/PR.
3. `CI15-C` is accepted: the contract workflow hygiene guard and durable proof script protect the accepted contract CI workflow.
4. `CI15-D` is accepted: future runtime CI lane names are recorded as blocked slots until their runtime surfaces exist.
5. `CI15-E` is accepted: coverage is report-only/later only and not a hard threshold before stable runtime modules and representative test corpus evidence exist.
6. `CI15-F` is accepted: the future `formal-intervention-ci` lane is recorded but blocked until a named Refinery family has fixtures, bridge/core differential checks, and explicit unsupported-surface handling.
7. The Wave 1.5 closeout gate is next: decide whether Wave 2 planning may proceed with contract CI in place or with explicit CI-debt rationale.
8. Do not start C#/.NET, Python brain, Unity, provider/model runtime, scenarios, or simulation logic from Wave 1 acceptance alone; Wave 2 waits for Wave 1.5 acceptance or an explicit Meta CI-debt exception.
9. Treat `docs/design/Formal-Intervention-Gates-Refinery.md` as the approved formal-gate design direction, but do not implement Refinery tooling until a later named family gate opens.

## First actionable step

```text
Wave 1.5 closeout gate — decide whether Wave 2 planning may proceed.
```

Expected Wave 1.5 gate brief:

```text
Decide whether Wave 2 planning may proceed with CI15-A through CI15-F accepted, or record an explicit CI-debt exception if any Wave 1.5 output is insufficient.
Do not add C#/.NET runtime, Python brain runtime, provider calls, Unity work, scenarios, runtime cost collection, eval runner logic, Refinery/solver tooling, simulation logic, or coverage hard gates.
Preserve the accepted Wave 1 contract semantics and treat green CI as mechanical evidence only, not domain approval.
```

W1-S1 closeout note, 2026-06-14:
- `src/ringfall-contracts/README.md`, `src/ringfall-contracts/docs/Contract-Versioning.md`, `src/ringfall-contracts/schemas/README.md`, and six schema-group `.gitkeep` files form the accepted C1-A/C1-B baseline.
- Step review verified no `*.schema.json`, schema bodies, examples, validation tools, runtime code, configs, scenarios, Unity files, provider/model behavior, or simulation logic.
- W1-S2/C1-C,C1-D was the next gated planning target after W1-S1 and is now accepted.

W1-S2 closeout note, 2026-06-14:
- `src/ringfall-contracts/schemas/packets/` now contains exactly five W1-S2 schema drafts: `avatar-pulse-packet.schema.json`, `scene-action-packet.schema.json`, `work-order-request.schema.json`, `tool-action-request.schema.json`, and `execution-result.schema.json`.
- Review fixes tightened `SceneActionPacket.actions[]`, `WorkOrderRequest` targeting, `ExecutionResult.hidden_effects`, and observable result objects before commit.
- No examples, validation tooling, runtime code, configs, scenarios, Unity files, provider/model behavior, C1-E schemas, trace schemas, eval schemas, or simulation logic were added.
- W1-S3/C1-E was the next gated planning target after W1-S2 and is now accepted.

W1-S3 closeout note, 2026-06-14:
- `docs/plans/W1-S3-C1-E-Track-C-Packet-Usability-Review.md` records Track C packet-usability findings and Track B/Meta handoff guidance.
- `src/ringfall-contracts/schemas/packets/` now contains exactly eight packet schema drafts, including C1-E `institution-brief.schema.json`, `institution-order.schema.json`, and `council-doctrine-packet.schema.json`.
- Review fixes enforced withholding traceability when `withheld_items_count >= 1` and routed remaining semantic validation debt to future C1-I/C1-J invalid fixtures.
- No examples, validation tooling, runtime code, configs, scenarios, Unity files, provider/model behavior, trace schemas, memory schemas, eval schemas, cost schemas, or simulation logic were added.
- W1-S4/C1-F,C1-G was the next gated planning target after W1-S3 and is now accepted.

W1-S4 closeout note, 2026-06-15:
- `src/ringfall-contracts/schemas/` now contains exactly fourteen schema drafts: eight packet schemas, three trace schemas, one state schema, and two memory schemas.
- Track B added `run-manifest.schema.json`, `cognition-trace.schema.json`, `action-trace.schema.json`, `state-diff.schema.json`, `claim-record.schema.json`, and `memory-update.schema.json`.
- Review fixes aligned `ClaimRecord.claim_type` with the canonical memory taxonomy and routed remaining semantic validation debt to future C1-I/C1-J fixture/tooling work.
- No examples, validation tooling, runtime code, configs, scenarios, Unity files, provider/model behavior, cost schemas, eval schemas, or simulation logic were added.
- W1-S5/C1-H was the next gated planning target after W1-S4 and is now accepted.

W1-S5 closeout note, 2026-06-15:
- `src/ringfall-contracts/schemas/` now contains exactly sixteen schema drafts after adding `traces/cost-event.schema.json` and `eval/eval-summary.schema.json`.
- Track D added CostEvent cost/model/provider evidence fields without runtime provider behavior; Track E added EvalSummary replay/eval summary fields without EvalEvent or eval-runner behavior.
- Review fixes reconciled the shared schema README so both split-lane C1-H schemas are documented together.
- Remaining semantic validation debt for CostEvent, EvalSummary, and source-ref vocabulary checks is routed to future C1-I/C1-J fixture/tooling work.
- No examples, validation tooling, runtime code, configs, scenarios, Unity files, provider/model behavior, eval runner code, runtime cost collection, or simulation logic were added.
- W1-S6/C1-I,C1-J was the next gated planning target after W1-S5 and is now accepted.

Wave 1 planning gate note, 2026-06-14:
- `docs/plans/Ringfall-Wave1-Planning-Brief-v01.md` is the Meta gate for Wave 1 entry.
- Track B W1-S1/C1-A/C1-B is implemented and accepted.
- Track B W1-S2/C1-C,C1-D is implemented and accepted.
- W1-S3/C1-E packet usability review and institution/council schemas are implemented and accepted.
- W1-S4/C1-F,C1-G trace and memory/state schemas are implemented and accepted.
- W1-S5/C1-H cost-event and eval-summary surface review is implemented and accepted.
- W1-S6/C1-I,C1-J valid/invalid examples and schema validation tooling is implemented and accepted; W1-S7/C1-K cross-track contract handoff review is the next gated step.
- Model-policy note before Wave 1 start: Ringfall stays OpenRouter-only; prefer free model lanes where stable, but require `deepseek/deepseek-v4-flash` as the explicit low-cost paid fallback instead of assuming free quota availability.

MetaOps sync closure note, 2026-06-14:
- `RF-STATUS-SYNC-01` closed the stale post-Wave-0 frontier drift across the Combined Plan, Meta Handoff, and local FAL active context.
- `RF-GUARDRAIL-SYNC-01` aligned the Design Canon guardrail summary, Risk Register detailed G1-G10 sections, and automatic hold trigger wording with `docs/ops/Ringfall-Risk-Register-and-Design-Guardrails-v01.md`.
- The guardrail sync touched ignored/private canon policy files only; this Combined Plan note is the tracked handoff record.
- Wave 2 runtime implementation remains blocked until Wave 1.5 contract CI readiness is accepted or Meta explicitly records a CI-debt exception.

---

# 14. Blocked-state fallback guidance

If a track is blocked, use these fallback paths.

## Track A fallback work

Allowed:
- read Unity UX doc,
- sketch artifact loader view model assumptions,
- draft UI panel notes,
- no implementation depending on unstable schemas.

Forbidden:
- inventing truth state,
- live sim UI before artifacts,
- full 3D gameplay work.

## Track B fallback work

Allowed:
- schema examples,
- contract tests,
- deterministic seed utilities,
- state model notes.

Forbidden:
- provider calls,
- Unity truth logic,
- LLM prompt semantics.

## Track C fallback work

Allowed:
- role cards drafts,
- prompt shape notes,
- memory/belief examples,
- L2/L3 stance refinement.

Forbidden:
- production prompt output that does not match schemas,
- hidden truth in actor context,
- expanding actor roster beyond FP1.

## Track D fallback work

Allowed:
- model policy config draft,
- OpenRouter adapter skeleton behind mock,
- cost event shape review.

Forbidden:
- real API dependency in tests,
- silent model changes,
- making brain mutate state.

## Track E fallback work

Allowed:
- eval rubric drafts,
- artifact validation scripts against examples,
- hidden leak test plans,
- review package templates.

Forbidden:
- relying only on LLM judge,
- treating dev cheap as canonical.

---

# 15. What “good progress” means after 2–3 weeks

## Best-case near-term outcome

After an effective early implementation period, Ringfall should have:

- repo skeleton and docs imported,
- schemas and examples validating,
- C# headless core building,
- first deterministic Aster seed run,
- Python brain mock returning a valid packet,
- artifact bundle smoke path,
- early eval hard gates in place.

## Bad false-positive outcome

A bad early outcome looks like:

- nice Unity mockup but no artifacts,
- model calls producing impressive text but no validation,
- vague actor roleplay but no world effects,
- schema drift between tracks,
- no replay/eval evidence,
- FP1 scope expansion.

---

# 16. Meta Coordinator responsibilities for this plan

Meta must:

- maintain source-of-truth order,
- prevent FP1 scope creep,
- sequence track handoffs,
- require artifacts for claims,
- enforce findings-first review,
- enforce formal-gate no-overclaim language and unsupported-surface diagnostics,
- update decision log when locked decisions change,
- stop hidden truth leaks,
- stop direct LLM state mutation,
- keep dev cheap/canonical distinction clear.

Meta should not:

- implement major feature code by default,
- let tracks silently adapt broken contracts,
- reopen model choices casually,
- couple Ringfall to FAL runtime too early.
- treat `Refinery valid` as full-world correctness or as permission to bypass Core validation.

---

# 17. Open design questions intentionally left unresolved

These are not blockers for Wave 0.

- exact public license
- UI Toolkit vs uGUI
- exact first dev active actor count
- exact demo sim-day length
- DTO generation vs hand-written records
- final remote provider
- public export policy
- L3 heavy debate mode
- Charter Auditor soft veto mode
- future model bakeoff timing
- exact Refinery integration route: local Docker CLI, Java library, or a later pinned tool wrapper
- first formal-gate CI activation timing after F1 fixtures and differential harness exist

Do not block Wave 0 or Wave 1 on these.

---

# 18. Initial coordination notes for sequencing kickoff

These notes are for the first Meta Coordinator `WAVE START`.

1. The planning core is complete enough for implementation handoff.
2. The next correct move is Wave 0.
3. Do not start with models, Unity polish, or scenario expansion.
4. The first contract danger is schema drift.
5. The first architecture danger is core/brain boundary erosion.
6. The first product danger is scope creep.
7. The first eval danger is missing artifacts.
8. Track B is the earliest critical path.
9. Track E should start earlier than feels necessary.
10. Track A should wait for artifact shapes before serious UI implementation.

---

# 19. Final summary

Ringfall should now move from planning canon to implementation.

The first implementation objective is not to make the world impressive.  
It is to make the world **structured enough that impressive behavior can become trustworthy**.

The near-term path is:

```text
Wave 0 → Wave 1 → Wave 2 → Wave 3 → Wave 4
repo/docs → contracts → core → brain → Aster vertical + first formal intervention gate
```

If the Aster vertical becomes real, replayable, and formally bounded at the intervention surface, the rest of Ringfall has a foundation.
If the project skips to UI, models, or broad world scope before that, it risks becoming LLM theater.

Implementation motto:

> Build the smallest real Ringfall vertical, not the largest fake Ringfall surface.
