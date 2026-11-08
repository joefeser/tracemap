# Verify computed-token library providers beside the application service

> Follow this prompt on `codex/workspace-scope-preflight`. On the approved machine, once authorized, validate 0.26.0 against the public fixture and the two original click IDs. Report the furthest retained relationship and the exact remaining gaps. Preserve the pinned scanner and source selection.

Pin `954c1617dbd4fb1c9530b30fe6d8a4e0a262fba2` or a docs-only descendant with identical implementation and fixtures. Require legacy-webforms/0.26.0 in fact evidence.

1. Run the focused Debug tests, build the CLI and scan the public control:

   ```sh
   dotnet test src/dotnet/tests/TraceMap.Tests -c Debug -warnaserror --filter FullyQualifiedName~WebFormsAngularServicePostTests
   dotnet build src/dotnet/TraceMap.Cli -c Debug -warnaserror
   dotnet src/dotnet/TraceMap.Cli/bin/Debug/net10.0/tracemap.dll scan --repo samples/webforms-service-post --webforms-iis-mount "/Portal/Desk=." --out <fresh-output>
   ```

2. Require SQLite integrity, NDJSON/SQLite ID parity and both expected fixture README query rows. On each endpoint follow unboundProviderGapIds to a Tier4 AngularUnboundProviderIdentity fact on the adapter script. Confirm the same ID is supported by the member, while the service registration is independently supported by the factory/module chain. The two sites have distinct source spans. Stop on public control failure.
3. Use the existing authorized private scan selection and confirmed mount for one fresh static scan. For each original click follow the service call, factory, service, member, literal POST and exact handler declaration. Check that any qualifying library call remains an unresolved provider observation and that the parent callback and dynamic-source gaps persist. Report whether both clicks reach the declaration or the first remaining typed gap. Keep paths, token values, detailed IDs and receipts local. No site startup, network POST or scanner patch is needed.
