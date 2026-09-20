# Compiled .NET Evidence Foundation Implementation State

Status: first-slice implementation and portable cross-platform validation complete; exact-head PR review evidence pending

Branch: `codex/compiled-dotnet-evidence-foundation`

Base: `origin/dev` at `0b728b62943de7c0c52a44e170e870c2691dcd34`

Tracking: #759, #766, #767, #768, #769

## Scope decision

The first implementation slice ends at bounded managed-input inventory,
assembly/module/type/member metadata identity, provenance/coverage gaps,
cross-reader agreement, and small public C#/VB.NET/F# fixtures. Source and
compiled facts remain separate in this slice. It does not begin
source-to-metadata reconciliation, broad IL traversal, rewrite analysis, PDB
reconciliation, historical corpus execution, or C++/CLI support.

## Evidence decisions

- Mono.Cecil supplements Roslyn and syntax fallback; it replaces neither.
- Compiled metadata is Tier2 structural evidence unless a future documented
  rule justifies a different tier. Reader/provenance/availability gaps are
  Tier4 unknown.
- Source, metadata, PDB, IL, and rewritten identities stay separate.
- Every compiled fact retains the ordinary scan repository/commit identity,
  while optional receipt-validated binary-source/build provenance uses
  separate fields and may remain absent for an unbound input.
- Manifest-level compiled-input provenance is unconditional, including when no
  assembly is admitted. Its local bounded-input digest participates in `scanId`
  before fact IDs are derived; a distinct shareable summary binds its
  privacy-projected digest into its own artifact identity.
- Private compiled facts/indexes remain local-only in the first slice; a
  shareable derivative must be a distinct non-`CodeFact` privacy-projected
  summary rather than leaking or substituting required scan identity fields.
- Metadata-only facts use a safe assembly locator, `1..1` non-source sentinel,
  and versioned token-bearing properties. Ambient dependency resolution is
  forbidden; only declared, admitted, hashed dependencies may be read.
- Metadata tokens are module-local locations. MVID, path, timestamp, and display
  string alone cannot prove source identity or freshness.
- Bounded-input hashing uses a canonical pre-digest payload that excludes the
  digest and all downstream identities derived from it; the computed digest is
  attached before scan/fact IDs are derived.
- Normalized type/member/signature identities preserve exact metadata
  namespaces, including the empty/global namespace and nested declaring chain.
- Missing, stale, ambiguous, unbound, or mismatched inputs reduce compiled
  coverage and never erase or upgrade source-derived evidence. Timestamps alone
  do not establish staleness.
- Correctness, fixtures, and validation contracts are open-core. Managed
  private Windows execution is outside this implementation slice.

## Historical inputs

- PR #770 at the base commit is the current Web Forms terminal-reachability
  authority.
- The two retained VB/Web Forms investigation worktrees are preserved and are
  inventoried in
  `docs/history/WEBFORMS_VB_INVESTIGATION_BRANCHES_2026-09-19.md`. They will not
  be merged wholesale.
- The `dotnetperf` assessment is pinned to
  `db8c3359badfec620ccdc6df062b1756ef9607f8`. Its strongest dimensions are
  exception-region/branch rewriting and Cecil method-body behavior; its test
  count is not broad ECMA conformance and it lacks the required modern
  cross-language matrix.

## Validation state

Tasks 1-6 are implemented. The lane uses pinned Mono.Cecil `0.11.6` with
deferred reading and a resolver that rejects ambient resolution, then
independently compares normalized rows from `System.Reflection.Metadata`.
Disputed rows are withheld. The CLI accepts explicit primary, dependency, and
binding-receipt inputs plus validated admission limits; the manifest, `scanId`,
facts, SQLite index, Markdown report, and execution receipt retain the bounded
contract without exposing raw absolute paths.

The public portable fixture matrix lives under
`samples/compiled-dotnet-evidence/` and covers C#, VB.NET, and F#. Focused tests
exercise exact CLR identities, duplicate and unresolved dependencies,
provenance states, malformed/native/missing/over-budget inputs, reader
disagreement, deterministic bytes, privacy, all five scan artifacts, and
unchanged source evidence. Review hardening also covers iterative deeply nested
type inventory, filesystem-aware path deduplication, and bounded projection of
overlong input locators. It also pins top-level receipt binding counts, rejects
text limits too small for a complete projected digest, and converts excessive
metadata-signature nesting into an explicit partial-coverage gap. The local
metadata-signature nesting into an explicit partial-coverage gap. Artifact
overflow retains only the configured number of per-input rows plus a
deterministic omitted-count/digest commitment. The local distribution workflow
runs the same focused tests on Windows, Ubuntu, and macOS.

Implementation commit: `a70ac803` (`feat: add bounded compiled metadata evidence lane`)

Portable fixture/test commit: `522e4325` (`test: add portable compiled metadata fixture matrix`)

Final bounds/provenance fix commits: `f28c4e93`, `7df78401`, and `5e17ee4a`.

Local macOS validation on 2026-09-20:

- `dotnet build src/dotnet/TraceMap.sln --no-restore`: passed with zero warnings
  and zero errors.
- `dotnet test src/dotnet/TraceMap.sln --no-restore`: 1,979 passed,
  zero failed, zero skipped.
- focused `ManagedMetadataExtractorTests`: 19 passed, zero failed; the combined
  managed-metadata/report focused set passed 26 of 26.
- two explicit admitted compiled-input CLI scans: 107 facts each, including 80
  compiled-rule facts; byte-identical `facts.ndjson`; all five required
  artifacts present; both output directories passed
  `scripts/validate-adapter-artifacts.py`; neither output contained a local
  absolute path.
- `scripts/check-private-paths.sh`: passed.
- `node scripts/kiro-review.mjs --self-test`: passed; implementation prompt
  dry-run completed with `Coverage: NotRun` as expected because it did not
  invoke the external Kiro reviewer.
- `git diff --check`: passed.

Task 7 is complete. On PR #772 implementation head `5e17ee4a`, the portable
matrix and package smoke passed on Windows, Ubuntu, and macOS; the .NET adapter,
combined-adapter, and private-path jobs also passed. ACK reported a clean merge
state, zero unresolved threads, zero pending/failed checks, and zero actionable
findings, but correctly withheld merge readiness because the required Codex/Qodo
review evidence still names older head `7df78401`. No reviewer was manually
retagged and no merge was performed.

Portable Windows success does not prove Windows PDB, legacy .NET Framework or
Web Forms build behavior, ILAsm/ILDAsm parity, the historical `dotnetperf`
corpus, or C++/CLI feasibility. The following remain deferred without implied
support: source-to-metadata reconciliation, PDB identity, operand-aware IL,
rewriting, legacy build execution, the historical corpus, and C++/CLI.

The later exact-head review added regression coverage for receipt-only scans
and oversized assembly-reference identities. Receipt-only activation now emits
the rule-backed `NoManagedInputDeclared` gap, and assembly-reference plus
dependency-resolution strings honor the configured compiled text limit.
