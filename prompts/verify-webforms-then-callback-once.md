# Verify the click method assigned in a controller's service callback

## Copy-ready instruction for Claude

> Follow `prompts/verify-webforms-then-callback-once.md` on `codex/workspace-scope-preflight`. Verify the public two-click control, then reuse the authorized static work scan with its IIS mount. Check each click's included method, fulfillment callback, trailing rejection callback and distinct typed gaps. Report the first missing hop and keep the pinned build intact.

1. Pin implementation `d525d808094e3154b02a622381785eccba7ce3c7` or a docs-only descendant with identical code, tests and fixture. Confirm `legacy-webforms/0.23.0` in evidence. Preserve any exploratory changes separately.
2. On the approved machine run the public controls:

   ```sh
   dotnet build src/dotnet/TraceMap.Cli -c Debug -warnaserror
   dotnet test src/dotnet/tests/TraceMap.Tests -c Debug -warnaserror --filter "FullyQualifiedName~WebFormsAngularThenCallbackTests|FullyQualifiedName~WebFormsInlineSettingsTests|FullyQualifiedName~WebFormsIisMountTests|FullyQualifiedName~WebFormsAngularClickTests"
   dotnet src/dotnet/TraceMap.Cli/bin/Debug/net10.0/tracemap.dll scan --repo samples/webforms-angularjs-then --webforms-iis-mount "/Portal/Desk=." --out <fresh-public-output>
   ```

3. Require SQLite integrity ok. The fixture README query should return four rows: click lines 4 and 5 each point to Scripts/panel.js:6 through a distinct partial candidate. Both candidates support the same callback at Scripts/panel.js:4–7. On that fact require rejectionCallbackStartLine 7, rejectionCallbackEndLine 9 and an offset at the rejection function. Each supports its own click ID, AngularDynamicScriptSourceUnavailable and AngularThenCallbackInvocationUnobserved. There should be zero ordinary handlers. Confirm the callback's registration support, source spans, evidence tier, rule, extractor version, commit, and matching SQLite/NDJSON IDs.
4. After the controls pass, run one fresh private static scan with the previously confirmed IIS mount and source selection. For each of the two known click IDs query both partial and ordinary candidates. Check the included method span, fulfillment span, rejection callback offset and lines, retained bundle member, owner and supporting gaps. Report what is observed even if another gap remains. Runtime callback invocation, dynamically loaded code, HTTP dispatch and SQL execution remain separate.
5. Return only sanitized counts, stage results and the first missing hop. Keep source, paths, receipts, settings values and identifiers local. A verification result comes from the pinned scanner without altering it or launching the customer application.
