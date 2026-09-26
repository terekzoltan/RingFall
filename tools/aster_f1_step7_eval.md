# Wave 4 Step 7 — local Track E Aster evidence

Run from the **execution** worktree `C:/EGYETEM/FUNSTUFF/RingFall-a4ij-evidence`.
This is dev/mock proposal smoke plus candidate-bound local F1 differential
evidence, not a canonical run, publication, Wave 4 closeout, or mutation grant.
Keep the separately dirty session home and all P1/P2 and Meta-owned files intact.

Prerequisites: accepted A4-F/G/H base `cc067ece4f3b699abbec8f7293daabd75a4ec545`;
locally accepted P1 nine-path aggregate `6bf530a8e1efda55703de8563b0435f1487baaa0fe8b0697c759aefca8f85fbc`;
P2 six-path aggregate `652f8abfe1bb140af7dd76ad9660024c964d6e77dfae879207b6fd690815dd45`.
The evaluator checks both source aggregates before any differential case.
Install only the project's existing dev requirements, build the accepted Core
solution, and have the already approved digest-pinned Refinery Linux/amd64 image
and matching Docker engine available. P2 uses `--pull=never` and `--network none`.
No network install, CI workflow change or new provider is part of this candidate.

First finish **all four source paths** (`tools/aster_f1_step7_eval.py`,
`tools/aster_f1_step7_eval_tests.py`, `tools/fixtures/aster-f1-step7/cases.json`,
and this `.md`), run the focused tests, and freeze their individual SHA-256
values and ordered `path=sha256` aggregate **before** any fresh real-solver run.
The evaluator requires that aggregate as an argument. For example, in PowerShell
from the execution worktree:

```powershell
python -B tools/aster_f1_step7_eval_tests.py -v
$aggregate = python -B -c "import hashlib,pathlib; root=pathlib.Path.cwd(); names=('tools/aster_f1_step7_eval.py','tools/aster_f1_step7_eval_tests.py','tools/fixtures/aster-f1-step7/cases.json','tools/aster_f1_step7_eval.md'); digest=lambda raw:hashlib.sha256(raw).hexdigest(); print(digest(chr(10).join(name+'='+digest((root/name).read_bytes()) for name in names).encode()))"
python -B tools/aster_f1_step7_eval.py --expected-aggregate $aggregate
dotnet test src/ringfall-core/Ringfall.Core.sln --no-restore
$env:PYTHONPATH="src/ringfall-brain"
python -B -m unittest discover -s src/ringfall-brain/tests -p "test*.py"
python -B tools/brain_artifact_smoke_tests.py
python -B tools/core_artifact_smoke_tests.py
python -B tools/schema_check.py
```

The evaluator calls the actual A4-H six-file producer, validates all six
versioned JSON schemas and both proposal/trace/cost chains, and creates case
inputs in isolated temporary storage. It asks Headless P1 for a separate fresh
same-input Core record after P2 obtains fresh Core facts and executes the real
Refinery CLI. P2 captures real check/generate output. Track E verifies the raw
receipt hashes, fixed-symbol instance and closed positive witness or the complete
named-error/Core-issue set, together with independent expected case outcomes.
P1 supplies **hashes of Core trace/result/diff links**, not the raw objects or
an independent JSON Schema check of those objects. Core alone validates and
executes; the solver never authorizes mutation.

The evaluator also pins each of the eleven fixture names to its exact recipe,
stage and expected result before generating artifacts. `cases.json` contains
those explicit inputs and expectations. A passing negative control
reports `hard_pass: false`; only the two modeled positives may report true for
their **scoped candidates**. A schema-invalid case is blocked before a solver
run. An unsupported case may have no solver run and cannot become a formal
pass. A malformed model response, missing capture, fallback, disagreement or
unbound input fails the evaluator rather than turning into a domain verdict.
There is no implemented repair path; repairable is not applicable.
For cases stopped before Core or the solver, the corresponding coverage entry
is `null` (not run), never a fabricated validator/formal coverage class.

The output JSON is **local** per-case evidence, including SHA-256 of temporary
inputs, six A4-H artifacts, a fresh P1 response, model/instance, P2 report and
real captured solver outputs. The evaluator retains actual bounded P2 check-error
and positive-witness stdout/stderr bytes plus `report.json` and `manifest.json`
in one uniquely named private directory under
`C:/Users/ASUS/AppData/Local/Temp/opencode/`. Its successful stderr receipt
identifies that directory and the saved report/manifest hashes; provide this
path to Meta for independent retrieval. Use
`python -B tools/aster_f1_step7_eval.py --verify-private "C:/Users/ASUS/AppData/Local/Temp/opencode/ringfall-step7-..."`
to recheck the saved file set, bytes and hashes against the **current** frozen
four-path source aggregate. Do not place private generated evidence in the repo.

The private manifest links case/input and fresh Core hashes, model/instance and
digest-pinned image identity, each invocation-specific P2 receipt, capture
lengths/hashes, the **already frozen** source aggregate and the saved report's
whole-file hash. The report does not hash the manifest; neither run-specific
hash belongs in source files. Docker-generated invocation names may change the
whole-report hash without changing a solver result, so report and individual P2
receipts must be distinguished. Missing, truncated, altered or unreadable
required capture **or report** blocks successful handoff and `hard_pass` for
the run. If any of the four source paths changes after freezing, discard that
run's proof, freeze again and regenerate the genuine solver/Core matrix before
handoff. Preserve the private files until `FIX_RECHECK`; if Meta cannot read
them, do not claim a hard pass.
The five coverage labels have different meanings:

| Label | Permitted claim |
|---|---|
| `schema_only` | Shape/version check under the existing JSON schemas. |
| `guarded_by_core_validator` | Fresh P1/Core authority and bounded execution/effect link checks. |
| `proved_by_refinery` | Named bounded predicate checked by real Refinery output and, for positives, closed witness. |
| `observability_only` | Cognition trace, cost and dev/mock metadata; recording is not Core/formal proof. Graph-link integrity is still a blocking evaluator check. |
| `unsupported` | Unmodeled facts/surfaces; never solver-valid or an authorization. |

The report deliberately says `gate_claim: local_scoped_evidence_only`. Existing
remote CI does not run this evaluator or Docker. With no OSL-W4-B artifact admitted,
exact-integrated-SHA remote CI is not required for this **local** candidate-bound
review and ACK. If that artifact enters fan-in, its Combined-plan exact-SHA CI
gate applies before its acceptance; if another approved gate requires it, stop
without publishing. Final acceptance belongs to Meta's candidate-bound review.
