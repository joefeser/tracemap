# Main/dev reconciliation — 2026-10-07

## Scope and pinned history

Bring the current public site work back into the engine integration lane without
resetting either protected branch. Work is on `codex/integrate-main-into-dev`,
created from the fetched main tip, with a PR targeting `dev`.

| Role | Commit |
| --- | --- |
| Main source / integration first parent | `7f026f5a9b59f9f3f2c1e203f1e8b001fdddc261` |
| Dev target / integration second parent | `f6149cd9d1bade03226a9d3070e2fcb58e9d5f74` |
| Shared merge base (#801 promotion) | `d76358f663ce40532fe0954ca9888612d51a4121` |
| Local integration merge | `4f1a4b03aabc488197af121777d1addc9bb066a0` |
| Unmodified union tree at that merge | `6e16cab048b19fdfce67bf03aaf57947279ed8a9` |

`git rev-list --left-right --count <main>...<dev>` reports 50 main-only and
71 dev-only commits. These counts include merge and implementation commits;
they are not counts of independent features. `repo-sync-promote plan` correctly
refused direct main-to-dev promotion with `PROMOTION_HISTORY_DIVERGED`. The
owner-requested integration merge reconciles both histories without a reset,
force push, or direct protected-branch update.

## What diverged and what was preserved

| Requirement | Implementation / test evidence | Result and remaining gap |
| --- | --- | --- |
| Preserve main-only work | 64 paths changed since the merge base: site/spec/site-validation work from #810–#817 and #819 | All imported unchanged in the integration merge. Site claims remain bounded to their named historical revisions and concept/demo/shipped levels. |
| Preserve dev-only work | 87 paths changed since the merge base: #803 Web Forms repairs, #804 central package pins, #818 produced-package declarations, and #820–#845 compiled-evidence work | All retained unchanged in the integration merge. Production changes include the independent optional-parameter cross-check; the later language matrices mostly add fixture and regression evidence. |
| Detect an overwritten file | Compare both changed-path sets; independently reconstruct the expected union from three `git ls-tree -r` inventories | Zero intersecting changed paths; conflict-free merge; all 2,717 tracked entries match the expected union exactly. This is tree preservation, not proof of every semantic interaction. |
| Audit the suspicious reset | Compare #802 commit `99b440dc` with the #801 merge-base tree | Both trees are `1163de1ea0bc4f57cd762ebc3a85ee014bc6fff2`. The reset PR did not discard functionality relative to the shared promotion. Its first-parent diff alone is misleading. |
| Detect duplicated implementation across the split | Main-only paths contain no engine implementation; `git cherry <main> <dev>` has zero patch-equivalent (`-`) non-merge commits | No evidence that the .NET sequence reimplemented main-only engine code. This comparison does not measure every historical engineering choice or prove every test was necessary. |
| Correct catalog drift | `ProjectFileReader.ReadProducedPackages` uses `packageVersion ?? plainVersion`; `ReadProducedPackages_packageversion_outranks_version_nuget_semantics` pins a conflicting-property example | Corrected rule-catalog prose to say PackageVersion outranks Version. Implementation and test already agree; no engine behavior change. |
| Repair the public positive endpoint fixture | The demo failed its connected-path assertion. Sample build reproduced CS0246 for ASP.NET attributes; restoring assets alone did not fix it. Semantic plus syntax fallback route facts produced AmbiguousMatch, which correctly withheld a cross-source path | Add the real Microsoft.AspNetCore.App framework reference and MVC import. Restore sample assets explicitly in both public demo and combined-path smoke scripts. The unchanged full demo passes: 8 paths / 24 reverse results. No endpoint identity, ambiguity or partial-coverage rule was weakened. |

The repeated language work is substantially acceptance evidence around existing
behavior, not a claim that each PR shipped a new extractor. Source symbols,
compiled members, PDB occurrences, original IL, rewritten IL and runtime evidence
remain distinct. This reconciliation does not close #759/#766/#767/#768/#769,
prove F# source extraction, or turn public fixture coverage into private-corpus
acceptance. No fixture binaries, customer sites or restricted corpus were executed.

## Validation

The first full .NET run was cancelled after two Web Forms failures while separate
smoke commands were rebuilding the CLI. One refusal was
`WEBFORMS_EXECUTION_PREFLIGHT_CHANGED_OR_TOOL_MISMATCH`; the other occurred
before mutation of the partition fixture. All six selected cases then passed in
an isolated `--no-build --no-restore` rerun. Concurrent tool rebuild/resource
interference is a suspected cause, not a proven product regression or a clean
full-suite result. The complete isolated rerun and final-head CI/ACK results are
recorded in the integration PR before it is declared ready; this committed audit
does not substitute for those live delivery gates.

Commands run in the combined integration worktree, with generated output outside
tracked source. `dotnet test` builds the solution; `-warnaserror` rejects introduced
compiler/analyzer warnings.

| Command / check | Result |
| --- | --- |
| `dotnet restore src/dotnet/TraceMap.sln --locked-mode` | Passed |
| `dotnet test src/dotnet/TraceMap.sln --no-restore -warnaserror` | Complete isolated run required; see the integration PR for its final result |
| `dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --no-build --no-restore --filter 'FullyQualifiedName~Invalid_partition_inventory_never_creates_a_run_or_exposes_input_paths\|FullyQualifiedName~Real_native_pipeline_completes_and_completed_continue_only_verifies_retained_reports'` | 6 passed |
| `JAVA_HOME=<Homebrew-Java-21> ./scripts/demo-public.sh <temporary-output>` | Original and restore-only runs failed the connected-path assertion; real-framework fixture and clean-assets runs passed the complete demo (8 paths / 24 reverse results) |
| `./scripts/smoke-combined-paths.sh <temporary-output>` | Passed with sample assets initially absent: connected path, bogus-selector negative, repeat-byte equality, reverse provenance and privacy assertions |
| `bash -n scripts/demo-public.sh scripts/smoke-combined-paths.sh` and `node scripts/demo-public-assert.mjs self-test` | Passed |
| `npm test --prefix site` | 1,251 passed; zero failures/skips |
| `npm run build --prefix site` | Passed |
| `npm run validate --prefix site` | Passed: 120 HTML files, 4,040 internal references, 119 sitemap URLs and bounded evidence-row validators |
| `dotnet src/dotnet/TraceMap.Cli/bin/Debug/net10.0/tracemap.dll scan --repo samples/modern-sample --out <temporary-output> --restore` | Passed: 27 facts, Level1SemanticAnalysis |
| `python3 scripts/validate-adapter-artifacts.py <temporary-output>` | Passed |
| `python3 scripts/test_validate_adapter_artifacts.py` | 7 passed |
| `PYTHON_BIN=<temporary-venv-python> ./scripts/smoke-python-endpoints.sh <temporary-output>` | Passed; one matched endpoint, with expected reduced coverage retained |
| `scripts/check-private-paths.sh` | Passed |
| `git diff --check` | Passed |
| `node scripts/kiro-review.mjs --self-test` | Passed |
| `npm ci && npm run check` in `src/typescript` | 258 tests passed across 11 files; build/check passed |
| Temporary venv, `python -m pip install -e "src/python[dev]"`, `python -m pytest src/python/tests` | 64 passed |
| `JAVA_HOME=<Homebrew-Java-21> gradle -p src/jvm test` | 36 passed; pre-existing ConfigExtractor deprecation note remains in the unchanged JVM tree |
| Playwright Chromium, 1440×1000 and 390×844 | Four Web Forms routes returned HTTP 200 with their expected heading and no horizontal overflow; desktop/mobile proof-page screenshots visually inspected |

Browser routes: `/webforms/`, `/webforms/local-demo/`,
`/webforms/review-workbench/`, `/webforms/source-plus-compiled-proof/`.
Screenshots and scanner outputs are local ignored/temporary artifacts, not newly
published machine-readable evidence. No new artifact schema was introduced; existing demo generator/input digest
checks still apply to its temporary output.

The demo failure also reproduced with the unchanged connected-path query after
restoring the sample; it was not a simple missing-assets failure. The endpoint fixture and
demo/combiner implementation are unchanged between the shared base and both
pinned tips, so this is a pre-existing positive-fixture defect. Broken-input
coverage remains in the dedicated fixtures; no runtime server was launched.

Full final-head public CI and live ACK review remain PR delivery gates. Local
TypeScript/Python/JVM suites also passed. Pinned OSS smoke reruns are deferred
because the integration preserves adapter trees exactly and adds no adapter
behavior. Public adapter CI remains required. Authentic Windows publishing, MSVC/C++/CLI
and authorized private-corpus acceptance are separate host/access gates; no claim
of running them locally is made.

## Host and access limits

Local checks use macOS, .NET 10, Node/npm, Python in a temporary virtual
environment and Homebrew Java 21/Gradle. Browser checks use Chromium. No paid
workers, Windows hosts or private inputs are dependencies of this integration.

## Next step

Merge the reviewed integration PR into `dev` using a merge commit to retain both
ancestries. Do not squash away the reconciliation ancestry. A later dev-to-main
promotion remains a separate reviewed action; this branch does not merge either
protected branch. Resume bounded evidence work only from the reconciled dev tip.
