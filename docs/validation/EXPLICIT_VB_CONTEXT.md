# Explicit projectless VB compilation context

`tracemap scan --repo <repo> --out <new-output> --vb-compilation-context <json>`
opts into an in-memory Roslyn compilation without creating a project, restoring
packages, discovering references, or executing source/assemblies. Default scans
are unchanged. `vb-semantic/0.10.1` labels every resulting semantic fact with
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

## Prepare inputs without hand-authoring hashes

Use Python 3.9+ and `scripts/prepare-vb-compilation-context.py`. Preparation scans
only explicitly selected directories, recursively, with a 4096-entry discovery
limit per source/reference roster. It does **not** extract an original compiler
invocation from a binlog or execute a build. Inspect existing build/configuration
evidence first; proposed values must be reviewed where that evidence is absent.

```text
python scripts/prepare-vb-compilation-context.py draft --repo <source-root> --source-dir <selected-vb-directory> --reference-dir <selected-reference-directory> --root-namespace= --option-strict off --option-infer on --option-explicit on --import System --out <local-proposal.json>
```

The flags above illustrate an explicit proposal, not recommended settings for
every application. Repeat source/reference directory and import flags as needed.
Do not point reference discovery at the scanner's build output. Source files must
stay under the repository. Review the local proposal's source roster, reference
roster, root namespace, three Boolean compiler options, imports and single-assembly
assumption. Directory-selected references still need scanner PE/identity checks.
Source membership and all settings are labeled proposed even if an operator used
build evidence to choose them; only current file bytes and HEAD are observed.

After explicit human approval of that proposal:

```text
python scripts/prepare-vb-compilation-context.py approve --proposal <local-proposal.json> --operator-approved --out <local-context.json>
```

Approval verifies generator/payload hashes, current HEAD and every captured file
hash. Both outputs use exclusive creation, must be outside the source repository,
and are local-only. The scanner accepts the resulting provenance envelope as well
as the original operator-authored raw context. Envelope hashes are integrity
metadata, not cryptographic proof of approval or original-build correspondence.

For governed review, put the context beneath the configured receipt root and set
`vbCompilationContextRelativePath` in a **new** fresh/projectless review config.
Preflight hashes the context plus every listed source/reference, checks the source
commit and declared file hashes, and charges the existing input/byte budgets.
Run forwards `--vb-compilation-context`; resume rechecks all those inputs and
refuses mutation. Attach-mode use is rejected. Do not change an existing run's
configuration or checkpoint. If the proposed roster exceeds budgets, request a
specific budget change rather than silently increasing them.

## Semantic declaration consumer regression gate

`ExplicitVisualBasicContextTests.Semantic_app_code_registration_preserves_candidate_boundary`
crosses clean/partial compilation with class, root namespace, wrong root namespace,
nested type, interface and duplicate declaration cases (12 combinations). Positive
cases retain Tier3 namespace-only registration and inline markup candidates backed
by actual Tier1 declaration facts; negative registration cases remain unresolved.
Repeated scans must preserve fact IDs. Compiler global qualifiers are normalized,
not real namespace/type identity. Partial compilation diagnostics remain visible.

Run this suite with `AppCodeRegistrationMatrixTests`, `LegacyWebFormsExtractorTests`
and the compilation-context workflow gate. The valid-context CLI test produces
and compares native scan artifacts. Full solution, pinned external language smoke
checks and private Windows acceptance are separate validation, not implied by this
focused gate. See `prompts/validate-semantic-webforms-coexistence.md` for the bounded
work-machine comparison. Missing database terminals are not claimed fixed here.

## Retained body availability is not connectivity

The traversal matrix in `WebFormsModernizationPacketTests` distinguishes property
body evidence, invocation-only evidence, call-only evidence without a target, and
a declaration with no body observations. Calls count as body observations even
without an eligible graph edge. The packet requests `downstream-call-edge`,
`downstream-graph-edge`, or `method-body-evidence` accordingly; none proves a
compiler error or a missing dependency. `semantic-call-resolution` is likewise a
next-evidence request, not a successful binding count. Run that suite alongside
`CombinedDependencyPathTests` and `ExplicitVisualBasicContextTests` (including its
native CLI artifact repeat). Full solution and pinned external smoke runs remain
deferred for this reporting-only correction. Use
`prompts/diagnose-retained-vb-body-connectivity.md` for a no-scan Windows diagnosis.
