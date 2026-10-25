# Design

Use strict authored `sqlserver-declarations/1` JSON, not a SQL parser or DDL-derived
origin. No SQL parser is currently a Core dependency; implementing only this explicitly
synthetic declaration origin avoids inventing a SQL interpreter. Native producer emits
`sqlserver-schema/1`; admission rederives the export from the separately supplied expected
public declarations and current Core assembly bytes. Unknown fields, duplicate JSON keys,
null/missing/malformed required fields and unsupported origins/versions fail admission.
Locations are author-supplied synthetic declaration spans, not extracted SQL spans.

Bindings use exact retained native fact IDs, public source-evidence projection SHA-256, exact two-part command spelling and
four-part logical identity, with repo/revision envelope. Schema export carries the same
repo/revision. No connection identity or DNN substitutions are inferred. Self-consistent
hashes are integrity, not authentication. Only public/synthetic inputs are supported.

Add an independent `sqlServerParameterState` property to semantic database operation
facts; keep the legacy aggregate witness and closed path-receipt projection unchanged.
Share one Roslyn IOperation extractor between C# and VB, bump both extractor versions.
Require fresh signed SqlCommand local, constant Add(name, SqlDbType[,size]), optional
explicit Direction assignment and explicit CommandType. Reject branch/loop/try/invalid
operation/alias/helper/mutation boundaries. Bound whole method to 20,000 operations,
parameters to 128. Default Direction=Input is supported for these exact Add overloads.

Compare in a separate native report seam. Source observations retain their original
extractor and tier; conclusions are Tier2 structural comparisons or Tier4 unknown,
never promoted compiler/schema/deployment proof. Report always partial; source scan and
path gaps/truncation remain separately owned. ReturnValue stays in call evidence but
is excluded from procedure parameter joins. SQL OUTPUT admits InputOutput only;
output-only call against OUTPUT has unknown input behavior. Defaults omitted remain
unknown. Exact ordinal identifiers only; case equivalence/aliases/precision/scale/
collation are unsupported. nvarchar uses UTF-16 code units, MAX=-1; ANSI provider length/encoding policy
versus schema bytes is unsupported. Size differences are declaration differences only.
