# Contract Versioning

W1-S1 created the versioning policy note only. W1-S2 introduces the first packet schema drafts.

The initial version for Ringfall artifact schemas is `0.1`.

Every external artifact schema introduced from W1-S2 onward must require or carry `schema_version`. W1-S2 packet schemas use `schema_version.const = "0.1"`.

Future version changes must be intentional, reviewed, and documented with migration notes when artifact compatibility changes.
