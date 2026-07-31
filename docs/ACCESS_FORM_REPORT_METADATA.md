# Microsoft Access Form/Report Functional Metadata

TraceMap can ingest protected form/report definitions and explain declared
data bindings without reconstructing the Access layout.

The supported functional metadata is:

- form/report `RecordSource`, `Filter`, and `OrderBy`;
- control `ControlSource`, `RowSource`, `RowSourceType`, `ValidationRule`,
  `BoundColumn`, and bounded column count;
- subform/subreport `SourceObject`, `LinkMasterFields`, and
  `LinkChildFields`;
- report group/sort field or expression metadata;
- supported saved-query output fields and static source-field candidates.

Standard artifacts retain only stable identities, hashes, counts, closed
categories, rule/tier/coverage/limitation provenance, and fact-backed edges.
Raw object names and serialized definitions remain protected input.

On Windows, the producer
`scripts/access-validation/Export-AccessFormReportMetadata.ps1` uses invisible,
force-disabled Microsoft Access automation and `SaveAsText` against an
explicit disposable copy. It fails closed if loaded surface state changes,
Access becomes visible, a canary fires, either source hash changes, or scratch
cleanup fails. It never opens a recordset, executes a query, opens/renders a
form or report, invokes an event, reads VBA source, or exports a macro body.

On macOS or Windows, compose the protected bundle with an immutable completed
base scan:

```bash
dotnet run --project src/dotnet/TraceMap.Access.Cli -- enrich-design \
  --base-scan <completed-access-scan> \
  --design-evidence <protected-bundle> \
  --out <new-enriched-output>
```

For owner/developer review, direct names can be projected only through the
explicit `identity-project` command documented in `docs/VALIDATION.md`. That
output has claim level `hidden`, is independently deletable, and is outside
standard combine/vault/public/release workflows.

Visual coordinates, dimensions, colors, fonts, borders, images, captions,
themes, formatting, tab order, accessibility reconstruction, screenshots, and
OCR are deferred. The metadata does not prove rendering, event firing,
navigation, query execution, row state, runtime reachability, correctness,
completeness, production use, or release approval.
