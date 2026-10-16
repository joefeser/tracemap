# One retained-handler query with zero-path diagnostics

Five-minute TOTAL wall-clock limit, including update/build and reading results.
Stop at the deadline and terminate only commands you started. Use one shell
consistently; do not execute PowerShell syntax through Bash.

Fast-forward the clean `codex/evidence-admission-hardening` tool checkout without
discarding unrelated changes. Record HEAD and build the CLI using the installed
.NET 10 SDK. Security patch differences are not a failure. Do not run tests here.

Reuse the existing completed run and the exact Save-handler selector from the
successful 85-second query. Read its completed checkpoint: `--bundle` MUST be
the run directory plus `reports.reportAttempt`, not the combined.sqlite file
and not any prior requery output directory. Use a fresh output directory.

Run `webforms-review requery-handler` ONCE with the same bounds and default
grouped-path view. Omit `--view method-graph`, `--surface-name` and
`--traversal-scope compiled-il`. Expect terminal scope `database-operations`.
Do not scan, combine, change context/source membership, references, budgets,
receipts, original reports or application files. Do not execute SQL or HTTP.

If paths are zero, the new receipt may name `zeroPathDiagnosticReport`. Read
that local JSON (and its HTML if useful), not the full source tree. It is an
unfiltered outgoing graph generated from the SAME in-memory graph with only
unused traversal work. Its cutoff status is independent of path truncation.
No diagnostic means the allowance was exhausted or no root was selected; stop
and report that, without increasing limits or launching another query.

Read ONLY `graph.sourceDatabaseAudit` in that JSON for the next finding. The
tool now performs the exact joins; do not reconstruct them through ad hoc
source searches. Return its operationFactCount, projectedOperationCount,
reachableOperationCallerCount, reachableOperationTerminalCount,
reachableNamedCallFactCount and rowsTruncated. Summarize counts by Operations
state and how many NamedCalls rows have zero operationFactIds. Named-call rows
are fact counts, not unique call sites; call names alone are not API proof.

If operation facts exist but callers are unreachable, report that identity/
connectivity boundary. If reachable named calls have no exact operation fact,
report the public API spelling, evidence tier, and sanitized target type/
assembly category when already present in the report. Do not claim these names
prove a supported API or an extractor defect. If facts lack projected terminals
or caller edges, report that explicit state. Keep all private identities local.
Do not attribute missing SOURCE operation terminals to external IL admission.
No source-context expansion, second query, DLL search, or follow-up investigation.

Return only HEAD, elapsed time, exit status, path count, terminal scope,
diagnostic emitted yes/no, diagnostic work/cutoffs, and the audit summary above.
Keep private paths, symbols, source, hashes and raw artifacts local; return
sanitized structure only. A failure ends this run. No follow-on investigations.

This change adds diagnostics, not a claimed fix for private database reachability.
Zero paths remain zero paths. Partial analysis remains partial.
