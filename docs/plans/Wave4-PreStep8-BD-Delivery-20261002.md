# Wave 4 Pre-Step-8 B/D Delivery — 2026-10-02

## Authority and publication boundary

**Local acceptance/closeout: complete. Publication/integration: AUTHORIZED /
PENDING.** Owner authorized the existing orchestrator as sole Git integration
owner for `terekzoltan/RingFall`, source `feat/a4-i-j-evidence`, target `main`,
using a merge commit and verification of the actual candidate and exact target.
No push, PR, remote checks, remote merge or target verification is claimed by
this note. The current local delivery candidate includes the separately accepted
D portability repair described below.

Meta's current authorization covers only this public-safe note, the current
next-action section of `ops/PROJECT_STATE.md`, a narrow active Combined addendum,
and one new exact-path documentation reconciliation commit:
`docs(governance): record scoped B/D delivery`. This is integration documentation,
not another product closeout. Prior uncommitted governance, private evidence and
session data are excluded. No product edits, deployment, live/paid calls, keys,
gate acceptance or next-Wave authority is added.

## Original accepted candidates and retained decisions — historical

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
The original D checks describe its original offline closeout, not clean-LF
portability or the current repaired source pins. Its separate repair is also
closed locally, with its own review, acknowledgement and commit below.

## Original delivery composition and source pins — historical

The original clean local candidate before documentation commit
`5388edfdf73448c1be4be2267bb7d44a504e1844` was
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

The following three D raw blobs match the original accepted D and original
documentation candidate `5388edfdf73448c1be4be2267bb7d44a504e1844` only. This
historical table is superseded for current delivery by the repair table below:

| Path | SHA-256 |
|---|---|
| `src/ringfall-brain/README.md` | `b43b93ce2d7e4b3ab6dcf5cb301cde6e9de5752a498be58fe921bebfa8d78b1e` |
| `src/ringfall-brain/ringfall_brain/providers/aster_trial.py` | `ff6871a95ed238a67af682456c5ed88d13a50f61163a7162ce877e60cf161e7b` |
| `src/ringfall-brain/tests/test_aster_trial.py` | `95357a8b69c19d6b41e685f0c5ec68f106230154e65680b886f6c632967b65d2` |

D's aggregate uses these ordered `path=hash` lines with a final LF. Raw committed
bytes, including accepted line endings, are authoritative; normalized text diffs
alone do not establish candidate equality.

## Separate accepted D portability repair

The unchanged original provider's CRLF-specific source pins caused a genuine
clean-LF failure: **134 tests ran / 42 errors**. That publication block is
historical; the separately reviewed repair is now closed locally. The original
B/D closeouts remain closed and are not replayed.

- **Repair commit:** `53e28060f443d72b28d43ee0941eb70a975aa3f9`.
- **Repair tree:** `1fc4f4d476fb36ca1c3dddea39c941b826d55fe0`.
- **Sole parent:** `fdd28a51de7a73afe98417a128b3af32b8f1a1cf`.
- **Final Meta review:** `op-dbded5e5-516c-48c5-980d-ebf8db32e0c0`,
  **GREEN / PASS / ALLOWED**, no findings.
- **Exact Track D ACK_ONLY:** `op-a8f4c8c5-ebda-4992-9260-3e05d214e66a`.
- **Local closeout:** completed with exactly the three product paths below;
  governance delta **NONE**. No plan, private evidence or unrelated work entered
  the repair commit.

These are the current committed raw source pins, verified against both the
accepted repair and current local delivery candidate:

| Path | SHA-256 |
|---|---|
| `src/ringfall-brain/README.md` | `8972bbea277f499accf265199a564509b1a7a1181ed818d17e3b795215458222` |
| `src/ringfall-brain/ringfall_brain/providers/aster_trial.py` | `15bc5d0559916fa1f44a29c2c5b45f1c22b0ea95a4902439907f70631ccca092` |
| `src/ringfall-brain/tests/test_aster_trial.py` | `2ffcaf79d823c313b0ba11852911fbf64666a9e13187003e7477cd3d344c3196` |

Their ordered UTF-8 `path=hash` records, LF-separated with a final LF, reproduce
reviewed aggregate
`9bffad6d3ae455d7f0e5d4c3adf9b2fa993cd8219e105cbec8ebea844d36e19f`.
The committed repair diff reproduces SHA-256
`d93094f2369bfef971822fe7401b3187c555a7e5c58a43ce7306218c011c9250`.

Accepted fixture/schema source content is unchanged. Only CRLF-to-LF byte
replacement is permitted before hashing; residual standalone CR and true content
changes still fail closed. Retained independent repair proof covers LF and CRLF
exports, each **46/46 focused**, **138/138 full Brain** and CLI help PASS, plus
**80 negative subcases** across five inputs, eight mutations and two timing
boundaries. This does not weaken provenance or authorize live work.

## Current local delivery candidate and checks — 2026-10-03

Before this documentation reconciliation, the existing local integration
candidate is `2290e417530cafbd5b05948098a3d954ae018fa0`, tree
`22d98c8e9a0b7ef56476e6f7fc891f74ce9d406c`. It conflict-freely merges original
documentation candidate `5388edfdf73448c1be4be2267bb7d44a504e1844` with the
accepted repair. Original B/D and repair commit identities are preserved in
ancestry. All 23 B source pins and the successor inventory remain unchanged;
the three current D pins equal the accepted repair. Its product tree equals
the repair's product tree; differences are confined to the three delivery docs.

The orchestrator reports these actual local composition checks on that candidate:

| Check | Result |
|---|---|
| Core tests | **362/362 PASS** |
| Brain tests | **138/138 PASS** |
| Core artifact smoke | **32/32 PASS** |
| Brain artifact smoke | **31/31 PASS** |
| Generated mock Brain artifacts | **PASS** |
| Hygiene proof/guard checks | **PASS** |
| Schema checker | **16 metaschemas / 41 fixtures PASS** |
| Diff hygiene | **PASS** |

Meta independently reverified committed source identities for this docs-only
transaction; these runtime results are the orchestrator's reported runs, not
tests rerun by Meta or remote CI. This documentation commit changes no product
bytes. Candidate/main CI and exact-main admission remain separate obligations.

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
- **R0:** Owner accepted five valid named records with bounded own contexts:
  materialized A1 and four permitted empty contexts with identity, scenario-load,
  projection and visibility evidence. Local R0 proof includes **153/153** filtered
  Core tests and five verified bounded contexts; it does not mean five actively
  deciding actors. Status is **R0_LOCAL_PASS / EXACT_MAIN_ADMISSION_PENDING**.
  This summary imports no raw assessment and does not mark a mandatory output
  complete or replace later exact-main evaluation.
- **Wave:** all five unchecked mandatory outputs and five holds remain open for
  assessment; Step 8 is **NOT_READY/unopened**. No Wave 4 gate verdict, output
  completion, Wave 5 dispatch or dependent prerequisite waiver is inferred from
  product acceptance, this documentation commit or eventual publication.

## Integration-owner handoff and Meta stop

The existing orchestrator resumes the authorized Git delivery sequence: freshly
verify canonical remote/source/base and the complete publication diff, preserve
accepted B/D/repair ancestry, push the authorized source, create or resume its PR,
verify required candidate/composition checks, merge with a merge commit, then
verify exact `main` commit/tree and required target checks. Required jobs are
Runtime CI `core-dotnet-ci` and `brain-python-ci`, and Contract CI `schema-check`
(display name **Schema checker**). Missing/skipped/pending checks are not PASS;
earlier base CI is not current-candidate or exact-target evidence.
The last supplied verified `main` is
`cdce3bda4a75445e423c8ddd09b7f910b2afdaee`; the orchestrator must reverify it
before the actual send. After exact-main delivery, Meta's separately authorized
final R0/state assessment returns to the original execution context. Neither
this note nor local composition proof performs that assessment.

Meta stops after the authorized three-path documentation commit and reports its
hash/tree/path scope. Publication remains **AUTHORIZED / PENDING** until the
integration owner supplies actual remote evidence. No release fragment is
required by the inspected RingFall repository policy for this docs-only change.
