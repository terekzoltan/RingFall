# Ringfall Contracts

`src/ringfall-contracts/` is the canonical home for Ringfall external artifact contracts.

W1-S1 created layout and versioning notes only. W1-S2 introduced the first core packet schema drafts under `schemas/packets/`. W1-S3 adds institution and council packet schema drafts for C1-E.

JSON Schema is canonical for external artifact validation. W1 packet schemas use JSON Schema Draft 2020-12 and `schema_version` `0.1`.

W1 packet schemas are draft contract surfaces. Structural validation is available now; deeper semantic authority, hidden-truth, and fixture coverage remains gated to later Wave 1 validation work.

Future execution-impacting model outputs must become strict typed packets before they can affect the simulation. Contracts must not allow direct LLM world-state mutation or authoritative truth edits.

## Next Steps Remain Blocked

C1-F and later schema work remain blocked until W1-S3/C1-E is accepted by Meta. Examples, validation tooling, C#/.NET runtime code, Python runtime code, provider/model behavior, Unity work, scenarios, generated artifacts, and simulation logic remain out of scope.
