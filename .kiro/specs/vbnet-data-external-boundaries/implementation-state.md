# Implementation state

- Branch: `codex/issue-737-vbnet-data-boundaries`
- Base: `origin/dev` at `debf5a8fa5720775cb60337a9d94053e83689a4f`
- Issue: #737
- Current scope: compiler-backed Visual Basic ADO.NET plus HTTP/config/file slice implemented locally; not pushed.
- Contract decision: reuse `SqlCommandDetected` / `database.sql.text.v1` and `DatabaseOperationCandidate` / `database.operation.call-pattern.v1` so existing public consumers can ingest VB evidence.
- Privacy decision: retain categorical command metadata and optional constant-value hashes; never retain raw SQL, procedure names, connection strings, or parameter values.
- Join decision: correlate only Roslyn-resolved receiver symbols within a containing method; spelling-only matches remain gaps.
- Implemented evidence: compiler-resolved DbCommand construction, CommandType assignment, parameter collection mutation, ExecuteReader/ExecuteScalar/ExecuteNonQuery families, and DbDataAdapter.Fill for DataSet/DataTable. Existing object/call facts retain DataSet, DataTable, reader, and supporting call-path evidence.
- Downstream decision: `DatabaseOperationCandidate` is a supported Web Forms terminal with the existing shared `sql-persistence` kind; C# and VB extraction remain language-specific while the projection contract is shared. Shared Web Forms extractor is `legacy-webforms/0.8.3`.
- Safety: constant command strings retain hash and length only. Stored-procedure evidence is the categorical `StoredProcedure` enum assignment joined by compiler-resolved receiver identity. Raw SQL/procedure/parameter/connection values are not retained.
- Gap budget: unresolved name-only Fill/Execute candidates emit at most 50 site gaps plus one deterministic budget-exhaustion gap per document and never emit operation candidates.
- External evidence: shared HTTP facts cover compiler-resolved HttpClient, WebRequest, and WebClient calls; ConfigurationManager/My.Settings access reuses ConfigBinding; VB Global.System.IO display strings feed the shared batch/data-movement extractor. Hosts, raw URLs, config keys/values, and file arguments are not retained by these new facts.
- WebRequest decision: construction with a constant string or New Uri constant retains safe normalized-path/hash evidence; a later parameterless GetResponse remains an unknown-destination HTTP fact plus a gap because cross-statement receiver correlation is not yet established.
- Service decision: compiler-resolved inheritance from WCF ClientBase(Of T) and ASMX SoapHttpClientProtocol is recognized, but existing service mappers do not establish VB proxy/operation facts. Explicit Tier4 gaps are emitted and task 8 remains partial rather than guessing a service mapping.
- Downstream validation: a synthetic VB Web Forms handler reaches shared HTTP/config/file evidence through handler/call-path projection; the generated packet, docs/query recipes, and WITS-compatible corpus validator consume the same facts without a VB-only schema. Existing code-path/source view consumes the same retained spans and call facts.
- Remaining issue scope: positive VB WCF/ASMX mapping plus relevant pinned smoke/final acceptance.
- Validation: full solution suite passes 1919/1919. The combined focused VB/Web Forms/review/handoff/batch suite passes 138/138. Dedicated external-boundary tests pass 4/4 and cover HttpClient/New Uri, WebRequest construction/unknown response destination, WebClient, ConfigurationManager section/indexers, System.IO projection, WCF/ASMX gaps, late binding, privacy, determinism, packet/docs/query-recipe/WITS corpus consumption. Shared batch tests pass with the VB display-string compatibility seam. The pinned OSS smoke remains pending issue completion.
