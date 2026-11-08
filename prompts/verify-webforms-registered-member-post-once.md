# Verify the POST inside the independently registered service

Pin implementation `c5abd8284dd10dc508c6a4f96a1a569e0321bfc3` or a docs-only descendant with identical code and fixture. Confirm a clean tree and extractor `legacy-webforms/0.35.0`.

1. When execution is authorized, run the focused public checks in a fresh output directory.

   ```sh
   dotnet test src/dotnet/tests/TraceMap.Tests -c Debug -warnaserror --filter FullyQualifiedName~WebFormsAngularServicePostTests
   dotnet build src/dotnet/TraceMap.Cli -c Debug -warnaserror
   dotnet src/dotnet/TraceMap.Cli/bin/Debug/net10.0/tracemap.dll scan --repo samples/webforms-service-post --webforms-iis-mount "/Portal/Desk=." --out <fresh-output>
   ```

2. Check SQLite integrity and NDJSON/SQLite fact-ID parity. Run both queries in the fixture README. Require two click endpoint rows and one shared registration/member/POST row. Verify source spans, the HTTP injection slot and the POST URL hash. Check both truncated source orderings and the duplicate HTTP option negative control.

3. If public checks pass and the private lane is authorized, perform at most one static scan with the previously confirmed selection and mount. Follow each original click's limit gap to its observedRegistrationFactId. Starting from that ID, follow exact definition and POST support IDs using the second README query. Report whether both clicks identify the same declaration-side evidence. Confirm alias and truncation gaps remain and there is no selected member, selected request or endpoint for those clicks. If the registration has a definition but no POST, report the gap supported by the definition and the first failed predicate, using synthetic terms. Do not treat the URL hash as proof of an endpoint or execution. Keep private source, paths, identifiers and receipts local. No code edits, repeat private scan or site execution.
