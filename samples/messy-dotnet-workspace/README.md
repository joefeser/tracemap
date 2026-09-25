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
- `vb-init-web/` and `vb-init-backend/` — a control `Init` handler with
  `With`/`For Each` over an inline `New ...().MyList`. The exact constructor
  calls an inline-created service, which forwards through a typed repository
  field and typed SQL-gateway field to a synthetic ADO.NET terminal. The graph
  asserts a constructor side-effect path, not property contents or control binding.
- `vb-init-duplicate/` — a second same-name, same-arity constructor in a
  separately scanned root; the page-to-constructor hop must fail closed.
- `vb-init-service-duplicate/` — a second same-name service in another root;
  the inline-created receiver call must also fail closed.
- `vb-qualified-init-web/` and `vb-qualified-init-backend/` — a projectless
  dropdown constructor side-effect path whose unqualified created type is
  grounded by one explicit namespace import. A same-name constructor in an
  unrelated namespace has its own SQL decoy and must not be reached by symbol
  reconciliation.
- `vb-qualified-ambiguous-web/` — competing explicit imports for those
  same-name constructors; no constructor or SQL terminal may be chosen.
- `vb-overload-web/` and `vb-overload-framework/` — a synthetic projectless
  constructor path through a field initialized by an inherited `Open` method and two
  `ByRef` SQL-gateway overloads (`ArrayList` then typed arrays) in a
  separately scanned root to an ADO.NET `Fill` terminal.
  Passing this case does not explain the private receiver declaration or prove
  a terminal in a private page.
- `root-generated/` — buildable Web Forms-shaped C# page whose auto-generated
  designer bridge is deliberately excluded from source traversal. A bound
  compiled scan pins the handler's source→metadata→IL/PDB identity chain.
- `vb-pdb-projectless/` and `vb-pdb-build/` — a source tree with no project file
  and a separate deterministic VB build of its page handler. The source-only
  path stops at an excluded generated bridge; an admitted portable PDB with an
  exact source-document checksum and one method-row/sequence-point owner can
  enter IL and reach the existing `root-generated` SQL terminal. Duplicated
  source methods or a missing checksum join withhold that entry. Native Windows
  PDBs and private page compilation are not claimed by this fixture.
- `vb-publish-projectless/` — an actual public-safe VB Web Site layout with
  `CodeFile`, `App_Code`, a dropdown `Init` handler, constructor-populated
  collection, inherited `Open`, overloaded procedure helper, and ADO.NET
  `Fill`. Its source-only regression must stop short of SQL. The separate
  Windows publish check uses the 32-bit Framework compiler without `-u` and
  verifies only that a `.compiled` map names an existing page assembly with
  no PDB. The public published IL constructs a `SqlDataAdapter` and calls the
  public inherited `DbDataAdapter.Fill(DataSet)` override; that one-argument
  target is not a protected overload. The fixture does not yet establish a
  TraceMap source-method identity join or a complete compiled path.
- `root-crosslanguage/` — buildable C#→VB→F# project graph. The fixture pins
  the current partial evidence: a C# syntax downgrade with a compilation gap,
  a VB semantic call to F#, admitted F# metadata, and an explicit unsupported
  F# source-ownership gap. It does not claim a complete source traversal.

The regression suite that consumes these fixtures lives in
`src/dotnet/tests/TraceMap.Tests/MessyWorkspaceRegressionTests.cs`; see
`docs/VALIDATION.md` for the focused validation commands.
