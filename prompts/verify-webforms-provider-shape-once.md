# Verify service resolution past the diagnosed library provider

> Use implementation `534dc6706346d2181549c9b89d9c0054112073fb` or a docs-only descendant with identical scanner and fixture. Verify legacy-webforms/0.28.0. When authorized, run the public control, then inspect each original click on the same private static scan selection and IIS mount.

1. Run the bounded public gate on the approved machine:

   ```sh
   dotnet test src/dotnet/tests/TraceMap.Tests -c Debug -warnaserror --filter FullyQualifiedName~WebFormsAngularServicePostTests
   dotnet build src/dotnet/TraceMap.Cli -c Debug -warnaserror
   dotnet src/dotnet/TraceMap.Cli/bin/Debug/net10.0/tracemap.dll scan --repo samples/webforms-service-post --webforms-iis-mount "/Portal/Desk=." --out <fresh-public-output>
   ```

2. Check SQLite integrity and NDJSON/SQLite parity. Require the two expected fixture README rows. For each endpoint follow unboundProviderGapIds to the adapter's AngularUnboundProviderIdentity at line 2. Expect providerMember service, ownerFailure unsupported-receiver and factoryCount 0. Verify the matching service-member fact supports that gap, and its registration has the separate factory/module evidence.
3. After a passing public control, run at most one authorized fresh private static scan. Follow each click's exact service call, app registration, member, POST and declaration. Check whether the diagnosed external provider now has an AngularUnboundProviderIdentity gap rather than terminating that search. If blocked again, report the first remaining gap and its source category/shape. Keep private data local, preserve inherited callback and dynamic-source gaps, and leave the scanner unchanged.
