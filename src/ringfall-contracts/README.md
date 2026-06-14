# Ringfall Contracts

`src/ringfall-contracts/` is the canonical home for Ringfall external artifact contracts.

W1-S1 created layout and versioning notes only. W1-S2 introduces the first core packet schema drafts under `schemas/packets/`.

JSON Schema is canonical for external artifact validation. W1-S2 packet schemas use JSON Schema Draft 2020-12 and `schema_version` `0.1`.

Future execution-impacting model outputs must become strict typed packets before they can affect the simulation. Contracts must not allow direct LLM world-state mutation or authoritative truth edits.

## Next Steps Remain Blocked

C1-E and later schema work remain blocked until W1-S2 is accepted by Meta. Examples, validation tooling, C#/.NET runtime code, Python runtime code, provider/model behavior, Unity work, scenarios, generated artifacts, and simulation logic remain out of scope.
