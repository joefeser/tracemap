# Work-machine test: compiled ArrayList SQL parameter lifecycle

Continue the offline TraceMap test on the work machine. Test the actual application
pattern using the published implementation; do not implement another speculative
extractor or repeat the previous unsupported-ArrayList diagnosis without checking
the new native evidence.

## Inputs and revision

Discover these inputs locally. Joe does not need to fill in a worksheet or know a
private acceptance folder. Use the current work-application context and existing
checkouts/build outputs; ask only when the application checkout cannot be identified
or multiple plausible target flows need a human choice.

```text
VALIDATED_IMPLEMENTATION_COMMIT = b4526fcec6b7ab5e814a1f55051e4229bff9139c
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
and record it as EXPECTED_TOOL_COMMIT. Verify that it descends from the validated
implementation commit above and that subsequent changes are documentation only.
If implementation, tests, fixtures or rules changed, inspect their validation before
claiming the recorded gate applies. Use a clean tool checkout at the verified SHA
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

This prompt authorizes bounded offline inspection, tool build/tests, a new native
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
dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --no-restore -c Debug -warnaserror --filter "FullyQualifiedName~IlSqlLifecycleExtractorTests|FullyQualifiedName~SqlServerOwnerResultEvidenceTests|FullyQualifiedName~IlSqlObjectPathExtractorTests|FullyQualifiedName~IlSqlShapeExtractorTests|FullyQualifiedName~CompiledSql"
```

The focused command should pass 87 tests. Record actual exit codes/results and local
CLI/Core/Reporting hashes. A work-machine rebuild need not have the same byte hashes
as the macOS build; pin the actual bytes used. If a required tool is missing, follow
AGENTS.md's Homebrew/path discovery where applicable before reporting it missing.
Do not launch another full suite just to repeat the already recorded unchanged-code
gate; use the focused gate and the real-input checks below unless new failures or
implementation changes justify broader validation.

## 2. Check whether the actual compiled flow fits

Inspect the actual compiled bodies and source correspondence without executing them.
The supported lifecycle pattern is:

- Exact signed framework identities for `mscorlib` 4.0.0.0 `ArrayList` parameterless
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
- The root must fit the implemented fresh-instance lifecycle entry contract: an
  inspected unique constructor, nonvirtual/nongeneric instance method returning
  `Object`, with no arguments or one Boolean argument. Both Boolean alternatives
  are analyzed. Existing property-held DataSet/helper/consumer restrictions still apply.

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
dotnet <local-built-tracemap.dll> scan --repo <PRIVATE_SOURCE_ROOT> --out <new-private-scan-directory> --il-body-evidence --compiled-input <caller.dll> --compiled-input <wrapper.dll>
```

Inspect the actual `ManagedIlSqlLifecycleCandidate` / `AnalysisGap` facts under
`dotnet.compiled.il-sql-lifecycle.v1`, schema `il-sql-lifecycle/1`, Tier3.
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

Return a sanitized matrix with the public tool SHA, actual tool-test result, private
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
