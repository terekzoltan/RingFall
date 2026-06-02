# RingFall

Public-safe repository skeleton for RingFall.

Current phase: Wave 0 repository bootstrap. The detailed design canon, world bible, implementation plan, and private coordination notes are local/private until an explicit export review approves public release material.

## Repository Layout

| Path | Purpose |
|---|---|
| `docs/` | Public-safe placeholders; private canon is ignored until export review. |
| `src/` | Future source code. Empty in this skeleton. |
| `client/` | Future client code. Empty in this skeleton. |
| `configs/` | Example configs only; no local secrets. |
| `scenarios/` | Future scenario packs. Empty in this skeleton. |
| `data/` | Local/generated run artifacts; ignored except `.gitkeep`. |
| `tests/` | Future tests. Empty in this skeleton. |
| `tools/` | Future developer utilities. Empty in this skeleton. |
| `infra/` | Optional deployment/remote-run material later. Empty in this skeleton. |

## Current Non-Goals

- no simulation implementation yet
- no C#/.NET solution yet
- no Python brain service yet
- no Unity project yet
- no OpenRouter/model execution yet
- no public release quality claim
- no private design canon in public history

## Source Of Truth

The detailed source-of-truth documents are private/local for now and are excluded by `.gitignore`. Public documentation should be added only after a deliberate sanitization/export review.

## Privacy And Local State

- `.fal/` is local/private FAL coordination state and is ignored.
- `.opencode/`, `.swarm/`, and `kontext/` are local tool/session state and are ignored.
- `data/runs/`, traces, replays, eval outputs, local configs, and secrets are ignored by default.
- `ringfall-canonical-docs-clean.zip` is a local source archive; live canon is markdown under `docs/`.
- `docs/**/*Ringfall-*.md` and the private Combined plan are ignored until public export review.
