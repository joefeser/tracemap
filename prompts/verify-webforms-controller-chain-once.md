# Verify a WebForms click through chained AngularJS controller registrations

## Copy-ready instruction for Claude

> Follow `prompts/verify-webforms-controller-chain-once.md` on `codex/workspace-scope-preflight`. Verify the pinned public fixture and report the first unresolved hop on the authorized work page. Use the scanner as built and keep exploratory patches separate. Stop if verification would require changing scanner code or scan inputs.

1. Pin implementation `4344b34fc93876b6de7c17635ca13b7d968e0b68` or a docs-only descendant with identical code, tests and fixture. Record the build checkout and local change status privately. Require `legacy-webforms/0.19.0` in the emitted evidence. Use a clean checkout if the existing one contains local repairs.
2. Build and run the public controls:

   ```sh
   dotnet build src/dotnet/TraceMap.Cli -c Debug -warnaserror
   dotnet test src/dotnet/tests/TraceMap.Tests -c Debug -warnaserror --filter FullyQualifiedName~WebFormsAngularClickTests
   dotnet src/dotnet/TraceMap.Cli/bin/Debug/net10.0/tracemap.dll scan --repo samples/webforms-angularjs-click --out <fresh-public-output>
   ```

3. Require SQLite integrity `ok` and the fixture README query's single row from Page.aspx:4 to Scripts/editor.js:2. Check the supporting controller registration, module, three unknown initializer facts and exact click ID. Cross-check spans and IDs in NDJSON. The regression that selects all three controllers must resolve distinct registrations at lines 1, 6 and 9 and distinct method assignments at lines 2, 7 and 10. Duplicate registrations, unknown intermediate calls and chains exceeding 32 links must produce a gap.
4. Only after the public control passes, use the existing authorized private selection and a fresh output for at most one static scan. For each of the two previously observed simple clicks inspect the exact click ID, included module, bundle member, controller registration and handler span or typed gap. An absolute script URL or master-page initializer can still block lookup before registration. If blocked, report the first available gap and mark the downstream chain unverified. A previous exploratory build cannot establish a pinned result.
5. Return sanitized counts and the first missing hop. Leave source, paths, receipts and digests local. Execution of the application, browser scripts or backend is outside this verification.
