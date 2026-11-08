# Verify independent service evidence at provider truncation

Pin implementation `53074b73ac1a0f5a57643a2b02aa3700939c027a` or a docs-only descendant with identical source and fixture. Confirm a clean tree and extractor `legacy-webforms/0.34.0`.

1. When authorized, run the public controls and scan into a fresh directory.

   ```sh
   dotnet test src/dotnet/tests/TraceMap.Tests -c Debug -warnaserror --filter FullyQualifiedName~WebFormsAngularServicePostTests
   dotnet build src/dotnet/TraceMap.Cli -c Debug -warnaserror
   dotnet src/dotnet/TraceMap.Cli/bin/Debug/net10.0/tracemap.dll scan --repo samples/webforms-service-post --webforms-iis-mount "/Portal/Desk=." --out <fresh-output>
   ```

2. Check integrity, NDJSON/SQLite fact-ID parity and the ordinary fixture's two README rows. Check the 16/17 boundary control and both file orders in Overflow_preserves_registration_and_alias_evidence_in_both_file_orders.

3. If those checks pass and the private lane is authorized, run at most one fresh static scan with the confirmed selection and mount. For each original click join the call to its AngularUnboundProviderLimit gap. Count supporting AngularUnboundProviderIdentity IDs and confirm exactly 16. Resolve observedRegistrationFactId, trace the registration's factory/module support, and check whether the alias gap links that same registration and the originating call. Verify the truncated lookup has no selected member, Angular POST or endpoint. Report the first differing fact or predicate if the registration is absent. Preserve inherited gaps.

4. Use sanitized counts and synthetic shapes only. Retain private details locally. The previous lexical diagnosis can be reused. No source modifications, repeat private run, application execution or network request to a handler.
