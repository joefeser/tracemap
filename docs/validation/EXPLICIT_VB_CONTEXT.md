# Explicit projectless VB compilation context

`tracemap scan --repo <repo> --out <new-output> --vb-compilation-context <json>`
opts into an in-memory Roslyn compilation without creating a project, restoring
packages, discovering references, or executing source/assemblies. Default scans
are unchanged. `vb-semantic/0.10.0` labels every resulting semantic fact with
`compilationContextKind=explicit-projectless-inputs`, `compilationContextSha256`
and `originalBuildCorrespondence=not-established`. Tier1 means compiler binding
under these supplied inputs, not equivalence to the original website build.

The operator-authored local input has exactly these fields (no duplicate or
unknown fields); placeholder hashes below must be replaced with actual SHA-256:

```json
{
  "schemaVersion": "vb-compilation-context.v1",
  "commitSha": "full repository HEAD",
  "rootNamespace": "",
  "optionStrict": true,
  "optionInfer": true,
  "optionExplicit": true,
  "imports": ["System"],
  "sources": [{"path": "App_Code/Worker.vb", "sha256": "lowercase SHA-256"}],
  "references": [{"path": "absolute path to an authorized reference DLL", "sha256": "lowercase SHA-256"}]
}
```

Sources must be inventoried repository-local VB files. References are explicit
absolute paths with exact hashes, loaded from captured bytes, never implicitly
from the host runtime or a directory glob. Duplicate simple assembly names are
refused, including two versions. Framework references must be supplied too.
Root namespace and imports are explicit; parse language is the scanner's bundled
Roslyn default, output is a library, comparison is binary, and no preprocessor
symbols are injected. Contexts requiring other compiler options are not supported.
This is not an ASP.NET build emulator: generated page/designer code, assembly
partitioning, mixed-language compilation, custom providers, and runtime deployment
equivalence remain outside this slice. Compile each supplied VB source set as one
assembly; do not claim this models a website's actual dynamic assembly partition.

Limits: 1 MiB context, 512 sources, 8 MiB/source and 64 MiB combined; 256 references,
64 MiB/reference and 512 MiB combined; 128 imports of at most 512 characters.
Symlink/reparse ancestors are refused. All paths/hashes remain local; the context
is **not a shareable artifact**. Any future automated context generator must carry
generator and bounded-input provenance and must not publish private paths/hashes.

Malformed, stale, missing or conflicting inputs refuse the entire context with
`ExplicitCompilationContextRefused`; syntax fallback continues. Valid inputs with
compiler errors retain individually resolved facts and sanitized diagnostic gaps;
partial compilation is not a clean build. Existing source snapshot verification
protects the captured source text. No source/binary identity reconciliation is
inferred from the synthetic compilation. Existing projects, explicit project or
solution selection, restore, and target-framework selection conflict with this
mode and are refused.

Run `ExplicitVisualBasicContextTests` plus the full focused filter in
`vb-compilation-context-matrix.yml`. Public tests exercise successful compiler
binding, namespaces/imports, partial compilation, exact hashes, missing/duplicate
references, scope and schema refusals. Private framework-reference compatibility
requires Windows acceptance with an operator-approved context. Full solution,
non-.NET suites and pinned OSS checks remain separate/deferred.
