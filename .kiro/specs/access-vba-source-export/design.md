# Access VBA Source Export Design

```text
immutable original + hash-identical disposable copy
  -> force-disabled invisible Access automation
  -> bounded SaveAsText module export
  -> private raw source files (owner controlled)
  -> normalized protected design bundle
  -> AccessDesignEvidenceComposer + AccessVbaProjector
  -> hash-safe static facts and explicit gaps
```

The exporter is separate from `AccessComReader` and `scan-file`. It consumes a
compatible form/report metadata bundle, preserving its event properties and
adding VBA modules from the same base-scan identity. Its output has sibling
`private-access-source/` and `normalized-design-evidence/` directories. The
private directory retains generic-name raw module files and the already exported
full form/report definition text, with hashes and roles but no object names in
its manifest. Only the
latter is accepted by `enrich-design`; the strict reader input rule prevents
raw files from entering normal input.

`SaveAsText(acModule, name, path)` is selected because the repo already uses
the same non-invoking export mechanism for form/report metadata. Module catalog
identity reads remain a new guarded capability, validated by count canaries,
invisibility, source hashes, timeout, and process cleanup.

The original and supplied disposable file are integrity gates: either changing
causes output removal and a typed source/copy failure. Before Access opens a
database, the exporter creates an inner scratch copy, removes only `StartupForm`
through DAO, and opens that inner copy. This prevents the startup sentinel from
being invoked without opening or rendering a surface. The bundle records the
initial/final hashes and typed outcome for both supplied and inner copies;
only the inner copy is expected to receive Access bookkeeping and it is removed
before output succeeds. Filesystem read-only mode is not enabled without a
separately validated Access compatibility decision.

The composer maps only `[Event Procedure]` or exact `=Identifier()` to a
candidate procedure in `Form_<surface>` or `Report_<surface>`. Arguments,
qualifiers, expression composition, or unresolved targets remain gaps.
Layout parsing remains explicitly deferred: retained definitions make a future
private parser possible without reopening the database.

For lifecycle analysis, the same retained definition is parsed only at the
`CodeBehindForm`/`CodeBehindReport` boundary. Its suffix becomes a protected
form/report `vba-module` with exact code hash and module-relative lines; the
record carries originating definition hash and first code line. A matching
AllModules export is ignored when this class-module record exists, preventing
duplicate procedures. Missing, blank, or procedure-unparseable code-behind
sections remain explicit gaps.
