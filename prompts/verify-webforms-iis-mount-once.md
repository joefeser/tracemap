# Verify a WebForms master-page script through an IIS mount

## Copy-ready instruction for Claude

> Follow `prompts/verify-webforms-iis-mount-once.md` on `codex/workspace-scope-preflight`. Verify the public fixture with the IIS mapping off and on. Then use the owner's confirmed virtual prefix and repository-relative web root on the authorized static work scan. Report whether each click reaches a handler and, if unresolved, the first remaining typed gap. Keep the pinned scanner and original source selection intact.

1. Build implementation `4f647c9b0603921a696bc19bcac05950dc55cd6d` or a docs-only descendant with identical code, tests and fixture. Require `legacy-webforms/0.20.0` in facts. Preserve local patches in their own checkout.
2. Run `dotnet build src/dotnet/TraceMap.Cli -c Debug -warnaserror` and `dotnet test src/dotnet/tests/TraceMap.Tests -c Debug -warnaserror --filter "FullyQualifiedName~WebFormsIisMountTests|FullyQualifiedName~WebFormsAngularClickTests"`.
3. Use the built CLI for two public scans into fresh directories:

   ```sh
   dotnet src/dotnet/TraceMap.Cli/bin/Debug/net10.0/tracemap.dll scan --repo samples/webforms-iis-mount --out <unmapped-output>
   dotnet src/dotnet/TraceMap.Cli/bin/Debug/net10.0/tracemap.dll scan --repo samples/webforms-iis-mount --webforms-iis-mount "/Portal/Desk=Site" --out <mapped-output>
   ```

   Without the flag expect an UnsupportedAngularScriptPath gap and zero handlers. With it, require a WebFormsExternalScriptReferenceCandidate at Site/Layout.master:4 pointing to file:Site/Scripts/panel.js, preserving the actual inventory spelling. Verify resolutionSource, mountPrefix, repositoryWebRoot and scriptUrl. The handler for Site/Page.aspx:3 must retain that include's exact ID and point to Site/Scripts/panel.js:2. Check SQLite integrity, NDJSON/SQLite IDs, rule, tier, commit and version. Scan IDs and authorized-scope fingerprints should differ.
4. After both controls pass, reuse the previous authorized `tracemap scan` arguments and source selection for one private static scan in fresh output. Add only `--webforms-iis-mount "<virtual-prefix>=<web-root-relative-to-scan-root>"`. Use `.` on the right if the existing scan starts at the web root. Use the verified folder spelling if the scan starts at its parent. The rule uses the selected inventory; a path that was filtered or absent is a gap.
5. Inspect the master-page include and each previously blocked content-page click independently. For each handler verify clickFactId and supportingFactIds with retained facts. If another script or master initializer blocks lookup, report it and stop there. Return sanitized counts and the first unresolved hop. Keep private paths and receipts local. Use the static scanner without deploying the site, fetching assets or executing application code.
