# Template package format

OpenDockify template packages use media type `application/vnd.opendockify.template+json` and UTF-8 JSON.
Format version `1` contains, in order, `formatVersion`, `stableId`, `revision`, `metadata`, `body`,
`definition`, `provenance`, and `digest`. Object properties inside `definition` are recursively sorted by
ordinal name before serialization; array order is preserved. `digest` is the lowercase SHA-256 hex digest
of the canonical package without the `digest` property.

Imports are limited to 1 MiB and JSON depth 32. Administrators first submit the bytes to
`POST /api/admin/templates/packages/validate`, then submit the unchanged bytes, returned receipt, and an
explicit `create-copy`, `new-revision`, or `reject` policy to `/api/admin/templates/packages/import` within
ten minutes. Validation checks the format version, digest, definition, identity conflict, and writes no data.

`create-copy` creates a private stable identity and records source provenance. `new-revision` appends an
immutable revision to an authorized existing identity. Rollback also appends a revision; historical rows are
never edited. Export packages contain no owner identifier, authentication data, document answers, or secrets.
