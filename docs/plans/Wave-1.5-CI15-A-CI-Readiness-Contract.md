# Wave 1.5 / CI15-A CI Readiness Contract

**Task:** CI15-A
**Owner:** Meta Coordinator + Track E
**Status:** complete / accepted for CI15-A only
**Date:** 2026-07-01
**Baseline planning commit:** `287f79e docs: add bounded Refinery formal gates`

## Purpose

CI15-A defines RingFall's official contract-CI scope and local verify command before Track E adds the first GitHub Actions workflow in CI15-B/CI15-C.

This is a scope-lock and readiness artifact only. It does not add CI workflow files, runtime code, provider/model behavior, C#/.NET, Python brain runtime, Unity work, scenarios, simulation logic, Refinery tooling, solver CI, eval-runner logic, coverage thresholds, or artifact upload behavior.

## Official Local Verify Contract

The local command sequence for the Wave 1 contract layer is:

```bash
python -m pip install -r requirements-dev.txt
python tools/schema_check.py
```

The first command installs the dev-only JSON Schema validator dependency declared by `requirements-dev.txt`. The second command validates the accepted Wave 1 contract surface:

- Draft 2020-12 metaschema validity for all committed schema files under `src/ringfall-contracts/schemas/`;
- manifest consistency for `src/ringfall-contracts/examples/manifest.json`;
- valid example fixtures;
- invalid example fixtures;
- static semantic reason isolation implemented by `tools/schema_check.py`.

## CI15-A Scope Contract

The first contract CI lane must protect only the current Wave 1 contract checker:

```text
python tools/schema_check.py
```

CI15-B/CI15-C may implement the first workflow around that command after this CI15-A closeout.

The workflow must stay narrow until later gates explicitly open more surfaces.

## Explicit Non-Scope

CI15-A does not approve:

- `.github/workflows/contract-ci.yml` or any other workflow file; that is CI15-B/CI15-C;
- C#/.NET core build or tests;
- Python brain runtime, OpenRouter calls, provider credentials, or model execution;
- Unity project build or editor automation;
- scenario packs, replay runners, eval runners, generated run artifacts, or simulation logic;
- Refinery/formal intervention tooling, bridge code, solver installation, or formal gate CI;
- hard coverage thresholds;
- upload of private/local state such as `.fal`, `.opencode`, `.swarm`, `kontext`, `data/runs`, local configs, or private design canon exports.

## CI Hygiene Boundary For Next Step

CI15-B/CI15-C should treat green contract CI as mechanical evidence only.

It must not be represented as:

- domain correctness approval;
- FAL approval;
- runtime authority validation;
- hidden-truth leak proof;
- provider/model integration proof;
- formal Refinery proof;
- Wave 2 runtime readiness by itself.

## Acceptance Evidence

CI15-A is accepted when the planning docs record:

| Acceptance point | Evidence | Status |
|---|---|---|
| Official local verify command is defined | This document records `python -m pip install -r requirements-dev.txt` then `python tools/schema_check.py`. | PASS |
| CI scope is contract-only | This document limits the first CI lane to `tools/schema_check.py`. | PASS |
| Runtime/provider/Unity/scenario/simulation scope remains closed | Explicit non-scope section preserves all hold boundaries. | PASS |
| Refinery/formal solver CI remains closed | Explicit non-scope keeps formal intervention CI inactive until a named family, fixtures, bridge, and differential harness exist. | PASS |
| Handoff to Track E is unblocked | CI15-B/CI15-C may implement the first workflow using this contract. | PASS |

Latest local verification evidence at CI15-A closeout:

```text
python -m pip install -r requirements-dev.txt
Requirement already satisfied: jsonschema<5,>=4.18

python tools/schema_check.py
schemas: 16 Draft 2020-12 schema files passed metaschema checks
manifest: 41 entries passed consistency checks
fixtures: 41 selected entries validated successfully
```

## Next Step

Track E may proceed to CI15-B/CI15-C: add the first reviewed contract CI workflow and hygiene/leak guard around the official local verify contract. The workflow must not open future runtime, provider, Unity, scenario, simulation, coverage-hardening, artifact-upload, or formal-solver lanes.
