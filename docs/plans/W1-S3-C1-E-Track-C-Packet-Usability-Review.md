# W1-S3/C1-E Track C Packet Usability Review

**Task:** W1-S3-C1-E
**Owner:** Track C
**Status:** review/handoff artifact
**Scope:** W1-S2 packet usability, role/prompt fit, hidden-truth safety, source-awareness, authority boundaries, and C1-E guidance for Track B/Meta.

## Scope Boundaries

Track C owns this document as packet-usability review evidence. Track C does not implement schemas in this lane.

In scope:
- review W1-S2 packet schemas for role/prompt usability;
- classify W1-S2 schemas as `usable as-is`, `usable with caveat`, or `needs Track B schema change`;
- provide C1-E guidance for `InstitutionBrief`, `InstitutionOrder`, and `CouncilDoctrinePacket`;
- identify hidden-truth, withholding, source-awareness, evidence, confidence, and authority-boundary risks;
- hand off recommendations to Track B and Meta.

Out of scope unless Meta expands the task:
- editing `src/ringfall-contracts/schemas/**/*.schema.json`;
- adding `InstitutionBrief`, `InstitutionOrder`, or `CouncilDoctrinePacket` schema files;
- adding `CouncilNoActionPacket` or `WithholdingRecord` schemas;
- trace, memory, eval, cost, or run-manifest schemas;
- examples, fixtures, validation tooling, C#/.NET, Python brain/runtime, provider/model behavior, Unity work, scenarios, generated artifacts, or simulation logic.

## W1-S2 Usability Verdicts

| Schema | Track C verdict | Evidence | Role / prompt impact |
|---|---|---|---|
| `AvatarPulsePacket` | usable with caveat | Supports local status, observed items, belief updates with confidence/source refs, intent, requested packet refs, social note, and risk flags. | Good L1 pulse surface. Caveat: `belief_updates[].claim` is free text, so prompt instructions must preserve rumor/belief/observation distinctions until future memory schemas make claim typing stricter. |
| `SceneActionPacket` | usable with caveat | Requires scene/actor/goal/actions and uses conditionals for `use_tool`, `issue_work_order`, and `communicate`. | Good L1 scene-action surface. Caveat: `issue_work_order` currently targets a crew id only; institution-scale dispatch and crew-pool targeting should remain in C1-E or later L2 packet work, not be overloaded into L1 actions. |
| `WorkOrderRequest` | usable as-is | Requires a target crew or crew pool, location, task type, priority, and bounded optional constraints/resources/visibility fields. | Usable for bounded delegation without granting doctrine authority. Prompt templates must make clear that issuer layer labels do not create authority. |
| `ToolActionRequest` | usable with caveat | Requires tool id, action, and mode; allows arbitrary `arguments` because tool-specific argument contracts are deferred. | Usable as a generic early contract. Caveat: prompts must not treat arbitrary arguments as permission to invent tool capabilities or bypass core validation. |
| `ExecutionResult` | usable as-is | Separates issuer, institution, and public observable summaries from internal `hidden_effects`. | Strong visibility split for hidden-truth safety. Prompt context builders must only expose the observable layer authorized for the recipient. |

## Findings

| severity | schema/field | role impact | recommendation | owner |
|---|---|---|---|---|
| medium | `AvatarPulsePacket.belief_updates[].claim` | L1 memory summaries could collapse observation, belief, rumor, and official line into a single untyped text claim. | Keep W1-S2 as usable, but C1-E and later memory schemas should preserve source/confidence/uncertainty explicitly; prompt law must mark rumor as rumor and belief as belief. | Track C / Track B later |
| low | `SceneActionPacket.actions[].issue_work_order` | L1 scene prompts may be tempted to use local scene action as institution dispatch. | Keep L1 action bounded; route institution-scale orders through C1-E `InstitutionOrder`, not through `SceneActionPacket`. | Track C / Track B |
| medium | `ToolActionRequest.arguments` | Prompt outputs could invent unsupported tool arguments or capabilities. | Track C prompt guidance should require tool actions to stay within `allowed_tools`; Track B/runtime validation later remains authoritative. | Track C / Track D later |
| high | `ExecutionResult.hidden_effects` | Hidden effects are safe only if not placed into unauthorized prompt context. | Treat `hidden_effects` as internal execution evidence only; context builders should use `issuer_observable`, `institution_observable`, or `public_observable` according to recipient visibility. | Track C / Track B / Track E later |
| high | future C1-E withholding fields | L2 withholding can leak hidden truth if raw withheld facts are embedded in public or L1-visible packets. | C1-E should prefer counts, refs, reasons, review cycles, or future `WithholdingRecord` refs over raw hidden details in broad-visible fields. | Track B / Meta |
| medium | future `CouncilDoctrinePacket.macro_orders` | L3 prompts could drift into scene-level micromanagement. | Require macro/doctrine language, duration/sunset/charter conditions, and audit visibility; do not model direct terminal/tool/crew actions as L3 doctrine. | Track B / Track C |

## C1-E Guidance For Track B

### Common Packet Conventions

Track B should preserve the W1-S2 conventions for any C1-E packet schema accepted by Meta:
- JSON Schema Draft 2020-12;
- `schema_version.const = "0.1"`;
- `packet_type.const`;
- `additionalProperties: false`;
- common metadata where appropriate: `packet_id`, `issuer_id`, `issuer_layer`, `issued_at_tick`, `source_context_id`, `rationale`, `confidence`, `evidence_refs`, `visibility_intent`, and `urgency`;
- issuer layer labels must not grant runtime authority;
- no fields that let LLM output directly mutate authoritative world state.

### InstitutionBrief

`InstitutionBrief` should be an L2 aggregate report, not a neutral omniscient summary.

Recommended semantics:
- institution identity and cycle/context identity;
- summary, evidence refs, uncertainties, risk flags, and confidence;
- recommended orders as proposals or refs, not hidden state mutation;
- escalation-needed signal for council or cross-institution routing;
- public/institutional narrative line kept distinct from source evidence;
- withheld item count or refs, not raw hidden details in broadly visible fields.

Track C guardrail: a brief can be biased, strategic, and institutionally self-protective, but it must remain source-aware and traceable.

### InstitutionOrder

`InstitutionOrder` should express L2 authority within mandate, not civilization-wide doctrine.

Recommended semantics:
- institution id and issuing seat/role id;
- order type, target scope, priority, resources, constraints, expected effect, and known tradeoffs;
- visibility and public/institutional exposure policy;
- duration, sunset, or review condition when the order has continuing effect;
- evidence refs and rationale;
- no charter exception, deep doctrine rewrite, or all-sector authority unless L3 has granted it through a separate packet.

Track C guardrail: L2 can dispatch, allocate, restrict, narrate, escalate, and pressure L1 actors within mandate; it must not invent top-level emergency authority.

### CouncilDoctrinePacket

`CouncilDoctrinePacket` should express L3 doctrine, emergency authority, macro priority, truth policy, and charter conditions. It should not be a scene-level action plan.

Recommended semantics:
- council cycle id and decision mode;
- doctrine shift fields with bounded values or named policies;
- emergency measures with scope, duration/sunset, granted authority, and public justification requirements;
- macro orders at policy level, not local tool/terminal actions;
- charter conditions, audit visibility, and post-action review requirements;
- dissenting positions or dissent refs so final doctrine does not erase contested reasoning;
- explicit no-direct-world-mutation boundary.

Track C guardrail: L3 can set doctrine and exceptional authority, but cannot directly turn bolts, issue L1 social actions, or treat raw local rumor as global truth.

## Withholding And Hidden-Truth Safety

Withholding is the highest C1-E hidden-truth leak risk.

Track C recommendations:
- separate ground truth, observation, belief, rumor, official line, and institutional line in prompt/context wording;
- do not put raw hidden facts into L1-visible, public-visible, or broad institution-visible fields;
- use withheld counts, refs, reasons, exposure risk, review cycle, or future `WithholdingRecord` references where possible;
- require evidence/source refs for claims that drive institutional action;
- preserve uncertainty and contradiction instead of converting inference into fact;
- keep `ExecutionResult.hidden_effects` internal and expose only authorized observable summaries.

`WithholdingRecord` is deferred unless Meta expands C1-E. If deferred, C1-E should still leave a clean path for later withholding evidence by using refs/counts rather than raw withheld content.

## Deferred Unless Meta Expands

The following are explicitly out of this Track C lane and should not be implemented as part of this artifact:
- `CouncilNoActionPacket`;
- `WithholdingRecord` schema;
- trace schemas including run/cognition/action/state-diff traces;
- memory schemas including `ClaimRecord` and `MemoryUpdate`;
- eval/cost schemas;
- examples, fixtures, and schema validation tooling;
- prompt/runtime implementation, model calls, provider code, Unity, scenarios, or simulation logic.

## Track B / Meta Handoff Requirements

Before Track B finalizes C1-E schemas, Meta should decide which Track C recommendations become hard schema acceptance criteria.

Track B should inspect this artifact for:
- W1-S2 usability caveats that affect C1-E field design;
- withholding and hidden-truth handling requirements;
- L2 authority boundaries for `InstitutionBrief` and `InstitutionOrder`;
- L3 anti-micromanagement boundaries for `CouncilDoctrinePacket`;
- evidence/confidence/source-awareness expectations;
- deferred packet concepts that should not be silently pulled into C1-E.

Track C closeout assertion: this artifact is review/handoff evidence only. It does not modify schemas, runtime behavior, examples, validation tooling, or prompt implementation.
