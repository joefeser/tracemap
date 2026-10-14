# Inventory existing gaps — no build, rescan or full test suite

Fetch `origin/codex/evidence-admission-hardening` and select that branch without
overwriting local changes. Use the committed `scripts/summarize-retained-gaps.py`.
Do not create ad-hoc parsers or run application code. This is one read-only pass
over an existing scan, not another acceptance or test run.

1. Ask the operator to select the existing scan directory containing
   `facts.ndjson` and `report.md`. Reuse the already known saved run if unambiguous;
   do not search the entire disk or guess a new source root. Keep paths private.
2. Confirm Python 3.9 or newer is already available. If missing, stop with
   `RETAINED_GAP_INVENTORY_TOOL_UNAVAILABLE`; do not install software.
3. Choose a new output filename outside the repository and the saved scan.
   Run once, replacing the placeholders with local paths:

   ```text
   python scripts/summarize-retained-gaps.py --scan "<EXISTING_SCAN_DIRECTORY>" --out "<NEW_LOCAL_SUMMARY_JSON>"
   ```

   The script has a 30-second processing budget, a 512-MiB facts limit,
   one-MiB line limit and one-million-fact limit. Stop on any limit or input error;
   do not raise limits, retry repeatedly, rebuild, rerun the scanner or run tests.
4. Return only the generated summary JSON after operator approval. It contains
   closed-label aggregate counts, the exact public generator/catalog hashes and a
   hash of the privacy-projected aggregate input. It contains no private input
   hashes, paths, symbols, source, SQL, scan identifiers or private commit IDs.
   Do not attach raw facts, reports, logs or terminal transcripts.
5. Explain the two separate denominators: `analysisGapFacts` and the retained
   report's unresolved-call count. Unknown reason labels are grouped under
   `other-or-missing-reason`; do not recover their raw values into the handoff.
   Missing report totals are unavailable, not zero. Do not add these denominators
   together or interpret occurrences as unique defects.

This first inventory does NOT reconstruct unresolved-call reasons or rank gaps by
SQL/HTTP reachability: those require retained resolver diagnostics or evidence
joins that this tool does not have. Report those fields as unavailable, not guessed.
Do not claim the historic 266 gaps changed without a comparable earlier summary.
The maintainer will use this inventory to scope whole-family synthetic coverage.
