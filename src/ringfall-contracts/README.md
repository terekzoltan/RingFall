# Ringfall Contracts

`src/ringfall-contracts/` is the canonical home for Ringfall external artifact contracts.

W1-S1 creates layout and versioning notes only. It does not define packet fields, artifact shapes, schema bodies, examples, validation tooling, runtime behavior, provider behavior, Unity behavior, or simulation logic.

JSON Schema is canonical for future external artifact validation. W1-S1 adds no schema definitions; those arrive through later gated Wave 1 steps.

Future execution-impacting model outputs must become strict typed packets before they can affect the simulation. Contracts must not allow direct LLM world-state mutation.

## Next Steps Remain Blocked

C1-C/C1-D packet schema work requires a separate Meta gate. Track C/D/E/A are not activated by this layout step.
