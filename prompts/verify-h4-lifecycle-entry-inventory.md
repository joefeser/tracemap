# Verify H4 lifecycle inventory visibility, without inventing dispatch

H1/H2/H3 have accepted retained entry-to-HTTP results. H4 has a retained
method-to-HTTP path; framework invocation remains unproven. Preserve all accepted
artifacts byte-for-byte. This is a reporting check, not authorization to close the
framework entry gap or to rescan private source.

Budget: 15 minutes, one CLI build and one H4 HTTP requery. No scan, combine,
preflight, application execution, HTTP request, database-server access, source
edits, Git mutation, new helper or widened traversal limits. Existing approved
read-only SQLite inspection is permitted. Private outputs/selectors remain outside
Git, the original run and inputs. Stop on missing or conflicting evidence.

1. Read the operator-prepared branch, full HEAD and dirty state live. Preserve
   unpushed work; stop on build-affecting changes. Use the existing inventory and
   verify its combined-index hash. Never copy a tool SHA from an old agent message.
2. Resolve the previously verified H4 method's exact sourceIndexId, scanId,
   commitSha and symbolId to one retained MethodDeclared. Write a new private
   source-root JSON selector with that tuple; do not choose a similarly named method.
3. Inspect the existing index for WebFormsEntryRelationship records with rule
   legacy.webforms.entry-lifecycle.v1 and relationshipKind
   framework-lifecycle-candidate targeting that exact method identity. Verify
   source-index scope and supporting override/declaration facts. Also distinguish
   an ordinary Overrides relationship from a signed-framework lifecycle witness.
   A method name or override alone is not proof of framework dispatch.
4. Build once using
   `dotnet build src/dotnet/TraceMap.Cli/TraceMap.Cli.csproj --nologo -c Debug`.
   Record HEAD before/after build and query. Hash the actual invoked tracemap.dll
   before/after query and require equality with receipt GeneratorSha256. Stop if
   HEAD or binary changes. Git identity and binary SHA-256 are separate evidence.
5. Run one query using the accepted limits and a new private output directory:

   ```text
   dotnet <built-tracemap.dll> webforms-review requery-handler --run <retained-run> --bundle <verified-bundle> --source-root <private-H4-selector.json> --out <new-private-output> --surface http-client --max-depth <recorded-depth>
   ```

6. Verify the expected H4 terminal and ordered method-call path, receipt identities,
   rules, tiers, locations and supporting facts. Preserve truncation and actual
   cutoff diagnostics. Do not infer extraction failure from truncated enumeration.
7. Inspect header.webFormsEntries in the JSON handoff and the new HTML section
   "WebForms entry relationships (separate from call paths)". The section is a
   retained-input inventory, not a claim that every displayed candidate belongs
   to the selected path. Match H4 by exact scoped identity, not display text.
   Verify any candidate's own tier (normally Tier2Structural), rule, location,
   supporting facts, extractor version, commit and unresolved boundary are visible.
   Confirm candidate relationships have NOT been added to traversed call edges
   and have NOT upgraded path tiers. Lifecycle execution remains unproven.
8. If the retained index has no H4 lifecycle candidate, report EVIDENCE NOT RETAINED;
   an older scan may lack it. Do not create a candidate or claim the current
   extractor failed without a fresh authorized test. If the index has it but the
   handoff omits it, report INDEX-TO-REPORT GAP. If handoff has it but HTML omits it,
   report RENDERING GAP. Null/empty inventory is not proof of no framework entry.

Write one new private note. Return only public tool revision, build status,
binary/index verification yes/no, H4 path present, candidate in index/handoff/HTML
yes/no/unknown, candidate tier, path tier, truncation, first unresolved boundary
category, elapsed time and verdict. Use CANDIDATE VISIBLE; DISPATCH UNPROVEN /
EVIDENCE NOT RETAINED / INDEX-TO-REPORT GAP / RENDERING GAP / CONTEXT REJECTED /
TIME LIMIT. Never say H4 ENTRY-TO-HTTP RETAINED merely because a lifecycle candidate
is displayed. No private symbols, source, paths, hashes, URLs or payloads returned.
