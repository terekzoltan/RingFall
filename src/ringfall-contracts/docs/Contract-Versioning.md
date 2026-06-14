# Contract Versioning

W1-S1 creates the versioning policy note only. It does not add JSON Schema files.

The initial version for future artifact schemas is `0.1`.

The `schema_version` requirement applies to future artifact schemas only. When schema files are introduced in later gated Wave 1 steps, each external artifact schema must require or carry `schema_version` according to that schema's design review.

Future version changes must be intentional, reviewed, and documented with migration notes when artifact compatibility changes.
