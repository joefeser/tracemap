# Claude: check current work-machine HTTP readiness

The operator requests a fresh current-tool readiness check, not another extraction
campaign. Determine whether the retained inputs and current TraceMap tooling are
sufficient for the accepted static HTTP review scope before proposing build work.

Read `prompts/verify-work-machine-http-readiness.md` completely before acting.
This prompt explicitly selects **fresh current-tool readiness mode**, sections
1–3, including the committed reporting-component verifier. Do not select the
verification-only follow-up for old binaries.

## Tool and safety gates

Use the operator-prepared checkout containing this prompt. Record live full HEAD
and dirty state. Require merged PR #904 commit
`be7247be5d4ed6df187c1921e44ba795996a46fe` and merged PR #905 commit
`a9c13c46d` to be ancestors of HEAD. If either is absent, report TOOL UPDATE
REQUIRED; do not pull, switch branches, or change the checkout yourself.
The referenced prompt's build-affecting dirty-state and binary identity gates apply.

Locate the accepted inventory and acceptance/correction notes using the referenced
prompt. Never choose an input because its folder looks newest. Retain H1 and H2/H3
accepted indexes and use the accepted fresh H4 index with its lifecycle candidate.

Within one total 20-minute budget:

1. Build TraceMap once with warnings treated as errors. A failed build stops work;
   do not fall back to an existing DLL.
2. Run at most three bounded HTTP requeries: H1, the shared H2/H3 event entry, and
   H4. Use unchanged recorded traversal budgets and new private output directories.
3. Independently verify the expected terminals and evidence for all four groups.
4. Run `scripts/verify-http-report-provenance.py` for the new native receipts using
   an installed Python 3.11+ executable and the actual invoked CLI DLL plus its
   adjacent Reporting DLL. Do not compare new reports with old producer binaries.
5. Verify the accepted baseline artifacts remain unchanged.

Do not scan, combine, preflight, build or execute the application, access databases,
execute HTTP calls, install dependencies, modify source, invent diagnostic scripts,
or overwrite accepted artifacts. Do not increase traversal limits or retry to hide
truncation. Missing inputs mean a blocked check, not permission to recreate them.
Keep all private data, selectors, paths, symbols, hashes, and outputs local outside
Git. All additional safety restrictions in the referenced prompt remain in force.

## Return one bounded answer

Write a new private local note and return the manually sanitized acceptance matrix
specified in the referenced prompt. Include tool revision, build result, binary
verification, baseline preservation, and elapsed time. Preserve UNKNOWN HTTP verbs,
truncation reasons, and H4's Tier2Structural lifecycle candidate separately from
its Tier1Semantic method-to-HTTP path. Candidate visibility does not prove runtime
dispatch, construction, or lifecycle execution.

Then answer:

1. Are the retained inputs and tooling sufficient for accepted static HTTP review?
2. What exact capability or input is missing, if any? Distinguish missing local
   inputs, root selection, extraction absence, bounded traversal, projection,
   rendering, and provenance problems using actual evidence.
3. What evidence-backed work can proceed, and what remains unproven?
4. If broader application implementation is requested next, what additional
   requirements or evidence would it need? Do not infer application-build,
   migration, SQL, or runtime readiness from this HTTP check.

Use the referenced prompt's verdict vocabulary. If blocked, name the smallest
operator action without executing it. **Do not start implementation.**
