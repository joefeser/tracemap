# Design

## Semantic extraction

`VisualBasicSemanticExtractor` walks Roslyn operations for each semantically analyzed `.vb` document. A new bounded data-boundary pass identifies:

- object creation whose constructed type derives from an allowlisted ADO.NET base type;
- invocation whose resolved method belongs to `DbCommand` or `DbDataAdapter` families;
- `CommandType` property assignments and parameter-collection mutation associated with a compiler-resolved command receiver.

Provider subclasses are accepted through base-type traversal, not textual namespace guesses. Existing `MethodInvoked`, `CallEdge`, `ObjectCreated`, and argument-flow facts remain the supporting call-path evidence.

## Shared contracts

Database calls use `DatabaseOperationCandidate` with `database.operation.call-pattern.v1`. Command construction and configuration use `SqlCommandDetected` with `database.sql.text.v1`. These are the existing language-neutral consumer contracts, allowing current database review and reverse-impact consumers to ingest VB evidence without a parallel schema.

Properties remain categorical and stable: framework family, operation kind, method name, receiver/result type, command-text classification, command-type classification, and parameter-evidence counts. Raw SQL, procedure names, parameter values, and connection strings are not retained. When a compile-time constant command string is available, only its SHA-256 digest may be retained.

## Receiver correlation

Within one containing method, Roslyn symbol identity correlates a local, parameter, field, or property receiver across construction, assignment, parameter mutation, and execution. Correlation is never based only on spelling. Facts remain site-specific; aggregated properties summarize only evidence seen for that same resolved receiver in that method.

## Gaps

Potential ADO operations are recognized textually only to decide whether to emit an `AnalysisGap`. A gap does not create a database-operation or command fact. Gap reasons are categorical and bounded per document. Late-bound calls, unresolved overloads, invalid delegate/dynamic shapes, and missing framework metadata remain unknown.

## Determinism and safety

Facts are ordered and deduplicated by the existing materialization pipeline. Diagnostics and gaps contain safe categories, not raw compiler messages. Tests assert repeated extraction equality and absence of SQL/connection/parameter sentinel values.

