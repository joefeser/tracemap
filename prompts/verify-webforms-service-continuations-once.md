# Verify a service registration inside a module configuration chain

> Use implementation `1fb838773aca942446ef51f650a201f31b970c6e` or a docs-only descendant with identical code and fixture. Verify legacy-webforms/0.30.0 on the authorized machine. Check the registration's own source span and all following module calls before tracing either click to its endpoint.

1. When execution is authorized, run the public gate:

   ```sh
   dotnet test src/dotnet/tests/TraceMap.Tests -c Debug -warnaserror --filter FullyQualifiedName~WebFormsAngularServicePostTests
   dotnet build src/dotnet/TraceMap.Cli -c Debug -warnaserror
   dotnet src/dotnet/TraceMap.Cli/bin/Debug/net10.0/tracemap.dll scan --repo samples/webforms-service-post --webforms-iis-mount "/Portal/Desk=." --out <fresh-output>
   ```

2. Require SQLite integrity ok, NDJSON/SQLite ID parity and two fixture README rows. Check the service registration spans Scripts/services.js:3–8, supports the factory at line 2, and records filter, directive, config and run at lines 9–12 in moduleContinuations. Check both unbound library observations, the POST at line 5 and the independently declared .ashx handler.
3. After the controls pass, run at most one fresh authorized private static scan with the same selection and confirmed IIS mount. For each original click ID follow the module, factory, service registration, member, POST and handler declaration. Confirm any inherited gaps remain and report the furthest retained fact for each click. If blocked, report the first failed admission condition and its synthetic expression shape. Keep private identifiers, URLs, paths and receipts local. No scanner changes, runtime calls or site execution are needed.
