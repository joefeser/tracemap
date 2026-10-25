# Design

Use pinned Microsoft.SqlServer.TransactSql.ScriptDom 170.191.0 (MIT, net8.0,
compatible with net10.0); TSql160Parser with quoted identifiers enabled.
The package nuspec identifies upstream 0a5e0d21aa9136c8cbe90e484b76fdd0fcc1eac9.
Official license: https://github.com/microsoft/SqlScriptDOM/blob/main/LICENSE
Package: https://www.nuget.org/packages/Microsoft.SqlServer.TransactSql.ScriptDom/170.191.0
Existing Core uses pinned PackageReference versions; no SQL AST dependency or
additional dependency policy found. Do not add DacFx, connectivity or execution.

Separate manifest/export/admission/CLI route. Manifest lists public synthetic
file paths and exact UTF-8 SHA-256; text is read separately, never embedded in
exports. Export carries a DDL-origin declaration projection, parser version and
binary hash, source file hashes and explicit unsupported facets. Admission
regenerates from expected manifest and actual files and checks complete equality.
DDL comparison calls the existing comparison engine only after DDL admission;
the old public JSON admission path continues to refuse DDL-origin projections.
Use a versioned DDL comparison wrapper for unsupported-facet evidence and exact
provenance, and retain the existing row machinery. Markdown explicitly labels
origin and retains limitations. Reuse #910 publication helpers unchanged.

All top-level statements must be supported procedure forms. Entire input rejects
on errors, SQLCMD references/directives, duplicate identities (including case-only
ambiguity), implicit schema, unsupported type/declaration option, missing body,
numbered/CLR/encrypted procedures or bounds. Bodies are parsed for syntax only;
never traversed for declarations/results/runtime meaning. Defaults and decimal
facets produce explicit unknown evidence; no expression bytes are retained.
Identifiers after AST unquoting retain the existing ASCII bounded vocabulary.
Quoted identifiers containing spaces/punctuation remain rejected, not normalized.
Type names must use the canonical spelling of the six supported types (case and
delimiters tolerated); ANSI synonyms that ScriptDom folds together reject.
One leading UTF-8 BOM is hashed but excluded from lexing. Lexing and parsing run
on a dedicated 64 MiB-stack thread so recursion the token budget does not cap
cannot abort the host process.
