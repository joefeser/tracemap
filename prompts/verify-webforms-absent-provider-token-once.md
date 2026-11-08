# Verify an absent token beside computed-token provider gaps

> Use implementation `7e3d2f810bba9c00c1dff345e04310f424a205c5` or a docs-only descendant with identical code and fixture. Confirm legacy-webforms/0.29.0. When authorized, run the public control first and then inspect the original two clicks on the private static scan.

1. On the approved machine run:

   ```sh
   dotnet test src/dotnet/tests/TraceMap.Tests -c Debug -warnaserror --filter FullyQualifiedName~WebFormsAngularServicePostTests
   dotnet build src/dotnet/TraceMap.Cli -c Debug -warnaserror
   dotnet src/dotnet/TraceMap.Cli/bin/Debug/net10.0/tracemap.dll scan --repo samples/webforms-service-post --webforms-iis-mount "/Portal/Desk=." --out <fresh-public-output>
   ```

2. Require SQLite integrity ok, NDJSON/SQLite ID parity and both expected README query rows. Each endpoint must expose two distinct gap IDs supported by its member: adapter line 2 with providerMember value and tokenKind absent, and adapter line 3 with providerMember service and tokenKind call-expression. Both require unsupported-receiver and factoryCount 0. Confirm the service registration still has independent factory and module support. Stop on public control failure.
3. Run at most one authorized fresh private static scan using the same source selection and IIS mount. Follow both click IDs. Check whether the zero-argument library site now has AngularUnboundProviderIdentity with tokenKind absent, whether the three computed-token gaps are retained, and whether the intended registration, member, POST and exact handler declaration are reached. If either click still stops, report the furthest retained link and the next typed gap with its source category. Preserve inherited gaps and keep private identifiers, paths, URLs and receipts local.
