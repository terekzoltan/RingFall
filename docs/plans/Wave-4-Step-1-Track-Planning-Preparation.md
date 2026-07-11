# Wave 4 Step 1 Track Planning Preparation

**Status:** planning aid only; not an implementation authorization.

## Purpose

This document helps Track B and Track C prepare their own Wave 4 Step 1 plans for `A4-A`, `A4-B`, and `A4-C`. It does not choose their implementation design, reserve files, approve a schema change, or mark any epic complete.

Track B and Track C must each produce their own scoped plan from the accepted Wave 3 frontier. Meta Coordinator reviews those plans before either track starts implementation.

## Accepted Starting Point

The Wave 2 Core path can produce deterministic Aster T0 heat-alarm artifacts. The Wave 3 brain path can emit only a deterministic mock L1 packet, cognition trace, and cost event; it has no Core mutation path and does not authorize provider calls, prompt runtime work, or production cognition.

Wave 4 Step 1 is limited to the Aster actor and tool-context foundation:

| Epic | Accountable track | Planning objective |
|---|---|---|
| `A4-A` | Track C/B | Define Aster actor records and actor-local observations while preserving hidden-truth boundaries. |
| `A4-B` | Track B | Define `crew_aster_repair_02` and the minimum crew state needed by this vertical. |
| `A4-C` | Track B | Define the minimum `local_grid_panel` and `maintenance_console` tool surfaces. |

Existing packet contracts include `WorkOrderRequest`, `ToolActionRequest`, and `ExecutionResult`; their presence is not approval to change their schemas or to implement execution in Step 1.

## Non-Negotiable Boundaries

- Core remains the sole authority for world-state mutation and validation.
- Actor-local observations must not expose hidden truth, other actors' private state, or privileged system facts.
- Step 1 does not add real provider/API calls, credentials, `.env` loading, prompt runtime loading, or model-policy execution.
- Step 1 does not implement ToolAction or WorkOrder execution, L1 prompt behavior, state-diff effects, or Wave 4 end-to-end artifacts; those belong to later accepted epics.
- No public contract/schema change begins unless the track plan names it, explains the compatibility impact, and Meta explicitly accepts it.
- Generated run artifacts, Unity/client work, Refinery/solver runtime, and CI expansion remain out of scope.

## Track C Planning Deliverable: A4-A

Track C should prepare a concise plan that answers:

1. Which Aster actor records and local-observation concepts are needed for the first heat-alarm vertical.
2. Which facts are visible to A1/Aster, which remain hidden, and how the plan proves the separation.
3. Whether Track B must provide or approve a Core-owned data representation before Track C can proceed.
4. The exact files and artifacts Track C proposes to change, including any review-only artifact.
5. Acceptance checks that establish local-observation limits without adding prompt runtime or brain-to-Core mutation.
6. Dependencies on `A4-B` or `A4-C`; do not assume one unless the proposed interface requires it.

Track C may recommend an implementation order, but that order is a proposal until Meta accepts the plan.

## Track B Planning Deliverable: A4-B and A4-C

Track B should prepare one scoped plan that explicitly chooses one of these execution forms:

| Form | Permitted use | Required clarity |
|---|---|---|
| Bundled | One Track B session completes `A4-B` and `A4-C` as one coherent sequential assignment. | State the internal order, shared acceptance boundary, affected files, and why neither epic needs an independent gate. |
| Split | `A4-B` and `A4-C` have separate rows or steps. | State the prerequisite and acceptance result that makes the later epic startable. |

The split form must be offered, but Track B may choose the bundled form when it remains coherent. A bundled row is not same-session parallelism.

The Track B plan must additionally answer:

1. The smallest Core-owned crew state for `crew_aster_repair_02`, including identity, authority, availability, and persistence implications.
2. The smallest tool-surface representation for `local_grid_panel` and `maintenance_console`, including what is intentionally unavailable in Step 1.
3. Whether existing contracts are sufficient; if not, identify the exact proposed schema/API change and hold it for Meta approval.
4. How the plan keeps later ToolAction/WorkOrder validation and execution out of Step 1.
5. The proposed tests or deterministic checks for state loading, identifier validation, and forbidden-scope absence.
6. The exact file scope and any cross-track handoff needed from or to `A4-A`.

## Meta Review Gate

Meta Coordinator reviews the Track B and Track C plans together before implementation. The review accepts a plan only when it:

- names exact epic ownership, file scope, prerequisites, and acceptance evidence;
- makes every A4-A visibility boundary explicit and preserves Core authority;
- distinguishes data/context preparation from later prompt, packet-emission, validation, and execution work;
- states whether Track B is bundled or split and, if bundled, gives the internal order;
- routes any schema, public API, compatibility, or shared-contract change to explicit approval;
- defines checks proportionate to the proposed change; and
- does not claim that Step 1 has implemented or accepted any Wave 4 epic.

Meta may approve both plans, request revision, or accept one while holding the other. Only an explicit Meta approval opens the corresponding implementation assignment.

## Suggested Handoff Shape

Each track plan should end with:

```text
Epic(s):
Execution form: bundled or split (Track B only)
Scope: exact files and artifacts
Prerequisites:
Hidden-truth/Core-authority constraints:
Out of scope:
Acceptance evidence:
Open questions or Meta decisions required:
```

## Source Order

1. Current committed repository state and accepted Wave 2/Wave 3 evidence.
2. `docs/plans/Combined-Execution-Sequencing-Plan.md`.
3. `docs/plans/Ringfall-Implementation-Wave-Plan-v01.md`.
4. `docs/design/Ringfall-Action-and-Tool-Contract-v01.md`.
5. `docs/design/Ringfall-State-and-Observation-Model-v01.md`.
6. `docs/design/Ringfall-Agent-Memory-and-Belief-Model-v01.md`.

If a source is absent, stale, or conflicts with an accepted artifact, record that fact for Meta review rather than silently inventing the missing rule.
