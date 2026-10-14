# Correct the retained-handler bundle argument; three-minute total budget

Start a three-minute wall-clock timer before any work. Stop at that deadline,
terminate only commands you started, and return the last completed step. Do not
expand this into an investigation, full test run, scan or context-preparation run.

Record tool HEAD and reuse the already built CLI containing the
`database-operations` selector. Do not rebuild or change the checkout. Use one
shell consistently; do not send PowerShell timing syntax to Bash. Reuse existing
run-002 and its retained combined index. No source, context, references, budgets,
original reports or receipts may be changed.

Read the run's latest completed checkpoint and its `reports.reportAttempt`.
Compare the previous command's `--bundle` with the absolute path formed from the
run directory plus `reports.reportAttempt`. The bundle must be that original
completed report directory, NOT a previous requery output directory. Keep the
actual private paths local.

If the bundle already matches, stop and report that fact. Do not retry, bypass
hash checks, edit checkpoints, or attribute this error to a binary-version
mismatch. In the current implementation,
`HANDLER_REQUERY_COMPLETED_CONTEXT_INVALID` checks bundle-path equality and the
completed checkpoint digest; retained requery uses the original recorded runtime
hash, not the current binary's hash.

Only if the bundle argument was wrong, run `webforms-review requery-handler` ONCE
with the corrected `--bundle`, the same exact Save-handler selection, run and
limits as the prior requery, and a NEW output directory. Preserve all existing
receipt validation.
Use its default grouped-path view: omit `--view method-graph`, `--surface-name`
and `--traversal-scope compiled-il`. The new default must print
`handlerQuery.terminalScope=database-operations`. It includes source `sql-query`
and `sql-persistence` plus compiled `database-api`; evidence tiers stay unchanged.

Do not rescan, recombine, execute application methods, SQL or HTTP, change the
approved roster, rerun the matrix, or inspect unrelated handlers. The local
synthetic clean/partial/missing/ambiguous matrix is already run by the developer.

Return only tool HEAD, elapsed time, bundle mismatch yes/no, query exit status
(or not run), reported terminal scope, number of paths,
terminal-kind counts, path evidence-tier combinations, truncation status and
whether an existing source database operation is reached. Keep all private
identities, source, hashes and paths local. If paths remain zero or the command
fails, return its sanitized reason and stop; do not launch another command to
investigate. Zero does not establish missing source or compiler failure.

The prior database-api-only result and this aggregate query have different
denominators. Any increase is newly included retained evidence, not new extraction,
an all-semantic chain, runtime execution, or proof of original-build equivalence.
