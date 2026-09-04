# Trace one operator-selected database-backed event

Use the existing retained `legacy-webforms/0.7.1` index (or a locally verified
successor containing the handler-ownership fix). Do not run another scan,
rebuild, install dependencies, execute the application, access a database or
service, modify the index, or write a BRD.

First read `claude-single-page-trace.prompt.md` in this directory and follow its
provenance, read-only access, exact-identity joins, traversal bounds and evidence
limitations. Also use `claude-projection-ownership-verification.prompt.md` for
projection lookup and support verification. The selection instruction below
replaces the first prompt's default selection and same-event instruction; the
other safeguards remain in force.

## Selection

Test exactly one different page/event that the operator identifies as known to
be database-backed. If the local conversation already identifies a specific
such event, use it. Otherwise ask the operator once which page and event to use.
Do not guess from names or select a successful path after exploring alternatives.
Record selection as operator-priority and keep that event even if tracing fails.
The operator's expectation is a test hypothesis, not extracted database evidence.

## Trace

Verify the matching retained run, exact event binding and resolved handler.
Find its projection through handlerSymbolId and exact supportingFactIds tokens,
not a direct bindingFactId lookup on the projection. Verify supporting edges
against handler ownership; preserve semantic versus syntax evidence distinctions.
Follow canonical source/target identities, including assembly identity, at each
hop. No method-name-only or cross-type joins.

Use the original bounds: six call hops, fifty symbols, one hundred edge rows,
and ten terminal candidates. Preserve omitted branches and report truncation.
Stop at an evidenced database boundary or the first unresolved hop. An external
service boundary is also a legitimate stopping point, not permission to call it.
Do not infer a stored procedure from a method name or invent missing edges from
manual source observations. Inspect actual retained fact types and rule IDs;
do not declare absence based on guessed schema names.

## Report

Return a short alias-only report with provenance, selected event alias, hop
table, public rule IDs, evidence tiers, connection basis, traversal counts,
omitted branches, terminal category and first unresolved hop if any. Separate
manual source observations and any source-verification limitations from facts.
Use the original static-trace-supported/partial result criteria. A supported
database-boundary path does not prove runtime execution, successful binding,
SQL execution, branch feasibility or whole-application coverage.

Keep private filenames, paths, symbols, SQL, procedure names, URLs, configuration
values and raw diagnostics on this computer. Do not include them in the report.
