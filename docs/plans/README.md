# RingFall Plans

This folder contains execution sequencing and implementation wave planning.

`Combined-Execution-Sequencing-Plan.md` is the active frontier and step-order source.

`Wave-1.5-CI15-A-CI-Readiness-Contract.md` records the accepted CI15-A local contract-CI verify contract and handoff to CI15-B/CI15-C.

Formal intervention gate sequencing must stay aligned with `docs/design/Formal-Intervention-Gates-Refinery.md`. The Combined Plan owns when a Refinery family becomes report-only, hard-gated, or explicitly unsupported.

## CI companion for the current Wave 4 roadmap

Planning companion approved 2026-09-20; Combined remains sequencing authority.
Existing `contract-ci.yml` and `runtime-ci.yml` own automatic contract, Core
.NET, Brain discovery and artifact smoke checks. No new workflow is activated.

| Epic / gate | Existing check and planned delta | Owner | Trigger / proof / revisit |
|---|---|---|---|
| A4-G limited execution | Core solution tests + artifact smoke already run. Add the plan's action rejection, authority and deterministic state-diff regressions to existing discovery. | Track B tests; Track E only for a needed workflow delta | Plan before implementation, candidate freeze then exact integrated SHA CI |
| A4-H cognition side | Brain discovery and mock artifact validation cover the surface. Preserve accepted regressions; no repeat review or new CI job merely for this companion. | Track D; Meta verifies accepted evidence | Integration/current baseline CI; revisit when artifact contract changes |
| A4-I/A4-J evidence gates | Plan deterministic cross-artifact/differential fixtures against accepted producer outputs; record unsupported families explicitly. | Track E, producer handoffs from B/D | Existing later gates; only propose a dedicated formal job when prerequisites and cost are proved |
| Deferred Unity / scenario-replay lanes | Existing CI15 blocked slots remain blocked; name minimal fixtures, runner and bounded cost in the enabling Epic. | Track A / Track E with Meta sequencing | Revisit at the named runtime/CI15 gate, not an automatic Wave-wide activation |

Each Epic references the applicable row in its verification plan. Existing
coverage can justify no workflow change. Review checks regression detection;
closeout records shipped/deferred checks, responsible owner and revisit point.
Local verification, remote CI on the integrated commit and Meta acceptance are
distinct. Do not add paid provider calls, coverage thresholds or solver execution
under this planning note.
