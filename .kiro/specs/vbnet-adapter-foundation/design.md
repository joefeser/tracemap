# VB.NET Adapter Foundation Design

## Approach

The first VB implementation remains inside the existing .NET scanner because
Roslyn, MSBuild workspace loading, source-snapshot protection, storage, and
reporting are already shared there. It adds a sibling VB semantic/syntax front
end rather than teaching the C# syntax walkers to interpret VB nodes.

```text
.sln / .vbproj / .vb inventory
  -> protected semantic-input snapshot
  -> MSBuildWorkspace + Visual Basic compilation
  -> VB semantic facts (Tier1)
  -> per-file VB syntax fallback and gaps (Tier3/Tier4)
  -> shared fact, symbol, call-edge, object-creation, SQLite and report writers
```

## Package and boundaries

- Add `Microsoft.CodeAnalysis.VisualBasic` and
  `Microsoft.CodeAnalysis.VisualBasic.Workspaces` at the repository's pinned
  Roslyn version.
- Introduce VB-owned rule IDs and extractor versions before emitting facts.
- Keep syntax node types isolated in VB-specific source files. Shared helpers
  may own deterministic IDs, safe properties, spans, and result merging only
  when their semantics are language-neutral.
- Extend inventory and source-snapshot selection to `.vb`; do not change C#
  inclusion or tiering.
- Merge C# and VB extraction results deterministically for mixed-language
  solutions, sorting by stable fact/relationship keys before persistence.

## First supported evidence

- Namespace, type, delegate, field, property, event, constructor, method, and
  parameter declarations.
- Compiler-resolved symbol references and member access.
- Invocation and object creation with containing caller, target symbol,
  arguments, and resolved parameter relationships where Roslyn proves them.
- Direct containment, inheritance, implementation, and override relationships
  where Roslyn supplies an unambiguous symbol.
- Syntax candidates for the same families when semantic evidence is
  unavailable, clearly separated by rule and tier.

VB-specific `Handles`, `AddHandler`, `RemoveHandler`, `RaiseEvent`,
`WithEvents`, and Web Forms composition belong to #738. Foundation fixtures may
contain these constructs only to prove they are not silently promoted to
resolved event edges before that slice.

## Identity and safety

Semantic symbol identity uses Roslyn documentation IDs or the existing
canonical .NET symbol normalization where compatible. Syntax-only identity is
file-scoped, language-tagged, deterministic, and never joins to a semantic
symbol solely by name. Persist safe names, hashes, kinds, arity, and spans; do
not persist source snippets, literals, connection values, SQL, local absolute
paths, or private repository identity by default.

## Coverage

The aggregate .NET scan is fully semantic only when every selected compiler
source in scope has the promised semantic coverage. A failed VB project load,
VB compiler diagnostics that prevent binding, skipped VB source, or syntax
fallback degrades the scan even when C# projects succeed. Reports identify the
affected language/project/file scope without calling the repository clean.

