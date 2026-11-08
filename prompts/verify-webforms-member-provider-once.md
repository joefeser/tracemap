# Verify the member-receiver provider gap and the application endpoint

Use implementation `8128ed61933c4881a8b3bbe593837508437cc268` or a docs-only descendant with identical implementation and fixture. Confirm extractor `legacy-webforms/0.31.0` and a clean tree. This is a static verification dispatch.

1. When execution is authorized, run the public gate from the repository root. Use a fresh output directory.

   ```sh
   dotnet test src/dotnet/tests/TraceMap.Tests -c Debug -warnaserror --filter FullyQualifiedName~WebFormsAngularServicePostTests
   dotnet build src/dotnet/TraceMap.Cli -c Debug -warnaserror
   dotnet src/dotnet/TraceMap.Cli/bin/Debug/net10.0/tracemap.dll scan --repo samples/webforms-service-post --webforms-iis-mount "/Portal/Desk=." --out <fresh-output>
   ```

2. Check SQLite integrity and NDJSON fact-ID parity. Require both README click-to-endpoint rows. For each endpoint inspect its unboundProviderGapIds. Require three distinct Tier4 gaps supported by the click's service call and the included adapter. The gap at Scripts/adapter.js line 4 must record providerMember service, receiverKind member-expression, tokenKind identifier, ownerFailure unsupported-receiver and factoryCount 0. Verify the independent service registration, member, literal POST and handler declaration by ID.
3. Only if the controls pass and the private lane is authorized, run at most one fresh static scan with the prior selection and confirmed mount. Follow both original click IDs independently. Report whether the member-receiver gap survives alongside each application's registration, request and endpoint. If the trace still stops, report the furthest retained fact and first blocking predicate, with a synthetic expression shape. Preserve inherited runtime gaps. Keep private source, identifiers, paths and receipts local. Do not edit code, execute the site, or repeat a private run.
