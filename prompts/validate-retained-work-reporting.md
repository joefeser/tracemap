# Validate retained database and HTTP reporting

Use the existing local baseline inventory from `locate-accepted-work-baseline.md`.
Validate reporting on `codex/http-evidence-regressions` against the accepted
retained evidence. Do not rediscover folders or run a fresh extraction.

Budget: 15 minutes total, one CLI build, one database requery, and at most one
HTTP query. Stop at the budget with partial results; do not launch follow-up
investigations. Use the shell's native syntax, not PowerShell expressions in Bash.

## Preserve and verify the inputs

1. Read the existing inventory note locally. Record current tool branch, full
   SHA and dirty status. Require this branch and the reporting implementation
   commit `f05ff5071` as an ancestor. Do not pull, switch, reset, clean, or remove
   the untracked directory. Stop if tracked changes affect the tool being built.
2. Follow the inventory's accepted index, report, receipt and run references.
   Recalculate SHA-256 for the index and each comparison artifact with a recorded
   digest; compare against its corresponding receipt field, not an unrelated
   generator digest. Record unavailable digests as unverified. Stop on mismatch,
   ambiguous baseline, missing input, or concurrent artifact changes. Keep the
   original scan generator, accepted query generator and new generator distinct.
3. Build only `src/dotnet/TraceMap.Cli/TraceMap.Cli.csproj` in Debug with
   `dotnet build --nologo -c Debug`. No application build, full test suite, scan,
   combine, preflight, context regeneration, dependency discovery or schema work.
   Record exit status and warnings. Use this checkout's built CLI, not a global
   installation or a binary from another TraceMap folder.

## Repeat the accepted database query

4. Recover the accepted invocation from local receipts and the current committed
   command implementation/help. Preserve its root identity, surface, traversal
   scope, depth, path/frontier/work limits and evidence input. Record the new
   algorithm version separately; do not force the old implementation version.
   Use a new output directory outside Git and outside the original run, bundle,
   and inputs. For `webforms-review requery-handler`, `--bundle` is the retained
   report/bundle directory, not the SQLite filename.
5. Run once. If current tooling cannot reproduce the settings or rejects the
   completed context, stop with CAPABILITY MISSING or CONTEXT REJECTED and the
   categorical reason. Do not rewrite checkpoints, bypass hashes, silently use
   another command, loosen limits, or rescan to make it pass.
6. Compare ordered method chains, terminal kinds, supporting fact/edge references,
   representative rule IDs, tiers, file/line spans, coverage, provenance gaps,
   exact-chain counts, variant counts and truncation. This is the same retained
   index, so supporting evidence should remain traceable to it. New output and
   generator identities are expected; do not normalize away substantive changes.
   Representative selection may change to a deterministic declaration/path
   witness, but must match an actual retained fact at its original tier and
   location. A terminal's tier must not upgrade the whole route.
   Counts alone are not acceptance. If enumeration is truncated, compare the
   retained routes but do not claim exhaustive equality or absence of other paths.

## Verify H1 and establish its first report if supported

7. Reverify the inventory's exact three retained H1 facts: event binding, caller
   edge, and HTTP operation, including symbol joins, rule IDs, tiers and locations.
   Use the same index read-only; inspect schema before querying. Do not substitute
   another similarly named handler or splice facts across scans.
8. H1 has no previous query receipt: any new report is a FIRST HTTP BASELINE, not
   a report-to-report regression result. Inspect committed CLI help/source for
   an existing retained-index query supporting `http-client` and the exact H1
   root. The handler-requery wrapper does not accept an arbitrary HTTP surface;
   do not invent `--surface-name http-client` for it. If a supported route exists,
   run once with explicit recorded bounds no greater than the accepted database
   query's corresponding limits, preserving evidence tiers. Otherwise return
   CAPABILITY MISSING for HTTP reporting while retaining the separate fact-chain
   verdict. Do not write a new helper, patch code or improvise a report generator.
9. If the report omits H1 despite retained facts, record the first demonstrated
   projection/join/search boundary only. A missing report path does not erase
   retained semantic evidence. Do not broaden this task to other HTTP groups.

## Return a bounded result

Write one new private local Markdown comparison note beside the new outputs.
Never overwrite the inventory or accepted artifacts. Record exact invocations,
generator and input identities, timings, evidence references and any deviations
locally. Native machine-readable artifacts must retain their generator SHA-256
and bounded-input SHA-256; do not create ad hoc derived JSON without provenance.

Return only this sanitized summary:

- Public tool branch/SHA, build exit and elapsed time.
- Baseline hashes: verified / mismatch / incomplete.
- Database: preserved / changed / inconclusive, with aggregate before/after
  chain and variant counts, truncation, and a generic description of any loss.
- Representative changes: evidence-backed yes/no; tiers and locations preserved
  or explained by the selected retained witness yes/no.
- H1 retained fact chain: verified / changed / inconclusive.
- H1 report: first baseline created / capability missing / query failed;
  path present yes/no, evidence tiers, limits and truncation.
- Overall: PASS WITH STATED LIMITS, REGRESSION CANDIDATE, CAPABILITY MISSING,
  CONTEXT REJECTED, INPUT MISMATCH, or TIME LIMIT REACHED.
- Smallest next action, if needed; no automatic retry.

Keep private paths, identities, fact IDs, hashes, source, URLs, SQL, payloads and
logs on the work machine. Do not commit or upload results, execute the application,
contact HTTP endpoints or databases, or claim runtime behavior. Database schema
validation and result-shape assumptions remain outside this reporting exercise.
