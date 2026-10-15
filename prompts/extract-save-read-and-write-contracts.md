# Extract read contracts and separately locate the Save write

Use the existing private migration handoff and accepted report. Ten-minute
total bound. This is read-only application-contract inspection, not scanner
development. NO build, scan, requery, DLL discovery, SDK changes, application
execution, database/HTTP calls, schema changes, code edits, or commits.

Preserve the accepted baseline: two retained chains, 256 variants, truncated=true,
partial coverage and unchanged provenance gaps. The latest handoff identifies
SelectCartItems and SelectCartCount routes through ExecProc_DataSet to
DbDataAdapter.Fill. These are read/refresh routes. A handler named Save does not
make every reachable operation a write. Earlier CreateBidGroup/ExecProc_Scalar
claims were not supported by those two retained chains.

Recover the existing private handoff and exact approved repository/commit from
local receipts. Use only authorized local source and already supplied schema or
contract files. Do not fetch new repositories, connect to a database, search user
directories, change branches, or alter the approved compilation context. Check
source state against the recorded commit; label drift and stop if it prevents
exact attribution. Preserve unrelated working-tree changes.

Work in two separate lanes:

1. READ CONTRACTS. At the exact SelectCartItems and SelectCartCount callsites
   cited by the handoff, trace the commandText argument passed to ExecProc_DataSet
   and the parameter-building code. Resolve only explicit assignments/constants
   and unambiguous source calls. Record local file/line evidence for command
   identity, CommandType, parameter name/type/direction/default/null behavior,
   result consumption, connection selection, and transaction/error behavior.
   If command identity is dynamic, retain the symbolic expression and state
   precisely what input is missing. Do not infer a stored procedure from a method
   name. Do not assume identical wrapper names imply identical contracts.

2. WRITE LOCATION. Starting at the original Save handler, inspect its actual
   source body and directly relevant callees to distinguish validation, mutation,
   navigation and refresh. Identify any source-supported write-intent call and
   its guard conditions, arguments and result handling. Follow at most six call
   hops and twelve files; do not explore unrelated handlers. Calls into unavailable
   source end at an explicit boundary. Do not require a write to exist, treat
   zero retained write paths as absence, or fabricate a chain by combining paths.
   Keep manual source observations separate from scanner-retained path evidence.
   If CreateBidGroup or AddLinesBatch is relevant, establish that from exact
   source links rather than earlier task prose. Execution order in source is not
   proof of a feasible runtime path.

Create a NEW non-overwriting PRIVATE human-readable Markdown supplement beside
the existing handoff. Do not modify accepted reports or emit machine-readable
artifacts. Link the accepted handoff/receipt locally, retaining their existing
generator/input identities. Include three compact sections:

- Read contracts: evidence-backed values versus unresolved inputs for both
  callsites, with local references. Do not include secrets or connection strings.
- Write boundary: supported call sequence/conditions, first unavailable hop,
  and whether any mutation is evidenced, merely suggested, or unresolved.
- Implementation readiness: what can be specified now, what cannot, and the
  single next required input. Do not approve implementation or choose a new
  framework/API architecture. Keep migration decisions pending human review.

If already-authorized schema files contain the exact identified procedure or
query contract, cite them locally; otherwise request that specific contract.
Do not execute SQL or infer side effects from API names alone. Preserve uncertainty
about database contents, transaction semantics and original-build correspondence.

Return only a sanitized summary: supplement created yes/no, read-contract
categories resolved, write boundary status, first missing input, elapsed time,
and any bound reached. All private names, SQL, identifiers, paths, hashes and
artifacts stay local. Stop at the budget; no follow-on diagnostic loop.
