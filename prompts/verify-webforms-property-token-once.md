# Verify an unresolved provider with a property-read token

Pin implementation `54b3b84c94e1faea7dff5d2159b61b865dd98703` or a docs-only descendant with identical code and fixture. Require a clean tree and `legacy-webforms/0.32.0`.

1. When authorized, run these public checks from the repository root with a fresh output directory.

   ```sh
   dotnet test src/dotnet/tests/TraceMap.Tests -c Debug -warnaserror --filter FullyQualifiedName~WebFormsAngularServicePostTests
   dotnet build src/dotnet/TraceMap.Cli -c Debug -warnaserror
   dotnet src/dotnet/TraceMap.Cli/bin/Debug/net10.0/tracemap.dll scan --repo samples/webforms-service-post --webforms-iis-mount "/Portal/Desk=." --out <fresh-output>
   ```

2. Check SQLite integrity and NDJSON/SQLite fact-ID parity. Follow both README click IDs through the independent service registration, POST and handler declaration. For each endpoint and request verify four distinct unbound provider gap IDs. The fourth must identify Scripts/adapter.js line 5, receiverKind this, providerMember service, tokenKind member-expression and Tier4Unknown. Check that known-module property tokens still block candidate selection in the negative control.
3. Only after the public gate passes and private execution is authorized, perform at most one fresh static scan with the previously confirmed selection and mount. Trace both original clicks separately. Record the furthest retained fact and whether the property-token provider remains an explicit gap alongside an independently identified endpoint. If blocked, report the first blocking predicate. From the same inputs, summarize other unsupported provider shapes in synthetic terms if available so they can be evaluated together. Keep private identifiers, source and receipts local. Preserve inherited gaps and distinguish a static candidate from an observed request. Do not patch, rerun the private scan or execute the site.
