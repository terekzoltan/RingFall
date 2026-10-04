# Aster F1 pre-execution successor — local evidence boundary

The `aster-f1-gated-execute` Headless command takes `--state`, `--candidate`,
`--pulse`, `--context`, and `--out` (a **new** directory under the private OS
temp `opencode` folder). It captures the four inputs once, produces a Core-only
`AsterF1Preflight` v1 record without executing, then invokes the adjacent
`run_preflight_v1.py` adapter through an operator-pinned absolute Python path.
The adapter checks `successor-preflight-v1.json`, binds the captured inputs and
uses unchanged `map_p1()` to write the mapped instance; it does not run Docker
or supply an execution verdict. The Headless parent independently derives and
compares the instance, then invokes an operator-pinned **absolute Docker executable**
for image inspection, `check -k`, `generate -o -` and owned-container settlement.
It captures actual exit codes and raw pipes. The external read-only operator
freeze record is prepared independently after each successor source freeze;
edited source plus a matching edited manifest cannot establish its trust anchor.
Only a real check, a complete closed generated witness (including an exact
pinned-model prefix), and Core agreement permit fresh Core validation and bounded
execution. A failed command does not publish an authoritative state or success
bundle. Parent-owned raw stdout/stderr, receipts and invocation links stay in a
private temp directory; a successful bundle is published atomically to `--out`,
with packet-linked artifacts and a file-hash manifest. The bundle's
`schema_validation=not_run` is intentional:
this slice does not supply the full Wave 4 JSON Schema-before-F1 hard gate.

`successor-preflight-v1.json` binds the nine P1 paths, all six **unchanged** P2
paths, and eight successor integration/source/test/guide paths (23 paths total).
It excludes itself to avoid self-referential hashing.
Freeze the per-path hashes and aggregate **before** generating real evidence;
an independent operator must refresh the external anchor for the new manifest
identity before any resumed solver run.
The old `P1_AGGREGATE`, P2 aggregate and Step 7 evidence are historical and
must not be changed. Run their old regression separately on accepted bytes;
the old source-identity test intentionally rejects changed successor bytes.

After an explicitly authorized local pinned run, inspect both the private
raw-receipt hashes and the final bundle manifest. These are scoped A1/F1/Core
evidence only; complete schema proof, visibility partitions, five actors,
memory, provider trials and the Wave 4 gate remain separate decisions.
