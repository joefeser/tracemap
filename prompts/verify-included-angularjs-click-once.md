# Verify a WebForms click to an included AngularJS method

Read AGENTS.md. Use `codex/workspace-scope-preflight` implementation
`4e32864e4d1453cab3b88ed95b9693393b5f0a1c` or a documentation-only descendant
with identical implementation and fixtures. Check the actual tool commit and
`legacy-webforms/0.17.0` in the fact evidence. The .NET build references pinned
Acornima 1.9.1 and AngleSharp 1.10.0. Use the existing approved build environment.

1. Run `dotnet test src/dotnet/tests/TraceMap.Tests -c Debug -warnaserror --filter FullyQualifiedName~WebFormsAngularClickTests`
   and `dotnet build src/dotnet/TraceMap.Cli -c Debug -warnaserror`. Run
   `dotnet src/dotnet/TraceMap.Cli/bin/Debug/net10.0/tracemap.dll scan --repo samples/webforms-angularjs-click --out <fresh-public-output>`.
2. Use the fixture README SQLite query. Require one row from Page.aspx line 4 to
   Scripts/editor.js line 2 for EditorController.save, with the exact click fact
   ID. The sibling controller has the same method name. Inspect supporting IDs
   through the page/master relationship, module and controller scopes, rendered
   bundle and declaration-linked member, JS registration and function assignment.
   Check NDJSON/SQLite rule IDs, tier, commit, spans, digests, version and SQLite
   integrity. Stop if the public control fails.
3. For the known work page use the saved authorized workspace and a fresh output
   directory if one static scan is needed. Confirm the expected external file
   was admitted in the selected .NET source inventory and the bundle/member
   facts are present. Keep previous outputs and the existing input limits.
   If the saved exact roster omits JavaScript, report the scope gap to the owner.
4. Query `legacy.webforms.angularjs-click.v1` for the page's click, owner facts,
   handler candidate or typed gap. Check the candidate's external source span,
   click fact ID, and controller/module ownership against the private source.
   Report the first missing hop. An independently read POST or handler declaration
   stays a separate observation until an explicit call relationship connects it.
5. Return sanitized counts and a stage table covering inclusion, scope, click,
   method and first unresolved hop. Keep private paths, identifiers, source,
   detailed digests and receipts local. Use static evidence only.

## Copy-ready instruction for Claude

> Follow `prompts/verify-included-angularjs-click-once.md` on the testing branch. Run the public same-name-controller control, then check the authorized work page for the exact click-to-external-method relationship. Report the first gap and which links came from retained facts.
