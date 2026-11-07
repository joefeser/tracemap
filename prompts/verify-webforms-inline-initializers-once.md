# Verify WebForms inline initializers and the external click handler

## Copy-ready instruction for Claude

> Follow `prompts/verify-webforms-inline-initializers-once.md` on `codex/workspace-scope-preflight`. First verify the public three-initializer fixture, then use the authorized local workspace to check the two previously unresolved simple clicks. Report the first unresolved hop and sanitized counts.

1. Read AGENTS.md. Pin implementation `5e597a958e83ec7ac934b6988561d4a317ff50dc` or a docs-only descendant. Verify the executable emits `legacy-webforms/0.18.0` in fact evidence. Build the CLI and run the focused tests in Debug with warnings as errors.

   ```sh
   dotnet build src/dotnet/TraceMap.Cli -c Debug -warnaserror
   dotnet test src/dotnet/tests/TraceMap.Tests -c Debug -warnaserror --filter FullyQualifiedName~WebFormsAngularClickTests
   dotnet src/dotnet/TraceMap.Cli/bin/Debug/net10.0/tracemap.dll scan --repo samples/webforms-angularjs-click --out <fresh-public-output>
   ```

2. Require SQLite integrity `ok`. Run the fixture README query. Expect one handler from Page.aspx:4 to Scripts/editor.js:2, with its exact click ID and no competing sibling-controller edge. Verify the following query returns three rows for Page.aspx lines 7, 8 and 9, each with value status `unknown`. Cross-check the same IDs, source spans, commit, version, rule, tier and digest fields in NDJSON. Stop on a public control failure.

   ```sql
   SELECT i.file_path, i.start_line, i.end_line,
          json_extract(i.properties_json, '$.valueStatus') AS value_status
   FROM facts h
   JOIN facts i ON instr(',' || json_extract(h.properties_json, '$.supportingFactIds') || ',',
                         ',' || i.fact_id || ',') > 0
   WHERE h.fact_type = 'WebFormsAngularClickHandlerCandidate'
     AND i.fact_type = 'WebFormsInlineServerInitializerCandidate'
   ORDER BY i.start_line;
   ```

3. On the approved machine use the existing private source selection, a fresh output, and at most one static scan. Check the three inline declarations and the two simple click candidates independently. For each click ID report the handler, supporting initializer IDs and exact controller/module, bundle membership and registration evidence locally. If unresolved, record its typed gap and the first missing hop. Complex click expressions retain their own gaps. Treat HTTP, IL and SQL observations separately from this frontend edge.
4. Return only sanitized counts, rule/version, and which stages were evidenced. Keep private identifiers, paths, receipts, digests, source and exception details local. This verification requires no site startup, rendering, or application JavaScript execution.

## Reconciling an exploratory work-machine run

Record the built checkout's commit and local change status privately. A shared
extractor version can also appear on a locally modified build. Label results
from an exploratory build separately, including any skips applied after
script-path or master-page gaps. On the pinned build an unavailable script
can contain a competing registration and keeps the join unresolved.

If the exploratory run reaches UnsupportedAngularControllerRegistration,
inspect the registration source locally and report only its synthetic shape:
argument count, module receiver, any enclosing function's parameters, and
whether the wrapper is directly invoked, conditionally invoked or has no
observed invocation. The gap covers both non-top-level calls and argument
counts other than two. Source inspection is sufficient for this follow-up.
