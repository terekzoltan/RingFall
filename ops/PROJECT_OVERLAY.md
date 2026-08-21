# RingFall Project Overlay

## Adoption And Authority

- Project: `RingFall`
- Overlay maintainer: `RingFall Meta Coordinator`
- Adopted Canon: `4.1.1`
- Canon root: `../Agent-Workflow-Canon`
- Effective date: `2026-08-20`
- Root bootloader: `../AGENTS.md`
- Current frontier: `PROJECT_STATE.md`
- Sequence authority: `../docs/plans/Combined-Execution-Sequencing-Plan.md`

Authority order is the Owner and current safety instruction, root `AGENTS.md` plus
this overlay, `PROJECT_STATE.md`, the active Combined row and direct prerequisites,
the accepted Epic artifact, then the adopted Canon and selected role runbook.

## Project Boundaries

- Core is the sole authority for world-state mutation and validation.
- Actor-local and public-facing surfaces must not expose hidden truth, another
  actor's private state, or privileged system facts.
- Brain, projection, planning, and review outputs may propose typed requests but
  do not directly mutate Core or world state.
- Public contract, schema, compatibility, and shared-boundary changes require an
  explicit plan and Meta acceptance.
- FAL and router evidence are non-authoritative continuity inputs; target
  repository authority remains controlling.

## Delivery And Safety

- Preserve unrelated and concurrent work; mutate only the assigned scope.
- Lifecycle routing follows the root `AGENTS.md` and adopted Canon role runbook.
- Implementation does not imply commit, push, publication, deployment, restart,
  or remote side-effect authority.
- On ambiguous authority, candidate, transport, hidden-truth, or mutation state,
  stop and use bounded read-only diagnosis before asking one exact question.

## Explicit-Stage Offline Adoption

- Target-owned state and `ops/stage-sources/` manifests bind the exact lifecycle
  source bytes used by the FAL explicit-stage adapter.
- This repository contains no live endpoint, credential, raw session ID, or
  capability grant. The protected FAL control plane remains local and owner-only.
- The present adoption is offline-only: it validates authority inputs but does
  not authorize or perform a lifecycle send.
- Production dispatch requires a separately admitted capability and an explicit
  Owner action. In particular, A4-E `/seq-next` remains unsent by this adoption.

## Canon Exceptions

No Canon exceptions are declared. RingFall's stricter Core, visibility, and
side-effect boundaries are project additions, not exceptions.
