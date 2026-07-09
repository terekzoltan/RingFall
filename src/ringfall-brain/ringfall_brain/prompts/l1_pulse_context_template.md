# L1 Pulse Context Template

Wave 3 Step 2 `B3-F` Track C artifact.

## Purpose And Non-Runtime Status

This document defines the semantic boundary for an L1 pulse context that asks an embodied actor to produce an `AvatarPulsePacket`.

B3-F outputs markdown/template documentation only; no Python runtime loader, no prompt renderer, no schema or config changes.

This is generic L1 pulse guidance plus a short A1/Aster example. It is not a runtime fixture, not a validator API, and not a model-policy decision.

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
- You may report, request, speak, use local permitted tools, or request work by your own crew.
- You may not change world state directly, invent L2/L3 authority, or issue macro policy.

Output:
- Emit only an AvatarPulsePacket-shaped response.
- Keep follow-up actions as requested packet draft refs, not executed actions.
```

## A1/Aster Example Stance

A1 is the Aster senior grid runner for early Aster Heat Alarm work.

Use this stance when drafting an A1 pulse:

- A1 is operational, local, tired, and competent.
- A1's legitimate view is Aster/Grid-Spine-03 and nearby crew/tool signals.
- A1 can observe heat alarms, crew fatigue, panel readings, and local repair pressure.
- A1 can prefer stabilizing the branch without a full shutdown.
- A1 may protect night-shift crew from blame.
- A1 may suspect deferred maintenance or coupler debt if supported by observations or memory refs.
- A1 can request a work order for `crew_aster_repair_02` or ask for local grid panel checks.
- A1 cannot know hidden thermal vulnerability unless it is discovered through an accepted visible path.
- A1 cannot authorize sector-wide load shedding, set doctrine, speak for the Utility Board, or mutate Core state.

## Sample Output Outline

This is a sample output outline, not a runtime fixture. It is field-aligned with the current `AvatarPulsePacket` schema, but it is not authoritative test data.

```json
{
  "packet_id": "pulse_A1_t000",
  "packet_type": "AvatarPulsePacket",
  "schema_version": "0.1",
  "issuer_id": "A1",
  "issuer_layer": "L1",
  "issued_at_tick": 0,
  "issued_at_turn": "day03_t1",
  "source_context_id": "ctx_A1_aster_heat_t000",
  "actor_id": "A1",
  "local_status": {
    "stress": 0.46,
    "fatigue": 0.58,
    "immediate_risk": "medium"
  },
  "observed": [
    "local heat alarm is active",
    "crew_aster_repair_02 reports elevated fatigue"
  ],
  "belief_updates": [
    {
      "claim": "deferred maintenance may be contributing to the current alarm",
      "confidence": 0.62,
      "source_refs": ["obs_A1_heat_alarm", "crew_report_aster_02"]
    }
  ],
  "intent": "stabilize the branch without claiming macro authority",
  "requested_packets": [
    {
      "packet_type": "WorkOrderRequest",
      "draft_ref": "draft_A1_work_order_inspect_grid_spine_03"
    }
  ],
  "social_note": "avoid blaming night shift before evidence is checked",
  "risk_flags": ["crew_fatigue", "local_heat_alarm"],
  "confidence": 0.7,
  "evidence_refs": ["obs_A1_heat_alarm", "crew_report_aster_02"],
  "visibility_intent": "internal",
  "urgency": "high",
  "rationale": "A1 can see the heat alarm and crew fatigue, so any deferred-maintenance concern remains a source-bound suspicion rather than established fact."
}
```

If this sample becomes a full runtime fixture later, validate it against `avatar-pulse-packet.schema.json` in that future step.

## Track D Handoff Notes

Track D may use this document as semantic guidance only after B3-F is accepted.

- Track D `B3-C` and `B3-D` can proceed independently of B3-F.
- Track D `B3-E` and `B3-G` prompt-facing/runtime behavior should consume this only after B3-F acceptance.
- Track D owns strict JSON parsing, schema validation, provider behavior, retry behavior, OpenRouter handling, trace writing, and cost writing.
- Track C does not define CLI commands, Python APIs, validator APIs, provider APIs, or repair loops here.
- Invalid model output must be rejected, repaired, or classified unsupported before any Core apply.

## Explicit Deferrals

- Positive memory examples for `rumor`, `belief`, `official_line`, and `withheld_item` are not in scope for B3-F unless a later Meta gate opens them.
- Runtime prompt rendering is not in scope.
- Context retrieval and memory retrieval are not in scope.
- OpenRouter/provider behavior is not in scope.
- Trace and cost emission are not in scope.
- Hidden-leak eval implementation is not in scope.
- Schema, schema example, config, Core, Unity, CI, and generated artifact changes are not in scope.

## No-Overclaim Rule

B3-F does not implement runtime prompt rendering, model calls, context retrieval, memory retrieval, trace/cost emission, hidden-leak evals, or formal intervention gates. It only defines a reviewable Track C context and prompt boundary for future runtime consumption.
