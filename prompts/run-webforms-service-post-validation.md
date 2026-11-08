# Execute the WebForms service POST validation

The source review is complete. Start here when Joe explicitly releases the test
and scan hold. Use implementation fb1d8e6f8643617f6ea10c6b53ca63c31d259ff3
or a documentation-only descendant with identical implementation and tests.
If execution already happened on that build, use the retained results and
continue from the first unverified step.

1. Run the focused tests and build the CLI:

   ```sh
   dotnet test src/dotnet/tests/TraceMap.Tests -c Debug -warnaserror --filter FullyQualifiedName~WebFormsAngularServicePostTests
   dotnet build src/dotnet/TraceMap.Cli -c Debug -warnaserror
   ```

2. Scan the public fixture into a fresh output directory:

   ```sh
   dotnet src/dotnet/TraceMap.Cli/bin/Debug/net10.0/tracemap.dll scan --repo samples/webforms-service-post --webforms-iis-mount "/Portal/Desk=." --out <fresh-public-output>
   ```

   Run the fixture README query and SQLite integrity check. Expect two
   distinct click IDs, one POST site and the exact handler declaration.
   Check the supporting fact IDs against NDJSON and the extractor version
   against legacy-webforms/0.24.0. Stop on a failed control and report it.

3. After the controls pass, use the existing authorized work-machine source
   selection and IIS mount for at most one fresh private static scan. For
   each of the two click IDs report the service call, injection binding,
   member, request and declaration joins present in the index. Preserve the
   partial-handler gaps. Report the first missing relationship if any.

Return executed test counts, public query row count, and sanitized private
stage results. Keep identifiers, URLs, paths and receipts local. Use the
pinned scanner unchanged.
