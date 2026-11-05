# Select offline SQL definitions from an installation template

Use this opt-in local-only input with the existing `sqlserver-definitions
verify|collect` command or workspace V3 `sqlEvidence`. It selects complete
batches from larger originals and substitutes explicitly declared DNN-style
owner/qualifier placeholders. Originals remain read-only. SQL is never executed.

This is definition evidence for a chosen review scenario, not an installed
schema, script execution model or automatic application-to-procedure binding.
All selected procedure result shapes remain unknown because omitted statements,
session settings and migrations are not interpreted. Declared table columns
refer only to the selected CREATE TABLE statement.

## Manifest

```json
{
  "version": "sqlserver-local-definition-selection-input/1",
  "visibility": "local-only",
  "contextRepository": "dnn",
  "contextCommitSha": "33b8fbe440ab665b8423599789db1f34d2a500fc",
  "definitionSet": "dnn-review-selection",
  "server": "DnnReviewServer",
  "database": "DnnReviewDatabase",
  "files": [
    {
      "path": "DotNetNuke.Schema.SqlDataProvider",
      "sha256": "e487db02868ca7eb551f12aaf9b561cde78151e8a1131a2b5bbb782fa22e6f46"
    }
  ],
  "selections": [
    {
      "sourcePath": "DotNetNuke.Schema.SqlDataProvider",
      "startLine": 7127,
      "endLine": 7138,
      "kind": "table",
      "name": "RoleGroups"
    },
    {
      "sourcePath": "DotNetNuke.Schema.SqlDataProvider",
      "startLine": 19151,
      "endLine": 19158,
      "kind": "procedure",
      "name": "GetRoleGroups"
    }
  ],
  "template": {
    "databaseOwner": "dbo",
    "objectQualifier": "DnnTrial_",
    "valuesOrigin": "review-scenario"
  }
}
```

These exact ranges/hash apply only to the pinned DNN file. The server/database
labels provide local context; they do not name a verified database. The application
repository/commit remains context, not proof of a deployed SQL export revision.
For other input, inspect and declare its actual paths, hashes, ranges and names.
Generated manifests can include the existing `provenance` generator/input hashes.

`sourceRoot` points to the folder holding the original install file. It may have
a non-`.sql` extension under this separate contract. The original v1 local and
public-synthetic contracts retain their `.sql` and parser input limits.

## Explicit settings and boundaries

- `databaseOwner` is a bare ASCII identifier such as `dbo`, without a trailing
  dot or brackets. Substitution emits a quoted owner and dot: `[dbo].`.
- `objectQualifier` is an ASCII identifier prefix up to 64 characters, or an
  **explicit empty string**. Missing/null is refused. No default is inferred.
- `valuesOrigin` is `review-scenario` or `owner-supplied`. Both are declarations,
  not authentication or a deployment attestation. The example intentionally uses
  hypothetical review values and produces `dbo.DnnTrial_RoleGroups` and
  `dbo.DnnTrial_GetRoleGroups`.
- Only exact `{databaseOwner}` and `{objectQualifier}` markers are substituted.
  Markers in comments, string literals or ambiguous double-quoted tokens are
  refused. The owner marker inside a quoted identifier is refused; qualifier
  prefixes inside bracketed identifiers are supported. Unsafe settings, unresolved
  identifier-like brace markers and selected SQLCMD text are refused.
- UTF-8 originals must use LF or CRLF line endings; lone carriage returns are refused.
  Line ranges are inclusive. Each starts at the beginning of the file or directly
  after a standalone `GO`, and ends at a standalone `GO` or end of file. The
  original is lexed with the pinned ScriptDom lexer so `GO` inside multiline
  strings/comments is not accepted as a boundary. Repeated batches are unsupported.
- Each selected range must contain exactly one declared table/procedure/view/
  function with the expected kind, owner and qualifier-plus-name. Fragment cuts,
  overlapping ranges, extra definitions and top-level execution/data are refused.
  No selection is silently widened and no install statement is silently discarded
  from an admitted range.

## Limits and retained evidence

Original inputs: at most eight files, 4 MiB/file, 16 MiB total, 200,000 lines/file,
1,000,000 lexer tokens/file, and a pre-lexing nested-comment depth ceiling of 128.
Quoted tokens are respected by the depth guard so comment-like literal/identifier
text does not count as nesting. Only their selected batches are parsed. Selected
inputs retain the existing 32-file, 256 KiB/file, 1 MiB total, token and complexity
limits, including after expansion. Lexing/parsing use the existing bounded parser
thread. Every listed original must contribute a selection.

The selected output uses `sqlserver-local-selected-definitions/1`. Its `selection`
record retains the complete original manifest, settings, original file hashes,
projected hashes, replacement counts, exact generator/parser identities and line
range mapping. The export's bounded-input digest binds the original manifest.
`input.files` inside this export describes virtual parser inputs; those files
are not written. Always reuse the original selection manifest for verification.

Object, parameter, table-column, reference and gap line spans map back to the
original file. Names, fingerprints and settings stay local-only. SQL bodies,
literal/default values and raw projected text are not written into JSON, SQLite
or reports. There is no public privacy projection.

Workspace V3 uses the same `sqlEvidence: { repositoryId, manifestPath, sourceRoot }`
section as the [SQL getting-started guide](WORKSPACE_SQL_GETTING_STARTED.md).
Verification and collection regenerate projections from exact originals; drift
outside a selected range also refuses admission. Run records hash actual original
files, and the SQL sidecar stays separate from `combined.sqlite`. Input/output
rechecks precede completion. Existing runs are preserved.

For the real public example, see the [DNN workspace trial](validation/DNN_WORKSPACE_TRIAL.md).
A matching compiled publication and source-call bindings remain separate work.
