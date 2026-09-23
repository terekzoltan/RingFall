# RingFall Agent Instructions

## AWC5 / Router V2 adoption (2026-09-06)

PROJECT_STATE distinguishes the dated Meta baseline from its Owner-enrolled
observed-progress block. Reconcile current operation evidence before acting on
an old phase/next-action line; this grants no scope or replay authority. Read the
active Combined section and needed frozen references, not the entire roadmap.

Adopted Canon: `5.0.0`. Read `ops/PROJECT_OVERLAY.md`, `ops/PROJECT_STATE.md`
and the current Combined frontier first. Canon `tooling/opencode` owns shared
definitions; FAL Router V2 is the sole sender. AWC5 replaces legacy explicit-stage,
Compact Lite, exact terminal-line and per-stage capability mechanics below.
Domain/Core, visibility, scope and independent review gates remain unchanged.
A4-E is accepted and closed at commit
`9a90c4ed6ccebb88c82c02ffb2e66875f59245e9`; never resend its lifecycle.
The current frontier is Wave 4 Step 4: Track D `A4-H cognition side` is target-
and V2-routing-ready for `/seq-next`, while Track B `A4-G` is target-ready but
requires a verified private RingFall `Track B` mapping before dispatch. Only
Owner interrupts sessions or compacts orchestrators.

Agent Workflow Canon root: `../Agent-Workflow-Canon`
Adoption contract: `../Agent-Workflow-Canon/ADOPTION.md`

## Global Combined Rules

Shared governance baseline: `../FractalAgentLab/docs/templates/Global-Combined-Rules-Template.md`.

This file is the RingFall project overlay. RingFall-specific Core authority, hidden-truth boundaries, accepted frontier, and stricter safety rules remain authoritative. The shared template supplies defaults where this file and the local Combined plan are silent.

## OC Session Router Workflow

Router mechanics are owned by `../FractalAgentLab/tools/oc-session-router/docs/workflow-orchestrator-runbook.md`. RingFall has no local exception to its single-pass plan-review law:

```text
Track /seq-next -> Meta /terv-review -> Track /terv-review-utan -> Track /implement
```

Only Meta `/terv-review` emits `GREEN / YELLOW / RED`. Track `/terv-review-utan` revises the plan without another color verdict or plan-review loop. Every final step-review synthesis routes through `/step-review-utan`; a bounded fix plan then receives one Meta `/terv-review` and one Track `/terv-review-utan`, and only `PLAN_REVISION_COMPLETE` plus `IMPLEMENT_READY` routes to `/implement`. Material replanning restarts at `/seq-next`.

## Project Git Delivery

Owner-approved convention: follow the adopted Canon's
`canon/GIT-DELIVERY-AND-INTEGRATION.md`. Integration target is `main`, verified
again before publication. Each independently integrable Epic/workflow-fix uses
a short-lived branch + PR; repairs stay on that branch. Independent concurrent
writers use authorized worktrees; stages/reviewers do not each need a PR or session.

The existing orchestrator may coordinate or execute the explicitly authorized
post-closeout Git sequence; Meta `/closeout-commit` remains local and never pushes.
One bounded Owner grant may cover push, PR, checks, merge and target verification.
No grant is inferred from this convention. Preserve accepted commit identity where
possible, serialize shared integration/governance and verify actual composition.
Existing work branches, local acceptance and narrower stops remain valid; no
retroactive rebranch/review replay, automatic session creation or next-Epic dispatch.
This does not adopt a new `GITHUB_PR` product lifecycle/profile.

## Combined Plan Sequencing Protocol

When editing `docs/plans/Combined-Execution-Sequencing-Plan.md`, follow the canonical execution-table protocol in section 3 of that document.

Short form:

- One execution-table row equals one concrete session assignment; that assignment may cover one or more epics, or one named gate.
- Multiple epic IDs may share one `Epic(s)` cell when the same session can complete them as one coherent assignment.
- A numbered step is a chronological barrier.
- Multiple rows in the same step mean those rows may run in parallel from the same accepted prerequisite state.
- Rows in the same step must use distinct `Session` labels and must not depend on each other's output.
- When a session owns multiple epics, always offer separate rows/steps as an optional clarity or risk-reduction choice; bundling remains allowed and the split is not mandatory.
- If one row needs another row's artifact, review, or gate result, it belongs in a later step.
- Closeout and wave-gate decisions are rows too: one gate per row.
- Older bundled rows do not require normalization solely because they contain multiple epics; revise them only when their ordering, ownership, or prerequisites are ambiguous.

If a bundled row's ordering is unclear, offer a split and ask Meta Coordinator rather than guessing its internal sequence.
