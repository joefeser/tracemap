# Issue #873 local validation receipt — 2026-10-15

## Identity and outcome

- Freshly fetched starting origin/dev: `25c4f6475db9b92b3f08dafafd5117a554e3020a`.
- Branch: `codex/webforms-entry-873`, isolated managed worktree.
- Frozen tested HEAD: `1266f4a3d5bcb0d29b286b975ac4077f46fda02e`.
- Implementation commits: `38b6e33db2682b1fb24cc640d0b83ca645bb03ae`,
  `bc563ba56d1eeec85dd21aa7782ef00b8b2a6582`; documentation: `1266f4a3`.
- macOS arm64, .NET SDK `10.0.302`, Debug/net10.0.
- Full suite completed: **4,367 passed, 1 skipped, 0 failed**, 25.6623 minutes.
- Focused suite: **597 passed, 0 skipped, 0 failed**.
- Relevant Python diagnostics: **60 passed**; artifact-validator tests: **7 passed**.
- CLI sample scan/combine/reports and pinned static corpus checks passed.
- No tracked file or HEAD changes during the successful frozen runs. No overlapping
  solution/tool builds. The subsequent receipt/state/checklist commit changes only
  documentation; it is not represented as another tested binary build.

## Commands and observed results

From the worktree root, before runtime freeze:

```sh
dotnet build src/dotnet/TraceMap.sln --no-restore -warnaserror
dotnet build scripts/diagnostics/WebFormsStaticCorpus --no-restore -warnaserror
```

Both succeeded with zero warnings/errors. Then:

```sh
dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --no-build --no-restore --filter 'FullyQualifiedName~HttpEntryBoundaryRegressionTests|FullyQualifiedName~OutboundHttp|FullyQualifiedName~ExplicitVisualBasicContextTests|FullyQualifiedName~SourceMetadataReconciliationTests|FullyQualifiedName~CombinedDependencyPathTests|FullyQualifiedName~GroupedCompiledPathHandoffTests|FullyQualifiedName~FrameworkSignatureCandidateTests|FullyQualifiedName~CompiledBaselinePrewalkTests|FullyQualifiedName~LegacyWebFormsExtractorTests|FullyQualifiedName~AppCodeRegistrationMatrixTests|FullyQualifiedName~VisualBasicWebFormsCompositionTests|FullyQualifiedName~WebFormsGapFamilyRegressionTests|FullyQualifiedName~LegacyWebFormsEventIdentityTests|FullyQualifiedName~RazorSemanticModelBindingTests' --logger 'trx;LogFileName=873-frozen-focused.trx'
dotnet test src/dotnet/TraceMap.sln --no-build --no-restore --logger 'trx;LogFileName=873-resumed-full-frozen.trx' --logger 'console;verbosity=normal'
python3 scripts/validation/test_inspect_retained_il_body.py
python3 scripts/validation/test_retained_gap_inventory.py
python3 scripts/validation/test_http_framework_inspection.py
python3 scripts/validation/test_sql_wrapper_report.py
python3 scripts/test_validate_adapter_artifacts.py
./scripts/check-private-paths.sh
git diff 25c4f647 --check
```

Focused duration 2m43s. The four diagnostic scripts passed 20/17/6/17 tests.
Artifact-validator tests passed seven tests and emitted a ResourceWarning about an
unclosed SQLite connection; it was not a compiler/analyzer warning or test failure.
The private-path and diff guards passed.

The full suite's sole skip was
`IlCommandBindingExtractorTests.Deep_projectless_Windows_ASPNET_publish_preserves_exact_chain_and_command_binding`:
“Authentic ASP.NET Framework publishing requires Windows.” This remains unverified
on this host and is not counted as a pass.

CLI commands (all used the already-built DLL):

```sh
dotnet src/dotnet/TraceMap.Cli/bin/Debug/net10.0/tracemap.dll scan --repo samples/http-entry-boundaries --out /tmp/tracemap-873-resumed-cli-scan
dotnet src/dotnet/TraceMap.Cli/bin/Debug/net10.0/tracemap.dll combine --index /tmp/tracemap-873-resumed-cli-scan/index.sqlite --out /tmp/tracemap-873-resumed-cli-combined.sqlite --label webforms
dotnet src/dotnet/TraceMap.Cli/bin/Debug/net10.0/tracemap.dll paths --index /tmp/tracemap-873-resumed-cli-combined.sqlite --out /tmp/tracemap-873-resumed-cli-paths.json --format json --to-surface http-client
dotnet src/dotnet/TraceMap.Cli/bin/Debug/net10.0/tracemap.dll paths --index /tmp/tracemap-873-resumed-cli-combined.sqlite --out /tmp/tracemap-873-resumed-cli-repeat.json --format json --to-surface http-client
dotnet src/dotnet/TraceMap.Cli/bin/Debug/net10.0/tracemap.dll paths --index /tmp/tracemap-873-resumed-cli-combined.sqlite --out /tmp/tracemap-873-resumed-cli-paths.md --to-surface http-client
```

Required scan artifacts all present: manifest, facts, SQLite, Markdown, analyzer log.
Manifest commit equals frozen HEAD; BuildStatus Succeeded, Level1SemanticAnalysis,
176 facts. New facts: six containment, four declarative-property references, three
framework lifecycle candidates. The report retained 18 inventory rows including
five unresolved-entry gaps, labeled partial; call report coverage is ReducedCoverage.
Two JSON report files matched byte-for-byte. The selector-free query retained no
paths, as expected without an admitted root. Additional exact-symbol CLI queries
selected the retained Tier1 HostPage method declarations using `--from-symbol`,
`--exact-from-symbol`, `--to-surface http-client`, `--max-depth 16`: ExplicitRead
retained one HTTP path; Page_Load retained none. No new entry edge kind entered
call paths. Ordinary/bounded parity, repeatability, truncation, privacy omission and
SQLite/combine round trips are separately asserted by the focused/full suite.

Pinned third-party checks used the built trusted diagnostic DLL, not dotnet run or
any downloaded build script:

```sh
dotnet scripts/diagnostics/WebFormsStaticCorpus/bin/Debug/net10.0/WebFormsStaticCorpus.dll /tmp/tracemap-873-kaliko 16acb2b411df16b9f16fa6849839959c7343cdbd
dotnet scripts/diagnostics/WebFormsStaticCorpus/bin/Debug/net10.0/WebFormsStaticCorpus.dll /tmp/tracemap-873-csrf 0ec5451b27b3a870d8c5766630ce658ef4401409
```

Both checkouts were clean. Kaliko: 107 inventoried files, 619 C# syntax facts,
780 WebForms facts, 235 gaps, seven new containment relationships. CsrFence:
21 files, 189 syntax facts, 24 WebForms facts, no new entry relationships.
Neither establishes semantic property/lifecycle proof. URLs, exact licenses and
source-line evidence are in `docs/validation/WEBFORMS_HTTP_ENTRY_PUBLIC_CORPUS.md`.
No third-party build/restore/hosting, HTTP endpoint request or database execution.

## Runtime freeze and local logs

Before/after manifests under `/tmp/tracemap-873-resumed-runtime-{before,after}.json`
were byte-identical. They contain 593 relative-path/SHA-256 rows for DLL, EXE, SO,
dylib, deps.json and runtimeconfig.json files under CLI, test and diagnostic
`bin/Debug/net10.0` directories. The manifest generator was
`/tmp/tracemap-873-freeze.py`; the bounded input is compact sorted-key JSON of those
relative-path/hash rows. These manifests contain no source code or private paths.

- Generator SHA-256: `c6416b71d34b7d5985f1568390dbe0be7a7befd3e02ddabc03d9a042e0c2152c`.
- Bounded-input SHA-256: `e1ad82f8447af91dcd5b45d9d5aaa3b5844a6635b37f7c1b7876982580885578`.
- CLI DLL: `9411fac4d1658c75029fc5ad74dfc13e67d84796d4bc62508d1591c7926b2835`.
- Core DLL: `599742d81a14d2caf2bd1e063dab241585f74d2b12e41212163a20fa109c527a`.
- Reporting DLL: `dd49c949b7c13182e57a82ae5ad6450280cc1a47c38a7876bbbb06b1da1ba233`.

Local logs are ephemeral; this receipt is the durable summary. SHA-256:

| /tmp log basename | SHA-256 |
| --- | --- |
| tracemap-873-resumed-build.log | `51359491a10b020a9cb1fd8b9730611d4b33e8f7f6f5c20f6dfc286ea59bcf2c` |
| tracemap-873-frozen-focused.log | `4b56353d9ff5a066adbe317e1700d2a80b92713451b365f317a6322cfca838cc` |
| tracemap-873-resumed-full-frozen.log | `95ad4bef8213f04e95de2b749f43b54250f1d78677666aac615383c4a232b384` |
| tracemap-873-resumed-python.log | `2247de8a8cb5e29da3334a5c1ceb0c4afd97b582bae3374608416ef1137c2b28` |

TRX files remain in the ignored test `TestResults` directory. No raw logs or scan
artifacts are added to the repository.

## Failed, cancelled and retried development runs

These are historical failures, not passes:

- Initial regressions exposed VB project/global-name resolution, root-relative Src
  handling, ambiguous registrations and framework member ownership; fixes retained
  the existing positive/negative protections.
- A broad Python discovery invocation produced 32 SQL command-text setup errors
  because CONFIG was unset. Correct targeted diagnostic commands above passed.
- A prefreeze broad .NET run was cancelled during review. A later focused run was
  596/597: a new test incorrectly expected bounded input overflow to throw; it was
  corrected to assert the actual ReducedCoverage/truncated/gap response. HTTP18/18
  then passed.
- Full run at `38b6e33d` was cancelled after 3,184 observed passes when review found
  receiver shadowing. It was not a full pass. A new inherited-shadow fixture first
  failed as expected, then passed after `bc563ba5` refused the conflicting receiver.
- The first resumed focused run returned 595/597. A documentation commit changed
  HEAD during repeated scans, invalidating identity comparisons in
  SourceMetadataReconciliationTests and OutboundHttpCoverageTests. No assertions
  were weakened. The successful 597/597 run and full run used frozen HEAD/files/
  binaries and passed those same tests.
- Early CLI experiments included an invalid positional scan invocation and invalid
  combined report format/view assumptions in new tests; corrected public commands
  and separate Markdown/JSON reports passed. No production compatibility was
  inferred from those failed invocations.

## Boundaries and handoff

Reviewed the final diff against the recorded dev baseline. Existing regressions
remain intact; only current extractor-version assertions were updated where needed.
The implementation is ready as local commits for owner review/publication approval.
No push, PR, external reviewer, merge or GitHub workflow mutation occurred.

Fresh scans are required for legacy-webforms/0.14.0, vb-semantic/0.10.5 and
csharp-semantic/0.22.2 evidence. Requery cannot recover absent witnesses from old
scans. No call-path algorithm change. `legacy-flows` omits the new local inventory.
Supported grammar and unresolved boundaries are in the coverage matrix. External
base members, inherited properties, arbitrary expressions, dynamic naming scopes,
DLL-only controls and missing C# PropertyDeclared facts remain outside this slice.

Not run: authentic Windows ASP.NET publishing (explicit skip above), Windows/private
acceptance, other pinned language-adapter corpora, JVM/Swift/TypeScript/Python-adapter
build/smokes, and million-row opt-in memory benchmarks. They are separate platform/
adapter/scale gates, not implied by this .NET change. The two required public static
corpora were inspected; their apps were never built or executed. No runtime
reachability, exhaustive website coverage or private-application acceptance claim.
