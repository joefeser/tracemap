# Verify the click candidate beside an unknown dynamic script

## Copy-ready instruction for Claude

> Follow `prompts/verify-webforms-inline-settings-once.md` on `codex/workspace-scope-preflight`. Verify the public dynamic-script control, then use the same authorized private static scan and confirmed IIS mount. Look for a partial click-to-method candidate with an exact blocking gap. Return sanitized counts and the first missing hop. Keep the scanner, source selection and worktree intact.

1. Pin implementation `8b144e4652fb1cf56748d7ae0b98f77e85c1ee7c` or a docs-only descendant with identical implementation, tests and fixtures. Verify the executable emits `legacy-webforms/0.21.0` in evidence.
2. Run the controls in the approved build environment:

   ```sh
   dotnet build src/dotnet/TraceMap.Cli -c Debug -warnaserror
   dotnet test src/dotnet/tests/TraceMap.Tests -c Debug -warnaserror --filter "FullyQualifiedName~WebFormsInlineSettingsTests|FullyQualifiedName~WebFormsIisMountTests|FullyQualifiedName~WebFormsAngularClickTests"
   dotnet src/dotnet/TraceMap.Cli/bin/Debug/net10.0/tracemap.dll scan --repo samples/webforms-inline-settings --webforms-iis-mount "/Portal/Desk=." --out <fresh-public-output>
   ```

3. Run the fixture README query and SQLite integrity check. Require one WebFormsInlineServerStringCandidate with unknown value, one WebFormsAngularPartialClickHandlerCandidate for Page.aspx:3 to Scripts/panel.js:4, and zero ordinary handler candidates. Confirm the partial candidate references the exact click, external registration, mounted include, server string and AngularDynamicScriptSourceUnavailable gap. Cross-check NDJSON and SQLite, including commit, extractor version, rule, tier and spans.
4. After the public control passes, run at most one fresh static scan against the same authorized private workspace selection with the previously confirmed mount. For each of the two known click IDs, check partial and ordinary handler facts separately. Follow supporting IDs to the server-string span, gap, module, registration, included method and bundle evidence. Report only stages actually reached. If both types are absent, report the remaining gap and stop. The dynamically configured script's contents and runtime behavior remain unknown.
5. Keep setting keys and values, script URLs, paths, digests, receipts and detailed source local. No requests to the dynamic script, site startup, debugger patch, application execution, or configuration discovery are needed.
