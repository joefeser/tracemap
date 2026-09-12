# Implementation state

- Branch: `codex/issue-737-vbnet-data-boundaries`
- Base: `origin/dev` at `debf5a8fa5720775cb60337a9d94053e83689a4f`
- Issue: #737
- Current scope: initial compiler-backed Visual Basic ADO.NET slice implemented; not pushed.
- Contract decision: reuse `SqlCommandDetected` / `database.sql.text.v1` and `DatabaseOperationCandidate` / `database.operation.call-pattern.v1` so existing public consumers can ingest VB evidence.
- Privacy decision: retain categorical command metadata and optional constant-value hashes; never retain raw SQL, procedure names, connection strings, or parameter values.
- Join decision: correlate only Roslyn-resolved receiver symbols within a containing method; spelling-only matches remain gaps.
- Implemented evidence: compiler-resolved DbCommand construction, CommandType assignment, parameter collection mutation, ExecuteReader/ExecuteScalar/ExecuteNonQuery families, and DbDataAdapter.Fill for DataSet/DataTable. Existing object/call facts retain DataSet, DataTable, reader, and supporting call-path evidence.
- Downstream decision: `DatabaseOperationCandidate` is a supported Web Forms terminal with the existing shared `sql-persistence` kind; C# and VB extraction remain language-specific while the projection contract is shared. Shared Web Forms extractor is `legacy-webforms/0.8.3`.
- Safety: constant command strings retain hash and length only. Stored-procedure evidence is the categorical `StoredProcedure` enum assignment joined by compiler-resolved receiver identity. Raw SQL/procedure/parameter/connection values are not retained.
- Gap budget: unresolved name-only Fill/Execute candidates emit at most 50 site gaps plus one deterministic budget-exhaustion gap per document and never emit operation candidates.
- Remaining issue scope: HTTP/file/service/config boundaries and complete downstream/smoke validation.
- Validation: solution build succeeded with the two known pre-existing warnings; focused Visual Basic plus Web Forms database-terminal suite passed 65/65. Dedicated `VisualBasicDataBoundaryTests` passed 3/3 and covers same-method two-command isolation, command/adapter/reader/DataSet/DataTable/Fill/Execute evidence, late binding, privacy sentinels, and repeated-scan fact determinism. Full solution suite passed 1915/1915. The pinned OSS smoke is intentionally pending parent review of this initial slice.
