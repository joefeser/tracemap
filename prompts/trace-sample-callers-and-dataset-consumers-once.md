# Trace callers and DataSet consumers of the completed sample — one bounded pass

Next owner dispatch: extend understanding from the validated isolated sample to its callers and returned DataSet consumers. Use the existing `CURRENT-UNDERSTANDING.md` and its linked evidence as the starting point. Do not repeat the completed collection, sample validation, compiler repair or conversion review.

Read applicable AGENTS.md and inspect status/diff. Preserve dirty work, source, configuration, scan outputs, prior handoffs and compile results. No scan, collection, build, compiler/test invocation, dependency installation, application/database execution, SQL execution, code/config edits, conversion rewrite, public commit, push, PR or merge.

## Reuse what is already known

Locate the private understanding document through the existing handoff/progress folder or recent session notes. Reuse its verified sample selector and links to the completed lifecycle result, two-source workspace, SQL sidecar, annotated method and successful compile report. Framework source evidence is separate from DLL admission; specialized lifecycle facts are separate from the combined workspace index. The successful isolated compile does not establish runtime behavior.

Do not ask the owner to reenter known paths, hashes, wizard answers or a target language. Do not rehash all artifacts or audit the whole repository. If a necessary artifact cannot be located, identify the specific absence and complete the portion available. Keep private paths, names, source, SQL, hashes and evidence references local, outside public repositories.

## Trace the actual caller/consumer flow

1. Identify the selected method's direct callers using existing call edges/references and targeted source inspection. Validate overload/owner identity where evidence permits. Matching names alone are candidates, not proven bindings. Keep distinct call sites/executions separate.
2. Follow those callers outward only far enough to explain the relevant entry path: page/control event, handler, application method or other actual boundary supported by existing evidence. Identify where each argument comes from and any conversions, defaults, validation or state reads before the call. Do not assume the same origin at every call site.
3. For each relevant call site, follow the returned DataSet through locals, fields, returns and immediate consumer helpers. Identify actual table selection (ordinal, literal name or dynamic name), null/row-existence checks, column reads, conversions, filtering, aggregation, binding or displayed/returned results. Preserve which execution produced the DataSet; do not attach a different call's result to the selected invocation.
4. Answer the concrete table-naming question: do these consumers require a particular DataSet/TableName, only table ordinals, or neither? Distinguish all inspected consumers from uninspected/ambiguous ones. A consumer using Tables(0) does not establish what that table contains, and absence of a name read in a bounded slice is not proof no caller ever uses a name.
5. Where consumer code accesses rows/columns, compare only with existing admitted SQL result evidence and direct source observations. Declared procedure parameters do not prove result schema. Static column access, runtime row existence, dynamic predicates and actual returned values are distinct. Dynamic DataTable.Select predicates may be described from source but must not be promoted into supported parser/extractor proof or executed.
6. Keep identity/evidence boundaries explicit. Classify each important connection as compiler/extractor-resolved evidence, existing compiled lifecycle proof, structural/syntax candidate, direct source observation or unknown. Source inspection can explain reduced-coverage code; it does not create semantic binding, source-to-DLL correspondence, runtime observations or new scanner facts. Do not infer that event callbacks ran, wrappers changed behavior, SQL executed or a table was populated merely from the presence of code.

The goal is a connected business explanation, not an inventory of every call edge. Follow only relevant callers/consumers. If several distinct entry paths exist, summarize them separately with their evidence and differences. Do not declare unrelated scan-wide gaps blockers for this flow.

## Update the existing private handoff

Add a clearly labeled caller/consumer section to the existing `CURRENT-UNDERSTANDING.md`, retaining prior findings and evidence references. Before updating, retain an exact versioned backup alongside it in the SAME existing private folder; do not create another folder hierarchy. Do not overwrite an existing backup. If concurrent changes are detected, preserve both versions and report the conflict instead of overwriting. Do not alter the annotated conversion or scan/compile artifacts.

Include a compact table for each relevant call site: entry/caller, argument origins, returned DataSet consumer, table selection, columns/result use, evidence class and specific unknown. Keep exact private source/fact references in this document only. End with a short explanation of what the feature accomplishes and which unresolved decisions actually affect conversion.

One bounded pass: at most 30 tool calls or 15 minutes, whichever comes first. Stop sooner when the question is answered. If the bound is reached, list what was inspected and the exact remaining lookup; do not treat uninspected callers as absent or dispatch another diagnostic loop automatically.

## Return a short sanitized report

- Entry/caller paths found and evidence strength, using aliases.
- Argument origins and how the result is consumed, in plain language.
- Table naming dependency: names / ordinals / mixed / not established, scoped to inspected consumers.
- Result-column/schema observations versus unknown row/value behavior.
- Concrete unresolved questions affecting this flow, separately from unrelated gaps.
- Whether the private document was updated with its prior version preserved; confirm no scan/build/compile/code/config/application/database execution occurred.

No employer identifiers, snippets, private paths, SQL or hashes in the response. Stop after the report. This dispatch does not choose a replacement architecture or implement a conversion.
