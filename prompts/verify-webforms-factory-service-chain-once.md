# Verify the factory-to-service registration chain

## Copy-ready instruction for Claude

> Follow `prompts/verify-webforms-factory-service-chain-once.md` on `codex/workspace-scope-preflight`. Verify extractor 0.25.0's new factory-to-service control. Follow the exact factory, service, member, POST and handler declaration IDs for each click. Use the unchanged scanner and report the first remaining typed gap.

Use implementation `0e53bd94741d00f75dbdc304e9bedf64b9037ad5` or a docs-only descendant with identical code and fixtures. Execute the following on the approved machine when Joe authorizes validation for this build.

1. Run the focused regressions and build the Debug CLI:

   ```sh
   dotnet test src/dotnet/tests/TraceMap.Tests -c Debug -warnaserror --filter FullyQualifiedName~WebFormsAngularServicePostTests
   dotnet build src/dotnet/TraceMap.Cli -c Debug -warnaserror
   dotnet src/dotnet/TraceMap.Cli/bin/Debug/net10.0/tracemap.dll scan --repo samples/webforms-service-post --webforms-iis-mount "/Portal/Desk=." --out <fresh-public-output>
   ```

2. Require SQLite integrity ok and NDJSON/SQLite ID parity. The fixture README query should return click lines 4 and 5, both reaching the POST at Scripts/services.js:5 and Api/Save.ashx:1. For each row, follow member and registration supporting IDs. The registration at Scripts/services.js:3 must reference the exact factory at line 2, which references the bundle-backed module. Check service token, injection slots, source spans, commit, rule, extractor version, and inherited partial-handler gaps. Stop on a public failure.
3. After the controls pass, use the existing private selection and confirmed IIS mount for at most one fresh authorized static scan. Inspect both original click IDs independently. Report the furthest retained link and any remaining typed gap. Keep private identifiers, paths, URLs, receipts and detailed IDs local. No runtime POST, backend execution or scanner changes are needed.
