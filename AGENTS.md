# RingFall Agent Instructions

## AWC5 / Router V2 adoption (2026-09-06)

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

Owner decision: use one short-lived branch per Epic, based on the accepted
`main` baseline. A bounded repair may use a separate repair branch. This is a
project Git convention, not adoption of the proposed AWC `GITHUB_PR` lifecycle.
Existing planning, independent review, ACK and closeout gates remain unchanged.

- `main` contains integrated, accepted work; do not implement Epics directly on it.
- Record the base SHA and Epic branch before dispatch; verify root/branch again on resume.
- Commit, push and merge require explicit Owner authority, which may cover one bounded sequence.
- PRs are optional unless repository rules or the Owner require them; CI and review are not optional.
- Prefer fast-forward integration when possible, preserving accepted commit identities.
- Divergence requires integration review and tests; never silently squash, rebase or force-push accepted evidence.
- Do not switch a shared checkout beneath active writers. Parallel writers require separately authorized worktrees or serialized execution.
- Reconcile shared governance files serially; verify default-branch CI before starting dependent work.

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
