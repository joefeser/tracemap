# Requirements

## Purpose

Extend the deterministic Visual Basic adapter with compiler-backed evidence for legacy ADO.NET data boundaries. This public scanner slice exposes evidence that downstream reviewers can retrieve; it does not infer runtime behavior, generate modernization plans, or translate database artifacts.

## Initial slice

1. Recognize compiler-resolved Visual Basic construction and use of `DbCommand`, `DbDataAdapter`, `DbDataReader`, `DataSet`, and `DataTable` families, including provider subclasses.
2. Emit the existing shared `SqlCommandDetected` and `DatabaseOperationCandidate` fact contracts for `Fill` and supported `Execute*` calls.
3. Retain compiler-resolved source symbols, target symbols, paths, spans, rule IDs, evidence tiers, commit identity, and extractor versions through the normal scan pipeline.
4. Classify command text without emitting private SQL or stored-procedure names. Constant values may contribute only a deterministic hash and a coarse stored-procedure candidate signal.
5. Retain semantic evidence for `CommandType` assignment and parameter mutation when it can be joined to the same compiler-resolved command receiver.
6. Emit bounded, explicit analysis gaps for ADO-like invocations whose receiver or target cannot be resolved because of late binding, invalid code, or unavailable metadata. Never guess a target or join.
7. Continue to emit useful partial evidence when a project has compilation errors.
8. Keep output deterministic and privacy-safe across repeated scans.

## Later issue scope

- HTTP, file, service, and configuration boundary extraction.
- Joining boundary facts into all Web Forms review, query-recipe, source-view, and WITS handoff surfaces.
- Broader legacy-provider and open-source smoke coverage.

## Non-goals

- Executing application code or database calls.
- Discovering live schemas or stored-procedure definitions.
- Emitting raw SQL, connection strings, parameter values, private paths, or application identifiers beyond the established private/raw-source options.
- BRD generation, migration planning, PostgreSQL translation, or claims about runtime reachability or success.

