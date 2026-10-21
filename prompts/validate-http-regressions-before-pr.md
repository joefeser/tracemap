# Preserve HTTP acceptance fixes before opening a PR

Use public synthetic fixtures only on codex/http-evidence-regressions. Do not open
or merge a PR in this task. Do not access work-machine artifacts, photos, private
sources or databases. Do not host sample apps or execute their HTTP/recursive code.
Run only the repository's test/scanner workflows and normal compile-time restore.

Read AGENTS.md and docs/VALIDATION.md. Record full HEAD and dirty state; preserve
unrelated edits. Do not modify HEAD while tests are running. Results from an older
head or interrupted run do not qualify the final head. Any requested later code
repair needs a new relevant test run and the final full gate.

## Coverage that must remain protected

| Behavior | Local regression |
| --- | --- |
| Native HTTP selector, exact source root outside prior report roots, four identity mismatch controls, unchanged accepted inputs, source-root claim label | WebFormsReviewExecutionTestsHandlerHttp |
| Event to helper to constructor to two independently identified HTTP terminals, cycle truncation and shallow negative control | Shared_event_entry_retains_two_independent_terminals_despite_cycle_truncation; samples/http-entry-dual-terminal |
| Real signed-framework lifecycle candidate versus same-name fake and intermediate override, no fabricated entry calls | Real_webforms_framework_keeps_override_body_and_host_entry_separate; samples/http-entry-boundaries |
| Private inventory survives legacy roots in ordinary/bounded readers; shareable legacy views still omit it | Real_webforms_framework_keeps_override_body_and_host_entry_separate |
| Candidate, empty and unavailable HTML inventories; escaping; unchanged paths and handoff round-trip | Entry_inventory_is_visible_without_becoming_a_call_edge |
| Existing C# property/inheritance and VB boundary behavior | HttpEntryBoundaryRegressionTests; samples/http-entry-inheritance-csharp |

Run the focused gate:

```text
dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --filter "FullyQualifiedName~WebFormsReviewExecutionTestsHandlerHttp|FullyQualifiedName~HttpEntryBoundaryRegressionTests|FullyQualifiedName~GroupedCompiledPathHandoffTests" -c Debug -warnaserror --nologo
```

Then run the final-head full gate without changing HEAD or concurrently rebuilding
the same worktree:

```text
dotnet test src/dotnet/TraceMap.sln -c Debug -warnaserror --nologo
```

Keep test logs outside Git. If a required tool is unavailable, follow AGENTS.md's
tool discovery instructions; do not silently replace the workflow. If a test fails,
record its exact name and failure before diagnosing. Do not weaken evidence/tier/
identity assertions, remove a negative control, clear truncation, increase limits
or classify fixtures as skipped merely to obtain green results. Report unexpected
warnings and skips. Verify final HEAD still matches the tested head.

Return exact tool commit, focused/full totals, elapsed times, failures/skips, clean
or dirty state, and any remaining coverage gaps. State whether the branch is ready
for PR review, not merge-ready. Private acceptance is complementary evidence, not
a substitute for this gate. Do not copy private findings into a public PR.
