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

The composer maps only `[Event Procedure]` or exact `=Identifier()` to a
candidate procedure in `Form_<surface>` or `Report_<surface>`. Arguments,
qualifiers, expression composition, or unresolved targets remain gaps.
Layout parsing remains explicitly deferred: retained definitions make a future
private parser possible without reopening the database.
