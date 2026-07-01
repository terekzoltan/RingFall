# Ringfall Formal Intervention Gates With Refinery v0.1

## Status

- Decision status: approved direction after Wave 1 contract handoff.
- Implementation status: planning and architecture integration only.
- Runtime status: no Refinery runtime integration, no C#/.NET bridge, no Python brain bridge, no Docker/Java dependency, and no formal model CI are implemented by this document.
- First intended implementation family: Aster Heat Alarm L1 `ToolActionRequest` / `WorkOrderRequest` intervention gate in Wave 4.

## Decision

Ringfall will use Refinery as a bounded formal intervention gate for execution-impacting LLM proposals, family by family.

Ringfall will not attempt to build a complete formal model of the whole world. The formal model covers only the intervention surface that the Director/Core is willing to consider. Anything outside the modeled surface is explicitly `unsupported`, not implicitly valid.

The strategic rule is:

```text
LLM = creative proposal generator
JSON Schema = artifact shape and typed boundary
Refinery = bounded graph consistency and formal safety gate
Core validator = authoritative runtime guard
Runtime apply = atomic deterministic mutation
```

## Why this exists

Ringfall is intentionally LLM-heavy. That creates a specific failure mode: an LLM can produce plausible but unsafe proposals that look valid as JSON.

JSON Schema can prove that a packet has the required fields and enum values. It cannot prove that the graph of actor, tool, crew, location, visibility, authority, memory, and doctrine relationships is consistent.

Refinery adds a formal layer for the relationships that matter for one bounded intervention family:

- whether a proposed actor may target a crew;
- whether a tool supports the requested action;
- whether a location or state fact is visible to the proposing layer;
- whether an L1 packet is trying to perform L2/L3 authority;
- whether a council emergency measure has a duration, sunset, and macro-only scope;
- whether a memory update is mixing rumor, belief, official line, and fact roles.

The goal is not to make the LLM stop hallucinating. The goal is to ensure hallucinated proposals do not get direct write access to the world.

## Non-goals

- No full formal world model.
- No attempt to model all ecology, combat, economy, social, or provider behavior.
- No claim that `Refinery valid` means the whole runtime state is correct.
- No direct LLM world-state patching.
- No cloud Refinery dependency for canonical runs unless Meta explicitly approves it later.
- No Refinery dependency in Wave 1 or Wave 1.5 contract CI.

## Placement in the Ringfall pipeline

For execution-impacting proposals, the long-term pipeline is:

```text
runtime snapshot
-> minimal formal fact export
-> LLM candidate packet / candidate facts in a designated output area
-> JSON Schema validation
-> Refinery formal intervention gate
-> bridge mapper
-> Core authority validator
-> atomic deterministic runtime apply
-> ActionTrace / StateDiff / MemoryUpdate / EvalSummary evidence
```

The gate produces a verdict, not a runtime mutation:

```text
valid | invalid | repairable | unsupported | fallback
```

Only `valid` outputs may proceed to the bridge mapper. `repairable` may go back to the LLM or a deterministic repair path. `invalid`, `unsupported`, and `fallback` do not mutate the world.

## Runtime fact authority

Refinery must not invent runtime truth. The Core exports only the facts needed for the current intervention family.

Each formal gate run must record:

- source runtime snapshot or artifact bundle id;
- exported fact family and version;
- candidate packet id;
- Refinery model family and version;
- unsupported surfaces encountered;
- final verdict;
- mapper/core validator agreement or disagreement.

If the Core and Refinery disagree, the safe result is `unsupported` or `fallback`, not `valid`.

## First formal family: Aster L1 action/work-order intervention

The first serious family should cover Wave 4 Aster Heat Alarm L1 intervention proposals.

### In-scope runtime facts

- actor id, actor layer, and actor current location;
- context visibility for the actor;
- known local tools such as `local_grid_panel` and `maintenance_console`;
- supported tool actions for those tools;
- crew ids, crew availability, and allowed crew task types;
- target locations visible/reachable for the family;
- candidate `ToolActionRequest`, `WorkOrderRequest`, or selected `SceneActionPacket` action facts.

### In-scope candidate assertions

- issuer actor proposes a tool action;
- issuer actor proposes a work order;
- candidate targets one tool, one crew, or one allowed scene action;
- candidate uses one bounded action family such as inspect, dry-run reroute, communicate, queue crew inspection, or request report.

### Initial error predicates

- L1 proposes L2/L3/council authority.
- Candidate targets a hidden-only location or state fact.
- Candidate references a non-existent actor, tool, crew, context, or location.
- Candidate tool action is not supported by the tool.
- Candidate work order targets an unavailable crew.
- Candidate proposes direct state mutation instead of a bounded command.
- Candidate requests `execute` when the family only allows `dry_run`.
- Candidate observable output includes hidden-only evidence.

### Explicitly unsupported in the first family

- global optimization of Aster utility systems;
- multi-sector resource planning;
- L2 institution orders;
- L3 council doctrine;
- combat, ecology, economy, and social simulation consequences;
- natural-language truth of rationale text.

## Family roadmap

| Family | First wave | Purpose | Gate posture |
|---|---:|---|---|
| F0 artifact-bundle graph | Wave 2 side-lane or Wave 3 prep | Prove manifest -> cognition/action -> state/eval references are connected enough for later loaders and replay | report-only until bundle validation exists |
| F1 Aster L1 action/work order | Wave 4 | Keep L1 tool/crew proposals inside allowed Aster intervention surface | report-only during integration, hard before Wave 4 closeout |
| F2 memory/visibility | Wave 5 | Prevent rumor/fact/official-line/withheld-item contamination and hidden truth leaks | hard for canonical memory/replay evidence |
| F3 L2 institution orders | Wave 6 | Validate institution authority, withholding, escalation, and order scope | hard for canonical L2 verticals |
| F4 L3 doctrine/emergency | Wave 7 | Validate macro-only doctrine, Charter conditions, emergency duration/sunset, and no micromanagement | hard for canonical L3 Black Seam decision |
| F5 cross-institution logistics | Wave 8 or later | Validate bounded campaign/logistics intervention families if they become execution-impacting | candidate, not required for FP1 until scoped |

## No-overclaim matrix

Every formal gate must classify each check:

| Classification | Meaning |
|---|---|
| `proved_by_refinery` | Encoded in the Refinery metamodel/predicates/error predicates and checked by solver semantics. |
| `guarded_by_core_validator` | Enforced by C# runtime authority or deterministic apply logic, not by Refinery. |
| `schema_only` | JSON Schema shape check only. |
| `observability_only` | Captured in traces/eval evidence but not blocking execution. |
| `unsupported` | Outside the modeled family; must not be treated as valid. |

`Refinery valid` is never a project-wide safety claim. It means only that the current candidate is consistent with the current bounded formal intervention family.

## Differential harness requirement

Before a formal family becomes a hard gate, Track B/E must create a differential harness:

```text
fixture or saved artifact bundle
-> JSON Schema checker
-> Refinery check
-> bridge mapper
-> Core authority validator
-> expected verdict comparison
```

Required fixture classes:

- valid minimal candidate;
- invalid schema candidate;
- invalid formal graph candidate;
- Core-rejected candidate;
- unsupported candidate;
- repairable candidate if repair is implemented;
- fallback candidate if deterministic fallback is implemented.

## Ownership

- Meta Coordinator owns sequencing, no-overclaim language, and gate posture.
- Track B owns Core authority, runtime fact export, bridge mapping, and intervention-family semantics that mutate or guard state.
- Track C owns role/prompt/memory meaning and must help define what the LLM is allowed to propose.
- Track D owns brain output area discipline, provider evidence, model retries, and candidate-packet emission.
- Track E owns fixture coverage, differential harnesses, eval evidence, and false-green prevention.
- Track A consumes only artifact outputs and must not treat formal models as live truth.

## Tooling posture

The preferred future canonical route is local and deterministic:

- pinned Refinery CLI or Java library version;
- local execution in CI or dev shell;
- no public Refinery service dependency for canonical evidence unless separately approved;
- all generated formal evidence stored as artifacts with versioned model-family ids.

Wave 1.5 contract CI remains intentionally narrow and does not include Refinery. Formal gate CI slots should be recorded in Wave 1.5 future CI mapping, then implemented only after a formal family has fixtures and a differential harness.

## Hold conditions

- A plan claims full-world formal validation.
- A formal gate accepts an unsupported surface silently.
- Refinery output bypasses Core authority validation.
- A cloud solver endpoint becomes required for canonical local runs without Meta approval.
- `Refinery valid` is documented or displayed as if it proves natural-language truth or complete runtime correctness.
- Core/Refinery verdicts drift without a blocking diagnostic.
