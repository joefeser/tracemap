# Access Form/Report Metadata Design

## Pipeline

```text
authorized disposable copy (Windows)
  -> invisible force-disabled Access automation
  -> bounded SaveAsText form/report serialization
  -> protected source-neutral v1 bundle
  -> pure Mac-capable parser/projector
  -> hash-safe facts and binding paths
  -> optional independent hidden-local identity projection
```

## Metadata model

The existing `AccessRawUiSurface`, `AccessRawControl`, UI parser, projector,
source-neutral composer, and screen-to-data traversal remain the composition
spine. Their allowlists are extended with lookup, nested-surface, and
group/sort metadata. All protected values are hashed unless they resolve to an
existing stable object/field identity.

Saved-query projection gains bounded query-output declarations and
output-to-source-field candidates derived from already-read QueryDef SQL.
Parsing masks literals/comments, accepts only a simple SELECT list, and never
executes or persists SQL. Query output stable keys give a query-bound control a
declared field node, while the candidate edge to a table field remains Tier 3.

## Producer boundary

The producer is a standalone PowerShell script. It:

- requires explicit source copy, original, output, repository/commit/base-scan
  hashes, database identity, and canary paths;
- snapshots original/copy SHA-256 before automation;
- forces `AutomationSecurity = 3` and `Visible = false`;
- records loaded form/report counts and visible window count before and after
  each serialization;
- writes raw text only beneath a per-run scratch directory;
- constructs deterministic manifest/NDJSON records after canonical sorting;
- verifies original/copy hashes and canaries, quits Access, and deletes scratch
  in every exit path.

An internally loaded surface that is not left loaded is represented by
categorical producer provenance. Any changed loaded state, visible UI, canary,
or mutation is a hard failure.

## Hidden-local projection

The optional projection is generated directly from a validated bundle and its
matched immutable base scan. It contains direct owner-local identities and
direct identifier bindings, never raw design text, inline SQL, expressions,
VBA, macro bodies, paths, or credentials. A manifest inventories every file by
relative path, size, and SHA-256 and declares claim level `hidden`.

## Deferred

Layout/format reconstruction and all runtime/code behavior remain separate
future work.
