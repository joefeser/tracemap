# Messy .NET workspace regression fixtures

Synthetic, public-safe fixtures that reproduce the workspace shapes observed in
real Web Forms/.NET scans: source spread across folders, independently scanned
roots, merged reviews, same-name members, deep call chains, cycles, and
projectless VB files. Everything here is synthetic; nothing copies private
source, names, paths, or artifacts.

Stable case identities live in `case-catalog.json`
(`messy-workspace-case-catalog.v1`). Cases are marked `implemented` or
`deferred`; deferred cases record their exact blocker or next-slice owner.
The catalog is the inventory — it does not claim that deferred cases are
proven.

Roots:

- `root-alpha/` — C# Web Forms-shaped site (SDK project) with pages, services,
  and data folders: a ten-step handler chain ending in an ADO.NET-style SQL
  terminal at traversal distance 12 (beyond depths 8 and 10), a three-node
  call cycle plus a self-cycle, and ten same-name `Process`/`Core` members in
  one file that must stay distinct.
- `root-beta/` — second independently scanned C# root with an `.ashx` handler
  whose gateway deliberately reuses the `Process`/`Core` simple names from
  `root-alpha` to pressure cross-root joins.
- `vb-projectless/` — loose VB files with no `.vbproj`/`.sln`, exercising the
  portable Roslyn syntax fallback (Tier3 evidence plus fail-closed gaps).

The regression suite that consumes these fixtures lives in
`src/dotnet/tests/TraceMap.Tests/MessyWorkspaceRegressionTests.cs`; see
`docs/VALIDATION.md` for the focused validation commands.
