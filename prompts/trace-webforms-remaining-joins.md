# Return one handoff for the original clicks and the larger site

Use the 0.35.0 implementation pinned in verify-webforms-registered-member-post-once.md. If that dispatch already ran at the pinned implementation, reuse its public results and private index. Otherwise follow its public gate and existing authorization for at most one private static scan of the original target. If the private index is unavailable and a scan is not authorized, report that dependency and complete the public portion.

From the same index, work through the following questions in one session. Keep exact IDs, source spans, extractor versions, commit identities and queries in the private working notes. For the shareable answer use synthetic examples and stage labels.

1. Trace each click forward by supporting IDs through its partial handler, service call, alias and truncation gaps, and observed registration. Record which observations the bounded lookup retained.
2. Starting at that registration, trace its definition and any POST by supporting IDs. Independently check whether the configured mount and literal target identify a unique handler declaration. Starting at that declaration, inspect any already indexed backend call or SQL evidence relevant to the original trace. Record which relations have persisted joins and which come only from source inspection. Keep observations from other scans separate.
3. Continue from independently identifiable evidence on either side of a missing join. For each missing relation, inspect its extractor predicate and the local source shape. Record a minimal invented fixture, one near-miss that must stay unresolved, and the facts or gaps expected from each. Group failures caused by the same predicate. Mark boundaries that require runtime evidence or unavailable inputs.
4. Return one short Markdown handoff with the furthest ID-backed stage for each click, a table of the missing joins, and the smallest set of synthetic regressions that exercises them together. If available evidence cannot answer a question, state what input would change that result. Do not stop at the first failed predicate.

## Larger site

Also assess the larger C# site identified by the operator on the work machine. Use its confirmed repository root, commit, project selection and applicable mount mapping. Keep its output in a separate directory. If the local workspace leaves multiple plausible targets, report that ambiguity rather than guessing. Reuse an existing index only if its commit, extractor and scan settings match. Otherwise run one authorized static scan of this target. Complete the original-target analysis even if this target is unavailable.

Use the larger site's own evidence to select up to two representative UI-to-backend paths. Follow exact fact IDs as far as supported. Record the first missing join per path and whether it reproduces a gap from the original site. If this site uses different client or server technologies, trace the entry points actually present. Report scan wall time when measured, analysis level, file and fact counts, and elapsed time for the representative SQLite queries. Mark unavailable timings explicitly when reusing an index.

Include both targets in the same sanitized handoff with separate results. Keep application names, local paths, source identifiers and receipts out of public Git.

This is a read-only diagnosis. Keep private source, IDs, paths, query results and screenshots on the authorized machine. No source patches, extra scans, site execution, external transfers or private-worker runs.
