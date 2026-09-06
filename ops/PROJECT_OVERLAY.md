# RingFall Project Overlay

## Adoption And Authority

- Project: `RingFall`
- Overlay maintainer: `RingFall Meta Coordinator`
- Adopted Canon: `5.0.0`
- Canon root: `../Agent-Workflow-Canon`
- Effective date: `2026-09-06`
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

## Router V2 adoption

Shared tooling source is Canon `tooling/opencode`; installed copies are outputs.
Use FAL `Invoke-OCRouter.ps1 -Action` with one private V2 mapping/store. Old stage
manifests are retained history, not fresh-send gates. No per-stage P0B, parser
marker or routine restart approval is required. Session identity, one-send claims,
scoped effects and meaningful acceptance remain. Read PROJECT_STATE for the
imported A4-E boundary: unresolved prior sends must not be replayed. Rollout
permission lifts only the rollout pause, not product gates or uncertainty.
Lane compact/restore uses V2 at idle boundaries; only Owner interrupts sessions
or compacts orchestrators. No lifecycle send by adoption; no private IDs here.

## Canon Exceptions

No Canon exceptions are declared. RingFall's stricter Core, visibility, and
side-effect boundaries are project additions, not exceptions.
