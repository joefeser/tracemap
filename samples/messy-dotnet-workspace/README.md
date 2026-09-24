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
  and data folders: a twelve-call-edge handler chain ending in an ADO.NET-style SQL
  terminal at graph distance 14 (beyond depths 8, 10, and 12), a three-node
  call cycle plus a self-cycle, and ten same-name `Process`/`Core` members in
  one file that must stay distinct, plus two interface overloads with two
  possible receiver implementations and four distinct SQL terminals.
- `root-beta/` — second independently scanned C# root with an `.ashx` handler
  whose gateway deliberately reuses the `Process`/`Core` simple names from
  `root-alpha` to pressure cross-root joins.
- `vb-projectless/` — loose VB files with no `.vbproj`/`.sln`, exercising the
  portable Roslyn syntax fallback (Tier3 evidence plus fail-closed gaps).
- `vb-compound-pages/` — three synthetic projectless VB Web Forms pages. Page
  Two dispatches to five same-name `Process` methods in one file, with two
  distinct SQL terminals after further call levels, including unqualified and
  `Me.` calls; Page Three reaches no
  supported terminal; Page Eleven reaches one. Bounded packet tests pin the
  exact receiver bridges and depth-independent terminal inventories.
- `vb-split-web/` and `vb-split-backend/` — the same 2/0/1 page shape with
  page handlers and backend classes in separately scanned roots. The test
  checks the cross-root bridges and terminal boundaries after index combine;
  passing this synthetic case does not establish that an unseen receiver
  pattern in a private merged graph is supported.
- `root-generated/` — buildable Web Forms-shaped C# page whose auto-generated
  designer bridge is deliberately excluded from source traversal. A bound
  compiled scan pins the handler's source→metadata→IL/PDB identity chain.
- `root-crosslanguage/` — buildable C#→VB→F# project graph. The fixture pins
  the current partial evidence: a C# syntax downgrade with a compilation gap,
  a VB semantic call to F#, admitted F# metadata, and an explicit unsupported
  F# source-ownership gap. It does not claim a complete source traversal.

The regression suite that consumes these fixtures lives in
`src/dotnet/tests/TraceMap.Tests/MessyWorkspaceRegressionTests.cs`; see
`docs/VALIDATION.md` for the focused validation commands.
