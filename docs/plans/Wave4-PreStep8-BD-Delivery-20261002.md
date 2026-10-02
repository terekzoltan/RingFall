# Wave 4 Pre-Step-8 B/D Delivery — 2026-10-02

## Authority and publication boundary

**Local acceptance/closeout: complete. Publication/integration: AUTHORIZED /
PENDING.** Owner authorized the existing orchestrator as sole Git integration
owner for `terekzoltan/RingFall`, source `feat/a4-i-j-evidence`, target `main`,
using a merge commit and verification of the actual candidate and exact target.
No push, PR, remote checks, merge or target verification is claimed by this note.

Meta's current authorization covers only this public-safe note, the current
next-action section of `ops/PROJECT_STATE.md`, a narrow active Combined addendum,
and one exact-path documentation commit:
`docs(governance): record scoped B/D delivery`. This is integration documentation,
not another product closeout. Prior uncommitted governance, private evidence and
session data are excluded. No product edits, deployment, live/paid calls, keys,
gate acceptance or next-Wave authority is added.

## Accepted candidates and retained decisions

| Identity | Track B — gated A1/F1/Core integration | Track D — offline provider client |
|---|---|---|
| Accepted commit | `a1a0982e874179ae2c1eab7cb22f3d521d21363d` | `8c4caf674ddac6a053912f09ee83ce606ad3a2da` |
| Accepted tree | `d65a8aec78940c4290e2b961675d939bd2dba2fe` | `6c8f30733474cceccb16ad02348ffb40fb766484` |
| Parent | `fe3cbe959853969f445b48109daa3d892f3ae946` | B accepted commit above |
| Reviewed candidate SHA-256 | `82571d4eb3b66322ab1bd67b002702a10b7d131761bbf660eb869bc8dd0c945d` — 23 source paths | `68978fcb9e68342fe77a2a2274321c3bd8913b4f33d6f2276cf1227db693df29` — three paths |
| Final Meta FIX_RECHECK | `op-9e738b2e-98ac-479d-9798-44b7029cae4c`; GREEN / ALLOWED | `op-bfbe4c9e-9610-4fc1-a44a-8829f6d6e82f`; GREEN / ALLOWED |
| Accountable ACK_ONLY | `op-9bc6c7c2-7afc-45c0-a769-67af474e3b3b` | `op-a69488e3-f7b4-44e4-bdc0-cfa043bcc608` |
| Completed local closeout | `op-ffbaf13a-4879-4417-90b6-46a5ad794b44` | `op-b94d4191-c382-4d88-a879-5b3af8432905` |
| Closeout result digest | `25992f2e4f4bfd0a445c5e695fa43e673dcd2596f2006403252561fee3cfed80` | `db4e8e49f88309d11310fbffbb038dab9a8b40fddb84a5bb4831714643791593` |
| Resolved review findings | `RF-B-SR-001`–`006` | `RF-D-SR-001`–`008` |
| Retained final offline checks | Core **362/362**; Python preflight **3/3**; Headless build with zero warnings/errors and help smoke | Focused client **42/42**; full Brain **134/134**; CLI help |

These checks are reported accepted local evidence, not tests rerun for this
documentation change or remote composition/target CI. Both product works remain
locally CLOSED and paused; none of their lifecycle stages is replayed.

## Source pins and actual local composition

The clean local candidate before the documentation commit is
`7bbf32785b1ca74570af43473448da5d50b23a7e`, parents accepted D and
`cdce3bda4a75445e423c8ddd09b7f910b2afdaee`. The latter is the PR #5 integration
of the earlier Steps 5–7, with tree equivalent to B's original prerequisite
`fe3cbe959853969f445b48109daa3d892f3ae946`. The routine base merge's tree is
exactly accepted D's tree; it introduces no product composition difference.
Historical publication-pending rows are retained history, not new send requests.

B's committed inventory is
`src/ringfall-core/formal/aster-f1-v0.1/successor-preflight-v1.json`, SHA-256
`582ecbe93f948bb2904210909fa084cd40b1d1489d496a00741cdb6ffe33dbb4`.
All 23 listed source hashes and the inventory itself were verified from committed
raw blobs against accepted B and the clean candidate. B's closeout changed 11
listed sources plus this inventory; 12 historical input paths remained unchanged.
Historical P1 `6bf530a8e1efda55703de8563b0435f1487baaa0fe8b0697c759aefca8f85fbc`
and P2 `652f8abfe1bb140af7dd76ad9660024c964d6e77dfae879207b6fd690815dd45`
aggregates retain their separate identities; they are not passing successor proof.

All three D committed raw blobs match accepted D:

| Path | SHA-256 |
|---|---|
| `src/ringfall-brain/README.md` | `b43b93ce2d7e4b3ab6dcf5cb301cde6e9de5752a498be58fe921bebfa8d78b1e` |
| `src/ringfall-brain/ringfall_brain/providers/aster_trial.py` | `ff6871a95ed238a67af682456c5ed88d13a50f61163a7162ce877e60cf161e7b` |
| `src/ringfall-brain/tests/test_aster_trial.py` | `95357a8b69c19d6b41e685f0c5ec68f106230154e65680b886f6c632967b65d2` |

D's aggregate uses these ordered `path=hash` lines with a final LF. Raw committed
bytes, including accepted line endings, are authoritative; normalized text diffs
alone do not establish candidate equality.

## Limits preserved

- **B:** `schema_validation=not_run` remains a reported limitation; this is not
  proof of the full schema-before-F1 hard gate. The earlier unexplained reroute
  anomaly remains a limitation. Injected wait controls cover `Invoke`, not a
  full-command/live stuck process. Scoped successor proof is not exhaustive
  Wave 4 hard-gate proof and does not replace historical P1/P2 identities.
- **D:** transport-injected, cooperative, offline-only and in-memory. There is
  no live HTTP adapter or live model/provider trial. Fake route/effort outcomes
  do not certify live capability, tokenizer behavior, durable billing or network
  cancellation. Production/live/billing certification requires separate scope.
- **Wave:** all five unchecked mandatory outputs and five holds remain open for
  assessment; Step 8 is **NOT_READY/unopened**. No Wave 4 gate verdict, output
  completion, Wave 5 dispatch or dependent prerequisite waiver is inferred from
  product acceptance, this documentation commit or eventual publication.

## Integration-owner handoff and Meta stop

The existing orchestrator resumes the authorized Git delivery sequence: freshly
verify canonical remote/source/base and the complete publication diff, preserve
accepted B/D ancestry, push the authorized source, create or resume its PR,
verify required candidate/composition checks, merge with a merge commit, then
verify exact `main` commit/tree and required target checks. Required jobs are
Runtime CI `core-dotnet-ci` and `brain-python-ci`, and Contract CI `schema-check`
(display name **Schema checker**). Missing/skipped/pending checks are not PASS;
earlier base CI is not current-candidate or exact-target evidence.

Meta stops after the authorized three-path documentation commit and reports its
hash/tree/path scope. Publication remains **AUTHORIZED / PENDING** until the
integration owner supplies actual remote evidence. No release fragment is
required by the inspected RingFall repository policy for this docs-only change.
