# Contract Versioning

W1-S1 created the versioning policy note only. W1-S2 introduced the first packet schema drafts; W1-S3 extends the packet family with institution and council schema drafts.

The initial version for Ringfall artifact schemas is `0.1`.

Every external artifact schema introduced from W1-S2 onward must require or carry `schema_version`. W1 packet schemas use `schema_version.const = "0.1"` until an intentional reviewed migration changes compatibility.

Future version changes must be intentional, reviewed, and documented with migration notes when artifact compatibility changes.
