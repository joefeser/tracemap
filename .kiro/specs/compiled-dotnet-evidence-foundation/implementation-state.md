# Compiled .NET Evidence Foundation Implementation State

Status: design and documentation only; implementation not started

Branch: `codex/compiled-dotnet-evidence-spec`

Base: `dev` at `046d3c4166f0999e8b0f9928d365708a84dc8ec0`

Tracking: #759, #766, #767, #768, #769

## Scope decision

The first implementation slice ends at bounded managed-input inventory,
assembly/module/type/member metadata identity, provenance/coverage gaps,
cross-reader agreement, and small public C#/VB.NET/F# fixtures. It does not
begin broad IL traversal, rewrite analysis, PDB reconciliation, historical
corpus execution, or C++/CLI support.

## Evidence decisions

- Mono.Cecil supplements Roslyn and syntax fallback; it replaces neither.
- Compiled metadata is Tier2 structural evidence unless a future documented
  rule justifies a different tier. Reader/provenance/availability gaps are
  Tier4 unknown.
- Source, metadata, PDB, IL, and rewritten identities stay separate.
- Metadata tokens are module-local locations. MVID, path, timestamp, and display
  string alone cannot prove source identity or freshness.
- Missing/unbound/mismatched inputs reduce compiled coverage and never erase or
  upgrade source-derived evidence.
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

Documentation guards only are required for this branch. No implementation,
package addition, build artifact, fact schema, or rule catalog change has been
made. Implementation validation remains unchecked in `tasks.md`.
