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

Within the remaining time, inspect the selected handler, its Save callee and
their retained outward connections toward App_Code/database candidates. Keep
exact identities locally. Report one evidenced blocking boundary if available:
caller/callee anonymized labels, source-index identity equal/different,
edge direction/kind, rule/tier, and associated gap reason. Distinguish an absent
retained edge from an edge present but not used by the grouped traversal. Do not
infer a missing connection merely from Tier3 evidence. If a terminal is present
in the unfiltered graph but no accepted path exists, report its witness edge
kinds/tiers for a traversal reproduction; do not claim runtime reachability.
If the graph cannot establish the boundary, say so rather than invent a cause.

Return only HEAD, elapsed time, exit status, path count, terminal scope,
diagnostic emitted yes/no, diagnostic work/cutoffs, and the bounded finding.
Keep private paths, symbols, source, hashes and raw artifacts local; return
sanitized structure only. A failure ends this run. No follow-on investigations.

This change adds diagnostics, not a claimed fix for private database reachability.
Zero paths remain zero paths. Partial analysis remains partial.
