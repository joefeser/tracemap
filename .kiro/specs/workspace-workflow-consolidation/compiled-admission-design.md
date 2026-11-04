# Next slice: source repositories and an existing compiled publication

Status: native fresh workspace route implemented and locally verified, including
built synthetic replays and the full Debug suite. Owner trial is next.
Retained-source attach remains a separate unimplemented extension.

## User journey

Configure web source, framework source and an existing published ASP.NET site.
Verify scope, explicit DLL/dependency selection and source/publication evidence.
Collect into a new run, combine admitted evidence, then open one starting report.
Do not require users to reconstruct diagnostic page-number commands.

## Two routes

### From zero: fresh source plus compiled collection

The existing native fresh workflow supplies source and explicit compiled inputs
to one scan. Use it for the web application, scan the framework separately, then
combine the two original raw indexes. There are three evidence channels but not
necessarily three scan indexes. Preserve native configuration, receipts,
checkpoint-selected scan identity and per-channel limitations.

The orchestration must resolve the exact admitted raw scan from checkpoint state,
not select the newest directory or feed a report's combined index back into
generic combine. Verify that source/compiled reconciliation and all report
bindings survive adding the framework source before declaring parity.

### Add a publication later: attach

Keep the existing web source scan immutable. Native attach produces a separate
compiled-only scan bound to that exact parent. Combine web, framework and
compiled raw indexes using explicit `CompiledAttachmentCombineInput` evidence.
The current lower-level combiner accepts additional source indexes while
validating the parent/child pair; the public generic combine command does not
expose that admission contract.

The framework is an additional source, not another automatically admitted parent
of the compiled child. No connection is inferred from similar names or commits.
Source/assembly binding and source-to-source relationships retain their own
evidence requirements.

## Configuration implemented for fresh collection

Opt-in `tracemap-workspace.v2` uses
`compiledReview: { repositoryId, nativeConfigPath }`. The referenced prepared
native config is consumed unchanged, must be fresh, and owns web scope. The web
workspace entry cannot override project/solution/glob/target settings. Core primary
metadata and publish receipt bindings are inspected during read-only verification.
Native source identity must match the selected workspace web root and commit.

The native completed-run verifier exposes only checkpoint-owned raw scan/report
locations after verifying history, artifacts and current runtime identity.
Workspace rechecks native inputs and source indexes after combination and before
completion. It retains the native bundle's independent index/document identities.
The original design below continues to constrain future config migration/attach.

Keep the source-only v1 configuration valid. Design a versioned extension that
names the web repository ID and an explicit native configuration/proof input.
Avoid copying independent DLL lists into multiple user-owned configs. Derived
effective settings must retain generator/input hashes and cannot silently alter
an imported config whose provenance binds its exact bytes. Missing correspondence
requires the existing explicit owner attestation/preparation route, not an
agent-generated claim.

Constraints addressed by the fresh route; re-evaluate for future attachment:

- Exact checkpoint-owned raw scan locator and retained snapshot prerequisites.
- Supported reuse or projection of imported configurations without breaking
  their proof-import provenance. Never mutate the original local config.
- Scope filter behavior: a compiled review whose web parent is excluded must
  fail before collection, rather than silently selecting another parent.
- Tool distribution pinning and output ownership across composed stages.
- One starting report that keeps source, framework and compiled gaps visible.

## Acceptance

1. Public synthetic web/framework/publication fixture exercises fresh collection
   and combined reporting; no private files or work identifiers are used.
2. Separate retained-source attach fixture preserves original bytes and binds
   the compiled child only to its actual web parent.
3. Missing files/receipts, hash or commit drift, wrong parent, excluded web scope,
   incompatible historical schema and existing output are explicit failures.
4. Framework evidence remains distinguishable; successful combination alone does
   not claim that every framework call was resolved.
5. Owner runs a single documented trial command and opens one starting report.
6. Windows ASP.NET publication acceptance is recorded separately from macOS
   metadata/IL or handcrafted contract fixtures.

## Historical config and SQL

`webforms-compile-web-config.v1` is owner-reported; its reader/generator remains
unlocated in current code. Do not guess a schema migration from field names.

SQL follows this slice. Keep the offline definition-admission concept in the
inventory; an agent rendering a dump is not proof that a native importer admitted
procedure contracts. Do not introduce a pretend SQL property to this extension.
