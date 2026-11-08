# Verify the registration retained at an unresolved alias

Use `7424cb64d2f826d5e8f95a3bba3d817f43cdb1a2` or a docs-only descendant with identical code. Confirm a clean tree and extractor `legacy-webforms/0.33.0`.

1. When authorized, run the focused public controls and scan into a fresh directory.

   ```sh
   dotnet test src/dotnet/tests/TraceMap.Tests -c Debug -warnaserror --filter FullyQualifiedName~WebFormsAngularServicePostTests
   dotnet build src/dotnet/TraceMap.Cli -c Debug -warnaserror
   dotnet src/dotnet/TraceMap.Cli/bin/Debug/net10.0/tracemap.dll scan --repo samples/webforms-service-post --webforms-iis-mount "/Portal/Desk=." --out <fresh-output>
   ```

2. Require both source orderings of Shadowed_provider_retains_independent_registration_without_selecting_it to pass. Check both ordinary fixture rows, SQLite integrity and NDJSON/SQLite fact-ID parity.

3. If public controls pass and private execution is authorized, run at most one fresh static scan with the confirmed selection and mount. Follow each original click ID to its own service call. Check whether the provider gap supports the call and a resolvable observedRegistrationFactId, and whether that registration is independently supported by module and factory facts. Confirm whether service member, POST, and handler joins remain blocked. If any stage differs, report the first actual gap.

4. From the same local source, determine whether the suspect receiver resolves lexically to a parameter, local declaration, outer binding, or implicit global. Locate the write or shadow responsible for the diagnostic. Determine whether it refers to the same binding, whether the application module alias could reach this receiver, and whether the provider token can be tied to a literal. If uncertain, say which binding or value remains unknown. Report only synthetic shapes and scope relationships. Keep private paths, symbols, source and receipts local. Do not modify the extractor, rerun the scan, or execute the site.
