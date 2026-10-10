# Corrected assignment: sanitize the actual source chain, not a sample

Read `AGENTS.md`. This supersedes the minimal-reproduction emphasis in
`CLAUDE_SANITIZED_REPRODUCTION_PROMPT.md`. Preserve the sample fixture already
created; it is useful regression work, but it does not satisfy this assignment.
Do not rebuild that sample or rerun the already-passed filter-fix tests as a
substitute for the work below.

## Required deliverable

Produce a consistently sanitized transformation of the owner's **actual page,
code-behind, supporting files, and relevant framework dependency source**. The
purpose is to investigate the remaining real call-chain complexity locally on
another machine. It is not to demonstrate the already-fixed publication filter
with a shorter example.

Do not author a replacement sample application. Do not replace an intermediate
framework method with a direct database API call. In particular, a direct
`SqlDataAdapter.Fill` call is not an acceptable replacement for the actual
framework wrapper chain just because the final endpoint is the same.

Use the original failing handler and existing private review evidence available
in this Windows checkout to locate the files. Do not ask the owner for more
photographs, individual aliases, or copied source. Do not assume every dependency
is available: establish that by inspecting the authorized local files.

## Begin with a private inventory and transformation map

1. Locate the actual page/code-behind and supporting source files. Follow the
   relevant calls into dependency source, including framework base classes,
   wrappers, constructors, getters, and argument-producing methods. The previously
   discussed four supporting files are a starting point, not a size limit.
2. Make a private inventory linking each original file/member to its sanitized
   counterpart. Record each relevant call edge and whether its target source is
   available. Preserve the actual source files as the transformation input.
3. Use one consistent mapping across the entire bundle. Keep the original names,
   paths, and mapping outside both the shareable tree and tracked repository files.
   In the shareable inventory, use synthetic file/member identities only.

## Fidelity rules

- Copy and transform actual source. Preserve all statements and control-flow
  structure in the included relevant methods except explicitly documented privacy
  substitutions. Do not summarize, shorten, rewrite, or "clean up" those methods.
- Preserve intermediate call hops, overloads, parameter positions and types,
  optional arguments, inheritance, nested types, properties/accessors, receiver
  initialization, exception regions, cache/session branches, impersonation logic,
  lambdas, early returns, and SQL/string construction structure.
- Preserve project and assembly boundaries relevant to extraction, including the
  separate framework dependency. Preserve markup/code-behind bindings and
  publication layout. Do not collapse everything into one assembly to ease builds.
- Preserve framework API names. Consistently replace business-specific identifiers
  and sensitive literals; use fictional SQL with the same construction and
  parameter relationships. Remove secrets and unnecessary private comments,
  resources, paths, domains, and personal data. Record privacy-driven changes in
  a private transformation ledger without exposing originals in public output.
- Do not bypass missing source. If only a proprietary DLL is available, do not
  ship it, invent its implementation, or claim a behaviorless stub is equivalent.
  Record the exact dependency boundary privately, preserve available work, and
  report the access/source limitation. Any proposed alternative extraction step
  must stay within the owner's authorized local access and applicable constraints.
- Unrelated application areas can be excluded from the package, but do not delete
  branches inside a relevant method simply because the shortest retained witness
  does not traverse them. Document scope exclusions and unresolved targets.

## Verify fidelity before spending time on builds

Compare each transformed relevant method to its original locally. Use syntax-aware
normalization of mapped identifiers and substituted literals where feasible to
check statement/control-flow structure. Inventory the before/after call edges
using the private mapping. Investigate missing intermediate calls rather than
accepting the same terminal API as sufficient.

Produce a public-safe coverage summary: files/methods transformed, call edges
preserved, source boundaries unavailable, and explicit exclusions. Never include
original identity hashes or reversible mappings in a shareable artifact. Apply
repository generator and privacy-projected bounded-input hash requirements to
new derived machine-readable artifacts.

## Build and validate the transformed package

After the fidelity check, supply portable build files and safe configuration.
Generate new assemblies and publication receipts from the transformed inputs;
never reuse original hashes/receipts after modifying source. Keep the deploy-only
publication condition and relevant assembly boundaries intact.

Provide one build command and one focused normal-query/verification command. Check
the original source-handler-to-business/data-access/framework-to-database route
under synthetic names, including relevant getter/argument flows. Inspect normal
receipt, handoff, and HTML, not just graph endpoint visibility. Preserve and report
remaining dispatch uncertainty, unknown runtime values, and depth limits.

Do not force exact 28/116 counts after excluding unrelated application areas.
Explain any relevant route differences. A green test for the old filter defect
does not establish source fidelity. Do not rerun the full TraceMap test suite or
repair unrelated tests for this packaging task. Do not execute real SQL, HTTP,
authentication, or business operations.

## Privacy review and delivery

Keep originals and mapping in a separate private directory. Scan and review the
entire proposed shareable tree, including filenames, comments, configs, project
references, generated outputs, and archive contents. Original-token matching is
only one check, not proof of privacy. Verify the package builds from a clean copy
without private filesystem references. Avoid shipping binaries/PDBs when they can
be rebuilt from the sanitized source.

Create a reviewed-candidate source archive and/or patch using an explicit file
allowlist. Keep the earlier sample patch separate; do not label it as this package.
Do not commit or upload private inputs or maps. Do not upload the candidate package
automatically; the owner must review it before transfer. Pushing is not required.

Finish with the exact local deliverable path, source-fidelity checks performed,
focused build/query results, privacy-review status, exclusions, and any precise
unavailable dependency. If a dependency blocks completion, report that honestly
instead of silently constructing a smaller sample. Keep a concise local state
note so compaction does not restart completed work.
