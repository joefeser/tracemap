# VB.NET Adapter Foundation Requirements

Issue: [#736](https://github.com/joefeser/tracemap/issues/736)  
Parent: [#1](https://github.com/joefeser/tracemap/issues/1)

## Goal

Add a deterministic Visual Basic .NET front end to the existing .NET scanner
without changing the shared evidence contract or weakening C# behavior. The
first slice covers inventory, Roslyn semantic evidence, syntax fallback, and
the minimum shared artifact integration needed for later Web Forms and legacy
data slices.

## Requirements

1. The inventory SHALL include `.vb` files and classify ordinary,
   code-behind, designer, generated, and assembly-info shapes without treating
   a filename convention as semantic proof.
2. The scanner SHALL load supported `.vbproj` inputs through Roslyn Visual
   Basic workspaces when the project and dependencies are available.
3. Compiler-resolved declarations, references, invocations, object creation,
   argument/parameter relationships, and direct symbol relationships SHALL be
   emitted as `Tier1Semantic` evidence with VB-specific rule IDs.
4. When project loading, compilation, or symbol binding is incomplete, the
   scanner SHALL continue over readable `.vb` files using Visual Basic syntax,
   cap those facts at `Tier3SyntaxOrTextual`, emit `Tier4Unknown` gaps, and mark
   coverage reduced.
5. Every fact SHALL retain a deterministic fact ID, repository and commit SHA,
   file path and one-based line span, extractor ID/version, rule ID, evidence
   tier, and sorted safe properties.
6. Syntax-only callee text SHALL NOT be represented as a compiler-resolved
   target. Late binding, overload ambiguity, default members, reflection,
   conditional compilation, and unavailable generated sources SHALL produce
   explicit limitations or gaps.
7. The adapter SHALL populate shared symbols, occurrences, fact-symbol joins,
   call edges, and object-creation rows only when a backing fact exists and the
   stored tier/rule preserves the original claim strength.
8. A VB scan SHALL emit the normal `scan-manifest.json`, `facts.ndjson`,
   `index.sqlite`, `report.md`, and `logs/analyzer.log` artifacts and pass the
   shared artifact validator.
9. The implementation SHALL preserve source-snapshot verification across
   semantic project loading and SHALL fail with the existing typed snapshot
   errors when protected VB inputs change during a scan.
10. Tests SHALL cover deterministic output, semantic success, syntax fallback,
    mixed C#/VB solutions, generated-file handling, unsafe-value redaction, and
    absence of VB files.
11. Validation SHALL include a checked-in synthetic fixture and a commit-pinned
    open-source VB.NET repository. Proprietary application source SHALL NOT be
    committed or named in public artifacts.

## Non-goals

- Full VB Web Forms control and event composition (`#738`).
- Specialized ADO.NET and external-boundary extraction (`#737`).
- Runtime dispatch, branch feasibility, application execution, or production
  usage claims.
- BRD inference, modernization planning, or Angular/.NET/PostgreSQL generation.
- Raw source snippets by default.

