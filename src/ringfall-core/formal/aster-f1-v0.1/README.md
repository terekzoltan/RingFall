# Aster F1/0.1 — bounded local formal-evidence supplier

This directory is the independently reviewable Wave 4 Step 6 P2 candidate.
It consumes the locally accepted, **uncommitted** P1 Core evidence interface
(nine-path aggregate `6bf530a8e1efda55703de8563b0435f1487baaa0fe8b0697c759aefca8f85fbc`).
Only Core's existing Headless `aster-f1-evidence` command validates and executes
the candidate; `map_p1.py` reads its bounded result and does not recreate Core
authority. P2 does not authorize mutation, deliver A4-J's hard gate, certify JSON
Schema validity, or claim natural-language or whole-world truth.

## Local invocation

With the accepted Core solution built and the Docker **Linux/amd64** engine
running, invoke `python run_refinery.py --state STATE.json --candidate REQUEST.json
--pulse PULSE.json --context CONTEXT.json` from this directory. The four paths
are explicit P1 inputs. The runner verifies their hashes against the fresh P1
stdout, checks all nine P1 source bytes, and uses the exact local image
`ghcr.io/graphs4value/refinery-cli@sha256:5d7eacdef0ddfb98e264cad405badf96753c68203e498a12d91331a0aa96ad57`.
`0.3.0` denotes the declared release, **not** a required local tag alias.
Docker inspection and both commands use the digest directly; `--pull=never`,
`--network none`, Linux/amd64, bounded CPU/memory/process/time/output and a
read-only Windows temporary-directory bind mount. `instance.problem` is created
there and never added to the candidate. A report contains actual exit codes,
exact argv and bounded raw-output SHA-256 hashes. No solver output is mocked.
All four relative input paths resolve against the caller's invocation directory
before reading or passing them to the repository-root Headless child; absolute
paths retain their meaning. The **combined** stdout/stderr retention limit is
65,536 bytes while a child runs, not a post-exit check. Overflow and timeout
terminate the owned child, close/drain its pipes and fail closed. Interrupted
Docker invocations use a unique `ringfall-p2-` container name and bounded
cleanup of only that name; incomplete cleanup is blocking fallback.

Run `python -B -m unittest discover -s . -p "test*.py" -v` for the real local
fixture matrix. These Docker tests are local evidence; existing Core CI does not
execute them. There is no added CI, package installation or network provider.

## Closed model and comparison

`aster-f1-v0.1.problem` contains finite graph types and named `error` predicates.
For each P1 record, the mapper emits **exactly five nodes**: actor, request,
sector and either tool+action or crew+location. It disables every class's
automatic `::new` node, sets exact type/node scopes, defaults every relevant
relation to false, then adds only known fixed-symbol P1 edges. Boolean facts
are asserted explicitly. Missing required tool arguments are represented by
a bounded flag; absent optional arguments are not invented as positive facts.
Both `check -k` and `generate -o -` use the same SHA-256-pinned input. A positive
witness must declare exactly those five nodes, exactly the expected type facts,
all class-new exclusions, relation closures and only the mapped positive edges.
Unknown syntax or output is fallback; `check` exit 1 alone is **never** invalid.

| Named predicate | Exact Core issue | Status | P1 mapped fields |
|---|---|---|---|
| `toolUnavailable` | `tool_unavailable` | denied | referenced resolved tool, `status=unavailable` |
| `crewUnavailable` | `crew_unavailable` | denied | referenced resolved crew, `crew_status=unavailable` |
| `toolExecuteDenied` | `tool_execute_denied` | denied | known action, `mode=execute` |
| `requiresDryRunConflict` | `tool_requires_dry_run_conflict` | denied | dry-run-required action, `requires_dry_run=false` |
| `toolArgumentsMissing` | `tool_arguments_missing` | denied | one required known argument row absent |
| `loadFractionNonPositive` | `tool_argument_value_invalid` | invalid | finite reroute fraction at or below zero |
| `loadFractionTooHigh` | `tool_macro_surface_denied` | denied | finite reroute fraction above `0.20` |

Every modeled field needs P1 completeness and guarded coverage. Tool query,
bounded reroute and bounded WorkOrder positives require Core `allowed` with no
issues and a real consistent check **and** closed generated witness; execution
may still be `deferred`. Model errors require the **complete** matching Core
issue set and its status. Unreferenced targets, unknown fields, unmapped Core
issues, schema-only claims, hidden metrics, current location inferred from
home sector, reachability and broader authority are unsupported—not solver
success. A real consistent check conflicting with Core, or a real named error
conflicting with Core, yields blocking `fallback/disagreed`.

The observed 0.3.0 named-error control reports `Inconsistencies found in
model:` followed by lines such as `toolUnavailable(r1): error.` on stdout and
exit 1. Malformed syntax also exits 1, with `InvalidProblemException` on
stderr; the parser accepts only the exact named-error format and a known
predicate set. `schema_validation=not_run`, P1 `formal_proof=not_run`, and
`mutation_authorized=false` remain explicit. There is no repairable verdict
without a real repair path.
