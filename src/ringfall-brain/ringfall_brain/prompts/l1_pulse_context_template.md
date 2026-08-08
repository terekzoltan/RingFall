# L1 Pulse Context Template

Wave 3 Step 2 `B3-F` Track C baseline, extended for Wave 4 `A4-D`.

## Purpose And Non-Runtime Status

This document defines the semantic boundary for an L1 pulse context that asks an embodied actor to produce an `AvatarPulsePacket`.

B3-F and A4-D output markdown guidance plus static acceptance evidence only; no Python runtime loader, prompt renderer, schema, or config changes.

This is generic L1 pulse guidance plus a bounded A1/Aster pulse and scene contract. It is not a runtime renderer, validator API, provider path, or model-policy decision.

Target packet: `AvatarPulsePacket` from `src/ringfall-contracts/schemas/packets/avatar-pulse-packet.schema.json`.

## Allowed Context Classes

An L1 pulse context may include only actor-visible, source-aware material:

- Actor identity: `actor_id`, layer, role family, role archetype, home sector, current location, affiliation, rank, public goals, and relevant loyalties.
- Local scene: the actor's current visible work area, local incident summary, and current tick or turn labels.
- Immediate observations: sensor alarms, tool readings, crew reports, visible fatigue, visible damage, messages, and other observations actually available to the actor.
- Bounded working memory: current intent, active crew, active observations, recent tool result summaries, and active risk flags.
- Beliefs and suspicions: explicitly labeled as beliefs, suspicions, or uncertainty with source refs and confidence when known.
- Local affordances: tools, consoles, work-order channels, crew units, and social channels the actor is allowed to use.
- Output guidance: required `AvatarPulsePacket` shape, evidence refs, confidence, visibility intent, and requested follow-up packet drafts.

## A4-D A1 Context Binding

The A4-D A1 prompt consumes the accepted Core projection represented by `src/ringfall-brain/examples/aster-a1-context.example.json`.

Its complete input surface is:

- `actorId`, `displayName`, `role`, and `layer`;
- observations containing `observationId`, `kind`, `signal`, and `sourceRef`;
- scalar `crewRefs` and `toolRefs`.

For the first Aster Heat Alarm context, `issuer_id == actor_id == context.actorId == A1`.

Observation provenance is classified as **trusted pre-materialized Core state** for this vertical:

- Core owns and validates each projected `ActorLocalObservation` before serialization.
- Prompt `observed` text must copy the accepted observation `signal` exactly.
- Packet `evidence_refs` and belief `source_refs` must retain the matching `observationId` and `sourceRef`.
- `source_context_id` is correlation metadata; it does not prove that arbitrary prose came from a source.
- A model interpretation belongs in a belief, intent, rationale, or risk field. It must not replace or rewrite the source-bound observation as fact.

A4-D does not add a public observation-ingest path or claim that signal text is mechanically rendered from an event or sensor payload. A future change from trusted pre-materialized signals to a renderer or ingest contract requires separate review.

The accepted projection is printable ASCII. A4-D retains that character set for this canonical first-vertical context and pulse example. This is not a product-wide localization policy; localized or richer Unicode prompt context requires a separately accepted contract before runtime use.

`crewRefs` and `toolRefs` are identifiers only. Their presence does not disclose or grant resource status, availability, supported actions, capability, permission, authority, or execution. They may identify a proposed request target, but every candidate remains subject to downstream schema and Core validation.

## A4-D Pulse And Scene Boundary

- A T1 pulse produces an `AvatarPulsePacket`-shaped candidate from the bounded actor context.
- A T1b scene is a scenario- or high-risk-triggered escalation, not an automatic consequence of every pulse.
- A pulse may place `SceneActionPacket`, `ToolActionRequest`, or `WorkOrderRequest` entries in `requested_packets` using only `packet_type` and `draft_ref`.
- A requested packet is a proposal. It does not prove that an action is allowed, validated, executed, or applied to Core state.
- A later scene candidate may target only actor-visible identifiers, but the identifier alone does not establish a supported action or available resource.
- A4-D defines prompt meaning only. A4-E owns candidate emission, A4-F owns Core authority validation, and A4-I owns vertical provenance and hidden-leak evidence.

## Forbidden Context Classes

An L1 pulse context must not include hidden or higher-authority material as actor knowledge:

- Core world truth store facts that the actor has not observed.
- Hidden thermal vulnerability such as `Aster-G4 has hidden thermal vulnerability` as something A1 knows.
- Hidden side effects, latent debt, delayed triggers, or private simulator annotations unless discovered through a visible path.
- Utility Board private intent unless it was communicated or observed by the actor.
- L2 institution dashboard truth beyond the actor's legitimate local view.
- L3 council doctrine, emergency powers, or macro steering authority as something the L1 actor may exercise.
- Full lore, world-bible truth, or developer-only background that would make the actor omniscient.
- Direct world patches, state diffs, or instructions to mutate Core state.
- Provider, OpenRouter, retry, cost, trace, or runtime implementation instructions.

Forbidden terms are findings only when they appear as allowed knowledge, authority, or output behavior. It is acceptable to list them in this `Forbidden Context Classes` section.

## Required Output Discipline

The model must be instructed to emit only packet-shaped `AvatarPulsePacket` content.

Required packet discipline:

- Use `packet_type: "AvatarPulsePacket"`.
- Use `schema_version: "0.1"`.
- Keep `issuer_layer: "L1"` for embodied actor pulses.
- Preserve observations as observations.
- Preserve beliefs, suspicions, and rumors as non-fact claims with confidence and source refs when available.
- Use `requested_packets` only for draft follow-up packet requests such as `WorkOrderRequest`, `ToolActionRequest`, or `SceneActionPacket`.
- Do not state that a requested packet has executed.
- Do not write direct Core state patches, state diffs, or tool results.
- Do not claim provider, cost, trace, or validation behavior.
- If a proposal exceeds L1 authority or available knowledge, omit it or mark it as uncertainty rather than inventing authority.

Schema-permitted fields for this step include:

- `packet_id`
- `packet_type`
- `schema_version`
- `issuer_id`
- `issuer_layer`
- `issued_at_tick`
- `issued_at_turn`
- `source_context_id`
- `actor_id`
- `local_status`
- `observed`
- `belief_updates`
- `intent`
- `requested_packets`
- `social_note`
- `risk_flags`
- `confidence`
- `evidence_refs`
- `visibility_intent`
- `urgency`
- `rationale`

Do not present invented fields as canonical contract vocabulary. If a schema gap appears, route it to Meta and Track B instead of changing the schema locally.

The A4-D A1 example does not populate `local_status`, `social_note`, or resource-detail fields because the accepted actor context does not expose evidence for them.

## Generic L1 Pulse Prompt Skeleton

Use this shape as prompt guidance, not as runtime renderer syntax:

```text
You are an L1 embodied actor in Ringfall.

Role:
- Actor id: <actor_id>
- Layer: L1
- Role archetype: <role_archetype>
- Current location: <current_location>
- Local duties: <visible duties only>

Visible context:
- <observation id or source ref>: <what the actor can actually observe>
- <crew/tool/source ref>: <local signal available to this actor>

Working memory:
- Current intent: <bounded current intent>
- Active crew: <crew ids or none>
- Recent relevant memory: <source-aware memory summary, if any>
- Current risk flags: <actor-visible risks>

Belief boundary:
- Treat observations as observations.
- Treat suspicions as beliefs, not facts.
- Do not infer hidden truth from developer or Core state.

Authority boundary:
- You may report, speak, or draft requests that target actor-visible identifiers.
- A visible identifier does not prove permission, availability, or a supported action.
- You may not change world state directly, invent L2/L3 authority, or issue macro policy.

Output:
- Emit only an AvatarPulsePacket-shaped response.
- Keep follow-up actions as requested packet draft refs, not executed actions.
```

## A1/Aster Example Stance

A1 is the Aster senior grid runner for early Aster Heat Alarm work.

Use this stance when drafting an A1 pulse:

- A1 is an operational L1 senior grid runner.
- A1's accepted context contains identity, role, layer, one local R2 heat-alarm observation, and scalar crew/tool references.
- A1 can observe exactly `Local R2 heat alarm is active.` from `event:event-t000-aster-r2-heat-alarm`.
- A1 cannot infer crew status, tool status, supported actions, or resource authority from a reference alone.
- A1 may state a bounded intent to inspect or stabilize the local alarm condition.
- A1 may express a source-bound belief that local inspection is needed, but the alarm does not establish its cause.
- A1 can draft a request involving `crew_aster_repair_02`, `local_grid_panel`, or `maintenance_console`; the draft does not grant capability or claim execution.
- A1 cannot know hidden thermal vulnerability unless it is discovered through an accepted visible path.
- A1 cannot authorize sector-wide load shedding, set doctrine, speak for the Utility Board, or mutate Core state.

## A4-D A1 Pulse Example

The tested static example is `src/ringfall-brain/examples/aster-a1-pulse.example.json`.

It is acceptance evidence, not runtime emission. It must:

- validate against `avatar-pulse-packet.schema.json`;
- preserve `issuer_id == actor_id == context.actorId == A1`;
- copy the accepted heat-alarm signal exactly;
- retain the observation and source references as evidence;
- keep interpretations uncertain and source-bound;
- represent scene, tool, and work-order follow-ups as drafts only;
- contain no hidden metric, resource metadata, execution result, or Core mutation data.

## Downstream Handoff Notes

- A4-E may consume this accepted semantic contract to emit strict candidate packets after A4-D closes.
- A4-F independently owns deterministic Core authority and action validation.
- A4-I must verify the trusted pre-materialized provenance decision and hidden-leak boundary before Wave 4 closeout.
- Track D owns strict JSON parsing, schema validation, provider behavior, retry behavior, OpenRouter handling, trace writing, cost writing, and candidate emission.
- Track C does not define CLI commands, Python APIs, validator APIs, provider APIs, repair loops, or execution behavior here.
- Invalid model output must be rejected, repaired, or classified unsupported before any Core apply.

## Explicit Deferrals

- Positive memory examples for `rumor`, `belief`, `official_line`, and `withheld_item` are not in A4-D scope.
- Runtime prompt rendering is not in scope.
- Context retrieval and memory retrieval are not in scope.
- Public observation ingest and event/sensor signal rendering are not in scope; the first vertical consumes trusted pre-materialized Core observations.
- Localization implementation is not in scope; printable ASCII remains limited to the canonical first-vertical transport.
- OpenRouter/provider behavior is not in scope.
- Trace and cost emission are not in scope.
- Hidden-leak eval implementation remains A4-I scope.
- Candidate packet emission remains A4-E scope, and Core authority validation remains A4-F scope.
- Schema, schema example, config, Core, Unity, CI, and generated artifact changes are not in scope.

## No-Overclaim Rule

B3-F and A4-D do not implement runtime prompt rendering, model calls, context retrieval, memory retrieval, candidate emission, authority validation, execution, trace/cost emission, hidden-leak evals, or formal intervention gates. They define a reviewable Track C context and prompt boundary for future runtime consumption.
