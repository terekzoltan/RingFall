# RingFall Agent Instructions

## Combined Plan Sequencing Protocol

When editing `docs/plans/Combined-Execution-Sequencing-Plan.md`, follow the canonical execution-table protocol in section 3 of that document.

Short form:

- One execution-table row equals one session assigned to one epic or one named gate.
- Do not put comma-separated epic IDs in a single `Epic(s)` cell.
- A numbered step is a chronological barrier.
- Multiple rows in the same step mean those rows may run in parallel from the same accepted prerequisite state.
- Rows in the same step must use distinct `Session` labels and must not depend on each other's output.
- If the same session owns two epics, split them into separate numbered steps until a future same-session fork workflow is explicitly approved.
- If one row needs another row's artifact, review, or gate result, it belongs in a later step.
- Closeout and wave-gate decisions are rows too: one gate per row.
- If an older planned section still has bundled legacy rows, normalize that section before opening it for implementation.

If the ordering is unclear, stop and ask Meta Coordinator instead of hiding the ambiguity in a multi-epic row.
