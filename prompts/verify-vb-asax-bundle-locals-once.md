# Verify VB application startup bundle locals once

Read AGENTS.md. Use `codex/workspace-scope-preflight` implementation commit
`fff0b0967f3757b8b5097d116019c2a548e8b3f3` or a documentation-only descendant
with identical implementation and fixtures. Record the tool commit and look for
extractor version `legacy-webforms/0.16.0` in the emitted bundle facts.

1. Run `dotnet test src/dotnet/tests/TraceMap.Tests -c Debug -warnaserror --filter FullyQualifiedName~WebFormsScriptBundleTests`
   and `dotnet build src/dotnet/TraceMap.Cli -c Debug -warnaserror`.
2. Scan `samples/webforms-script-bundle-startup` into a fresh output directory.
   Verify two declarations, three members and two references under
   `legacy.webforms.script-bundles.v1`. Compare fact IDs between NDJSON and
   SQLite. Run the fixture README query, check both declaration IDs and the exact
   three ordered rows, then run `PRAGMA integrity_check`. The earlier
   `samples/webforms-script-bundles` fixture should still yield four bundle facts.
3. Once the public controls pass, inspect the existing authorized local work
   setup. Use an index produced by this version on the intended inputs, or perform
   one authorized static rescan into a new directory with the same saved scope
   and limits. Keep prior runs. Stop on a setup, identity or coverage mismatch.
4. Inspect the known master reference and the application startup file. For each
   expected member, follow its exact declaration fact ID and check physical file
   lines and local ordinal. Record any bundle-rule gap and whether the source
   matches the admitted inline VB and local-use forms. Treat same-path render
   references as candidates until application ownership has independent evidence.
5. Return sanitized counts for declaration, member, reference and gap facts.
   Report the first unresolved hop toward the client call and backend. Keep exact
   identifiers, paths, source, hashes and receipts private. The fixture verifies
   static declarations. Runtime and external JavaScript tracing require their own
   evidence.

## Copy-ready instruction for Claude

> Follow `prompts/verify-vb-asax-bundle-locals-once.md` on the testing branch. Verify the public interleaved-bundle control first. Then check whether the authorized work scan retains the startup declaration and the intended member with its exact declaration ID, ordinal and source line. Report any gap and the first unresolved hop.
