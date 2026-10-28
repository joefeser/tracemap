# Work-machine test: compiled ArrayList SQL parameter lifecycle

Continue the offline TraceMap test on the work machine. Test the actual application
pattern using the published implementation; do not implement another speculative
extractor or repeat the previous unsupported-ArrayList diagnosis without checking
the new native evidence.

## Current closed legacy core profile

The current head admits two explicit signed neutral mscorlib profiles (2.0.0.0 and
4.0.0.0, token b77a5c561934e089) for the bounded ArrayList operations: parameterless
construction, Add(Object), GetEnumerator, IEnumerator.Current/MoveNext and
IDisposable.Dispose with the existing tracked-enumerator checks. The exact legacy
Object base constructor remains admitted. The scalar/Object/IDisposable type
operands and RuntimeHelpers.GetObjectValue helper also accept these exact core
identities, preserving declared scalar types and unknown input values.

This is a closed signature catalog, not global version normalization. Legacy
generic collections, System.Data 2.0.0.0, unsupported ArrayList overloads/mutations,
lookalike namespaces and incorrect signing/signatures remain unsupported. Existing
bounds, element freezing, full enumeration and per-execution attachment rules remain.
The object-path and SQL compare routes are unchanged.

Tests are explicitly deferred. The C#/VB definitions rewrite the public fixtures'
core AssemblyRefs in memory, require independent metadata agreement, and cover
producer/reader paths, execution isolation and invalid collection/attachment cases.
Compilation is not behavioral or private-input acceptance.

Use the same privately verified DLLs/root selections with a fresh output directory.
Before the one native run, inventory ALL distinct encoded framework call/type
identities in the selected retained closure (max128 members and256 distinct
framework identities; report truncation). Use existing body/call facts or local
metadata inspection, not execution. Classify exact signatures against this head's
catalog. Keep body links/tokens/private hashes local. A shareable summary may contain
only public framework signatures and recognized/unsupported status, with no private
owner, method, field, SQL or source content. This inventory is descriptive and must
not be used to infer a candidate or skip an unsupported operation.

If another identity fails, return the first native failure AND the inventory of all
remaining unsupported public framework signatures in that closure, not just the
next constructor name. Do not infer compiler behavior or target framework from an
AssemblyRef version alone. Use the single sanitized capsule/sample fallback below.
A throwing/omitted wrapper is an illustrative stub, not a complete replay.

## Current root-sliced metadata repair

Use the newly fetched public head, IL extractor **0.2.8**, policy
`explicit-il-body-evidence.v12`. This iteration is compile-only: Joe explicitly
requested no tests. Historical pass totals do not validate this patch.

The previous actual-text accounting repair could still exhaust the shared corpus
quota before the target root. That quota is per reader across admitted inputs,
not per assembly. This patch adds explicit root-sliced retention rather than raising
the cap. The selected root and inspected callees receive a fresh bounded metadata
lease; unrelated classes do not consume that lease. This is a proposed repair,
not private acceptance. No new photos or manually supplied identities are needed.

Before scanning, derive each target's `rawFileSha256` and `metadataToken` from the
existing local `ManagedIlBodyDeclared` facts, verify the DLL still has that SHA,
and append `--il-lifecycle-root <sha256>:<0x06xxxxxx>` for each selected root (max16).
Use the exact MethodDef token, not a name, MemberRef token or invented argument value.
Keep identities private. If old body facts are absent, inspect the matching compiled
metadata locally to establish SHA and MethodDef token; do not ask Joe to type them.
Do not change the application or rebuild its DLLs to manufacture a root.

Selected mode deliberately skips corpus-wide SQL object/lifecycle interpretation.
Raw body facts remain available, but `objectBodyScope: deferred-to-root-slice`
means their object metadata was deferred, not that the selected root failed.
The scan is partial. Inspect each selected lifecycle candidate/gap's JSON
`lifecycleSlice` (`il-sql-lifecycle-slice/1`) for actual retained/unavailable members,
per-reader character usage, decode work, constructor-definition count and failure.
The receipt binds generator and bounded-input hashes. Body links still reference
admitted raw body facts. Do not diagnose starvation from the deferred corpus facts.

Limits: 16 root selections, 128 closure bodies/root, 4,096 admitted raw bodies in
the lookup index, 4,096 reference lookups/root, 64 MiB verified input bytes/root,
at most 1 MiB retained characters/reader/root, and one aggregate decode-work budget
across selected roots. Each lease is released before the next root. Existing
256-instruction/64-field metadata and interpreter/heap/enumeration limits remain.
Both readers must agree on the selected body and its original admitted raw body.
Changed inputs, missing/ambiguous bodies, incomplete metadata or closure/budget
overflow must produce a gap, never assumed values.

Also retain `bodyMetadataFailure` when present. `root-metadata` / `callee-metadata`
mean metadata was missing before interpretation. A constructor search with zero
admitted matches does not alone prove constructor absence or overload ambiguity;
use the slice's inspected constructor-definition count and exact failing member.
Raw admission and `provenanceState: unbound` do not establish interpreted lineage.

Run once with the selected-root flag. If there is still no entry candidate, use the
failure-only Claude request below with that same output. Return the exact first
failing slice/body/opcode and an independently authored complete public sample.
An illustrative stub with omitted methods or a throwing wrapper is not a replay.
Do not start another whole-app scan just to repeat the diagnosis.

## Entry patch status and execution constraint

The published branch now includes an entry-protocol patch after the previously
validated ArrayList implementation. **Joe explicitly requested no tests for this
quick iteration.** Compile the tool once, then inspect the bounded native behavior
on the available real inputs. Do not run unit/full test suites unless Joe separately
asks. The new source tests/fixtures exist but have not been executed. Compilation
is not behavioral validation; previous 87/5,527 results cover the older baseline.

## Inputs and revision

Discover these inputs locally. Joe does not need to fill in a worksheet or know a
private acceptance folder. Use the current work-application context and existing
checkouts/build outputs; ask only when the application checkout cannot be identified
or multiple plausible target flows need a human choice.

```text
HISTORICAL_VALIDATED_BASELINE = b4526fcec6b7ab5e814a1f55051e4229bff9139c
TOOL_BRANCH = codex/sql-compiled-wrapper-evidence
EXPECTED_TOOL_COMMIT = AUTO: exact fetched branch SHA, verified and recorded below
PRIVATE_SOURCE_ROOT = AUTO: locate the work application checkout
PRIVATE_ACCEPTANCE_ROOT = NONE: old acceptance notes/receipts are optional
COMPILED_INPUTS = AUTO: discover matching caller and SQL-wrapper DLLs
ROOT_AND_SELECTED_READ = AUTO: locate the actual ArrayList custom-parameter flow
OWNER_DECLARATIONS = NONE unless an honest independent owner declaration is available
NEW_OUTPUT_ROOT = AUTO: create a new private local directory outside source/baselines
```

Read AGENTS.md, the current continuation section of
`.kiro/specs/sql-compiled-wrapper-evidence/implementation-state.md`,
`docs/validation/SQL_COMPILED_WRAPPER_EVIDENCE.md`, and lifecycle rule limitations.
Fetch `origin/codex/sql-compiled-wrapper-evidence`. If the handoff supplied an exact
prompt/tool SHA, verify and use it. Otherwise resolve the fetched branch SHA once
and record it as EXPECTED_TOOL_COMMIT. Verify that it descends from the historical
baseline above. Implementation/fixture/test/rule changes for the new entry protocol
are expected and explicitly unvalidated. Record that status; do not stop solely
because the user deferred tests, and do not apply the historical pass totals to it.
Use a clean tool checkout at the verified SHA
without resetting any dirty checkout. Record this public tool SHA separately from
the private application's revision.

Discover matching caller/wrapper DLLs in existing local build or publish outputs.
Use project references, assembly identities, inspected bodies and any available
source/binary correspondence evidence; similarly named DLLs are insufficient.
Do not rebuild the application or silently accept stale/mismatched binaries.
If DLLs are unavailable, report exactly which assemblies/projects are missing and
what build would be required. Preserve uncertainty about deployment correspondence.

Locate the target flow by inspecting custom parameter objects added to ArrayList,
passed to a compiled wrapper that constructs/attaches actual SqlParameter instances
and fills a DataSet. Identify the real selected read and its eligible root. If
several applications or flows fit, return a short private candidate list and ask
one focused selection question. Do not make Joe supply identities the agent can
establish from local evidence.

No acceptance folder or owner declaration is required for the native lineage test.
If no old notes, receipts or baselines exist, mark them absent and continue with
available source/compiled inputs. Create a fresh private output directory yourself.
Never substitute public synthetic fixtures for a real-input acceptance claim.

The implementation-tree validation was 87 focused/regression passes, 5,527 full
Debug passes with zero failures and three existing skips, five Python guards, and
unchanged original/named 20/26-root compiled matrices. The archive/recovery incident
and earlier development failures are recorded in implementation-state.md. These are
public synthetic results; the work-machine test is still required.

## Authority and privacy

This prompt authorizes bounded offline inspection, tool compilation, a new native
scan when inputs fit, and optional conditional schema comparison. Do not execute
the application, SQL, provider methods, containers, live requests or impersonation
flows. Do not alter application source, accepted baselines, existing output,
ACK policy or deployment state. No push, PR, merge or publication of private material.

Keep employer source, names, paths, SQL, identities, private hashes, facts, exports
and reports on the work machine. Do not upload them to hosted services or paste them
into public issues/commits. A sanitized summary is not automatic authorization to
publish the underlying evidence. Preserve generator and bounded-input SHA-256
provenance for derived artifacts; shareable derivatives must hash only their
privacy-projected inputs. Do not store source snippets by default.

## 1. Establish the actual input and build state

Inspect retained notes and evidence first if available; their absence is not a
blocker. Preserve any existing accepted baseline hashes before and after testing.
Record exact application repo/revision/dirty state, caller and wrapper assembly
identities/MVIDs/hashes, missing bodies, build status and available
source-to-binary or deployment correspondence. Treat these as separate claims.
A failed or partial application build must stay labeled failed or partial.
Do not reuse facts from an older generator as proof of the new feature.

Build the tool once using .NET 10 and the repository's documented prerequisites:

```text
dotnet build src/dotnet/TraceMap.Cli/TraceMap.Cli.csproj -c Debug -warnaserror
```

Record actual build exit codes and local CLI/Core/Reporting hashes. The earlier
87 focused passes are historical, not an expected result for this untested patch.
Do not run test suites during this iteration. A work-machine rebuild need not have
the same bytes as a macOS build; pin the actual bytes used. Follow AGENTS.md's local
prerequisite discovery if a tool is missing. Report compile failures directly.

## 2. Check whether the actual compiled flow fits

Inspect the actual compiled bodies and source correspondence without executing them.
The supported lifecycle pattern is:

- Exact signed framework identities for `mscorlib` 2.0.0.0 or 4.0.0.0 `ArrayList` parameterless
  construction, `Add(Object)`, `GetEnumerator`, and `IEnumerator.Current/MoveNext`.
  Method names alone, `IList`/other collection dispatch, or lookalike APIs do not fit.
- At most 16 inspected custom-object entries. Constructor field stores and inspected
  property getters must resolve the name, SqlDbType and size used by actual
  `SqlParameter(name, type, size)` construction and command attachment.
- One complete enumeration; custom elements freeze when added. Unknown element types,
  unsupported mutation, unknown escape, repeated/partial enumeration and missing,
  ambiguous or incomplete bodies must produce explicit gaps, not assumed values.
- Each parameter allocation attaches once and each command fills once; fresh
  collections/commands must keep separate execution and returned-table identities.
- Existing bounds remain: 4,096 bodies, 256 instructions/body, 8,192 steps, 24 call
  depth, 256 heap objects, eight executions and 64 stack/local slots, plus aggregate
  work/retained-metadata budgets. Do not increase bounds to force acceptance.
- The root must fit a bounded lifecycle entry contract: a
  nonvirtual/nongeneric entry. The original Object-returning column-read route still
  takes zero arguments or one Boolean. The separate entry route admits exact signed
  DataSet-returning producers and SQL-related Void loaders, static or with a fresh
  inspected parameterless instance constructor, up to 16 opaque unbound arguments.
  A single Boolean argument has both alternatives; other unknown control/scalars
  remain gaps. Instance/session state is not fabricated. A producer must return a
  tracked SQL-derived DataSet; a Void loader must retain such a dataset in a direct
  instance field/property or perform an observed read. DataSet subclasses are not
  silently treated as framework DataSet. Existing helper/consumer restrictions apply.

Do not edit or wrap the application to manufacture a supported root. Do not equate
an accepted ArrayList leg with acceptance of a later unsupported Select predicate,
catch continuation, session state, dynamic helper or whole-application flow. Missing
source is not proof of source/binary equivalence. The tested net48 fixture/API identity
does not establish the application's target framework or runtime behavior.

## 3. Run a fresh native offline lineage check when eligible

An independent SQL result declaration is not required merely to test native lineage.
If exact caller/wrapper bodies and a real eligible root exist, run one bounded scan
into a new directory. Repeat only as needed to check deterministic replay or a
concrete failure. Use the built CLI and repeat `--compiled-input` for each necessary
assembly; do not omit the wrapper or substitute source-reader evidence.

```text
dotnet <local-built-tracemap.dll> scan --repo <PRIVATE_SOURCE_ROOT> --out <new-private-scan-directory> --il-body-evidence --compiled-input <caller.dll> --compiled-input <wrapper.dll> --il-lifecycle-root <privately-derived-sha256>:<privately-derived-MethodDef-token>
```

Inspect all three fact kinds under `dotnet.compiled.il-sql-lifecycle.v1`, Tier3:
`ManagedIlSqlLifecycleCandidate` (`il-sql-lifecycle/1`, returned column read),
`ManagedIlSqlLifecycleEntryCandidate` (`il-sql-lifecycle-entry/1`, producer/loader
completion), and `AnalysisGap`. Do not count only the older candidate type and
mistake new entry facts for zero output. Entry payload `entry` retains root argument
origins, root signature hash, returned/stored datasets, tables and commands with
per-execution parameter allocations. Count each kind separately for the actual
selected roots; record specific admission/body/argument/output gaps.
Retain native fact IDs, generator/bounded-input hashes, invoked-body links,
body/binary/offset/invocation, command hashes, execution identity and table/read
lineage locally. Verify that the intended custom constructor and getter/field loads,
actual SqlParameter creation/attachment, command Fill and selected returned read
are linked by inspected bodies. Separate source observations from native findings.

Test execution isolation against the actual available calls: a parameter from one
execution must not silently bind another. Do not mutate the private program to add
negative cases; the public C#/VB tests cover wrong elements/collections, mutation,
escape, limits, partial/repeated enumeration and invalid attachment. If actual
mutation or unsupported operations are present, require a native gap.

If the real root or an indispensable leg does not fit, retain the native gap or
bounded body evidence and report the exact failed leg. Do not force a candidate,
replace unknowns, run the application, or repeatedly rescan an unchanged refusal.
A native ArrayList success is a lineage result, not parameter-to-SQL-contract
comparison; that comparison route has not been added for ArrayList inputs.

## 4. Optional independent schema comparison

Only original returned-column `ManagedIlSqlLifecycleCandidate` facts are accepted
by this comparison route. Producer/loader entry facts establish a separate bounded
leg and must not be fabricated into a returned read. If only entry facts exist,
report native entry success/partial coverage and defer column/schema comparison
with that explicit missing consumer leg.


Keep these routes distinct:

- `sqlserver-compiled-lifecycle compare` retains its public synthetic declaration
  contract. Never relabel employer SQL or a manual production transcription synthetic.
- `produce-owner-schema` / `compare-owner` provide separately labeled Tier3
  `owner-declared-result-shape` admission. They verify declaration integrity and
  context only; they do not authenticate an owner, parse general DDL, verify deployed
  metadata, establish completeness independently, or execute SQL.

If an authorized owner can honestly supply an independent first-result declaration,
use version `sqlserver-owner-result-declarations/1`, origin
`owner-declared-result-shape`, identifier policy `ordinal-exact-no-aliases`, and exact
application repo/revision. Follow the current `SqlServerResultDeclarations` and
`SqlServerResultShape` contracts: four-part object identity, private relative file
and line evidence, first-result scope, explicit completeness/unknown reason, and
ordered columns. Use `unknown` when completeness cannot be established; do not
invent a complete column set or infer it from the same consumer being checked.
If no honest declaration exists, still report the native lineage test and mark
schema comparison unperformed with its precise missing input.

```text
dotnet <local-built-tracemap.dll> sqlserver-compiled-lifecycle produce-owner-schema --declarations <owner-declarations.json> --out <new-owner-schema.json>
dotnet <local-built-tracemap.dll> sqlserver-compiled-lifecycle compare-owner --declarations <owner-declarations.json> --schema <new-owner-schema.json> --bindings <new-lifecycle-bindings.json> --facts <new-scan/facts.ndjson> --repo <exact-application-repo> --revision <exact-application-sha> --out <new-private-comparison-directory>
```

Derive `sqlserver-compiled-lifecycle-bindings/1` from the exact current native facts
using the current evidence digest API and contract/test examples. Bind each selected
candidate digest, read/table/body/binary/offset/invocation and every execution's
command plus independently supported object identity. Never paste stale fact IDs,
guess digests or repurpose parameter/source-reader bindings. Preserve generator and
bounded-input provenance for any local helper/derived binding. Keep all owner
artifacts and reports private/local, including their hashes.

The owner report must retain version `sqlserver-owner-compiled-lifecycle-comparison/1`,
`schemaOrigin: owner-declared-result-shape`, partial coverage and conditional
limitations. A match/mismatch is conditional on the declaration. Keep SQL schema
ancestry separate from current application-written value origin. Clear/re-Merge,
guarded overwrites and failure snapshots must not become SQL-value, catch/rollback
or runtime claims.

## Return one consolidated result

Return a sanitized matrix with the public tool SHA, actual build result and tests-not-run status, private
input presence/build state, root fit, native ArrayList/custom-object/SqlParameter/
attachment/Fill/read legs, execution isolation, schema versus current-value origin,
optional owner-declared comparison status, baseline preservation, native gap IDs,
and the smallest remaining capability or input needed. Use opaque case labels;
keep private names, paths, SQL and hashes in the local receipt only.

State separately whether this proves the bounded real-input ArrayList lineage
pattern, whether the selected consumer/schema comparison was actually completed,
and whether later flow legs remain unsupported. Do not call an all-unknown report
acceptance or call the overall SQL effort, runtime, deployment or private application
complete. Do not change implementation merely to clear a test; report concrete
failures and evidence first.


## Failure-only Claude request: one diagnostic capsule and independent sample

Use this only if the new root-sliced metadata iteration still cannot produce the
selected native entry lineage. Do the work locally; do not ask Joe for more pictures,
private source pasted into chat, DLL uploads, hash transcription or another full scan.
Use the existing output and available local source/metadata inspection.

1. Privately identify the first failed root/callee/constructor stage. Link the exact
   current tool SHA and local body fact, metadata token, binary and receipt. Retain
   private names, paths, hashes and mappings only in the private evidence note.
2. Return one sanitized table with stable opaque aliases and the actual reason code,
   instruction count, declared-field count, requested character credits, used-before
   credits and cap for each reader, plus raw/call/value/object agreement flags.
   Distinguish missing peer, actual disagreement, field/instruction/text cap and
   retention-budget refusal. If a receipt is absent, say absent; do not invent it.
3. For constructor selection, report the admitted parameterless match count and
   unavailable-metadata count. Privately inspect whether the needed parameterless
   constructor exists and whether its metadata was retained. Other overloads are
   not automatically an ambiguity. Distinguish absent, dropped and multiple matches.
4. If metadata is retained and interpretation fails later, record the exact native
   gap and public framework API/opcode shape needed to reproduce it. Do not call it
   a body-retention failure or assume it is the last blocker.
5. Independently author the smallest synthetic C#/VB sample that reproduces that
   single failing property using neutral names/literals and public APIs. It should
   represent the relevant field/method counts, argument/constructor shape, root and
   callee relation or control operation. Include an opaque root label and expected
   native outcome/reason; distinguish executed observations from proposed expectations.
   Never copy, transliterate, or merely rename employer code/SQL. Include that independently authored neutral sample in this requested sanitized
   return only after checking it contains no employer identifiers/source/SQL. If
   that cannot be assured, retain it locally and return one precise abstract shape
   detail instead. This does not authorize public commits or publication.
6. If no faithful public-safe reproducer can be authored, identify one specific
   missing metadata fact or shape detail. Do not return two speculative causes or
   demand another broad investigation. A native positive on one leg does not close
   missing downstream transformation/schema/runtime legs.

Deliver once: the sanitized diagnostic table, the independently authored sample or
one precise missing detail, and separate entry-lineage/consumer/schema statuses.
Keep the exact evidence note and all employer artifacts on the work machine. Do not
execute the application/SQL or change implementation, ACK policy, accepted baselines,
push/PR/merge/deployment state. Test suites remain deferred unless Joe authorizes them.
