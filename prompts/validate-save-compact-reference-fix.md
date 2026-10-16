# Validate the bounded-reader fix using the existing fresh scan

Five-minute TOTAL limit. One CLI build and ONE handler requery. NO rescan,
preflight, context preparation, DLL search, test suite, provenance changes,
SDK pinning, application execution, SQL or HTTP execution.

The previous build was correct. The reproduced bug is in the bounded graph
reader: algorithm 1.6 discarded explicit-reference properties while compacting
CallEdge rows. Algorithm 1.7 preserves them. The existing fresh vb-semantic/0.10.2
scan already contains the required witness and must be reused unchanged.

Fast-forward the clean TraceMap codex/evidence-admission-hardening checkout.
Preserve unrelated changes; stop if they conflict. Build the CLI once with the
installed .NET 10.x SDK. Normal security patch differences are not a blocker.

Use the SAME completed fresh run from the previous acceptance, not the older
0.10.1 scan. Recover the last successful webforms-review requery-handler command
from its local receipt and repeat it once with a NEW sibling output directory.
Keep the exact handler, database-operations mixed scope and bounds unchanged.
--bundle must be the report directory recorded by that run, NOT combined.sqlite.
Do not edit checkpoints or overwrite old outputs. If the original invocation
cannot be recovered, stop rather than guessing parameters or rebuilding a scan.

Report:
- Tool HEAD, path algorithm 1.7, existing extractor 0.10.2, build/query exits,
  elapsed time, path count and truncation.
- Whether the exact M4 target connects to F1 through
  explicit-reference-member-candidate, with Tier3 and unchanged unbound
  provenance/originalBuildCorrespondence=not-established.
- Whether a mixed path reaches the retained SqlCommand.ExecuteScalar API.
  Command text/binding uncertainty must remain separate. Do not infer SQL,
  procedure identity, CommandType value or runtime execution.
- Whether the unrelated source database operation caller remains unreachable.

If paths remain zero, use only the emitted diagnostic graph and the existing
fresh index to check the bridge's exact conditions: context kind, dispatch,
complete target identity, raw-file hash and unique target count. Report actual
values only as sanitized matches/counts. The already-retained artifact-context
gaps are provenance annotations, not proof that this bridge was refused. Do not
start another scan or investigate unrelated gaps.

Keep private symbols, paths, hashes and artifacts local. Stop at five minutes.
