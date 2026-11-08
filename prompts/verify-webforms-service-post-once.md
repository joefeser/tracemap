# Verify the new AngularJS click-to-service POST rule

## Copy-ready instruction for Claude

> Follow `prompts/verify-webforms-service-post-once.md` on `codex/workspace-scope-preflight`. The new implementation is `fb1d8e6f8643617f6ea10c6b53ca63c31d259ff3` and the expected extractor is `legacy-webforms/0.24.0`. For now, review the implementation, fixture and SQLite query on disk only. Leave tests and scans unrun until Joe releases the validation hold. Report source-review findings separately from executed results.

Read AGENTS.md and the new `samples/webforms-service-post/README.md`. Verify
the receiver in the click method is bound by its positional controller
annotation to the selected service registration. Inspect the member's
constructor annotation for the injected HTTP parameter and the request
inside that member. Check the exact mount-to-inventory lookup and how the
independent ASP.NET directive fact becomes support for the endpoint link.
Use the competing service's same-named member and the earlier list call as
controls. Preserve both inherited click coverage gaps.

## Once Joe authorizes execution

1. Use the pinned implementation or a docs-only descendant with identical
   code, tests and fixture. Run the focused Debug regression gate:

   ```sh
   dotnet test src/dotnet/tests/TraceMap.Tests -c Debug -warnaserror --filter FullyQualifiedName~WebFormsAngularServicePostTests
   ```

2. Build the Debug CLI and scan `samples/webforms-service-post` with
   `--webforms-iis-mount "/Portal/Desk=."` into a fresh output. Run the
   fixture README query. Expect two distinct clicks leading to one literal
   POST site and one declared handler. Inspect each service registration's
   exact module and bundle supports, the injection slots, the member and call
   site spans, and request/declaration IDs. Check SQLite integrity, NDJSON
   parity, commit, rule, tier and extractor version. Stop on control failure.
3. Only with the existing authorization for private static collection, reuse
   the original selection and confirmed mount for at most one fresh scan on
   the approved machine. Start at each previously selected click. Report
   which exact-ID relationships are present and the first remaining typed
   gap. Keep private names, paths, URLs, payloads, detailed IDs and receipts
   local. A static link to a declaration leaves runtime POST dispatch, backend
   method invocation and SQL execution unobserved.
