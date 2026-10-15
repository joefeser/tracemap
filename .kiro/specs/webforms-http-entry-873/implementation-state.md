# Implementation state

Branch: codex/webforms-entry-873.
Fresh origin/dev baseline: 25c4f6475db9b92b3f08dafafd5117a554e3020a.
Issue: https://github.com/joefeser/tracemap/issues/873.

Implemented the three separately typed entry families and a retained-input report
inventory. No call traversal/root admission. Existing composition reused; missing
VB project/qualified-name registration and scan-root relative Src resolution fixed.
Ordinary VB property declarations are the positive fixture; missing C# declarations
stay unavailable. Framework witnesses cover direct signed metadata members only.

Files: docs/validation/WEBFORMS_HTTP_ENTRY_RELATIONSHIPS.md (matrix),
WEBFORMS_HTTP_ENTRY_PUBLIC_CORPUS.md (two pinned MIT static checks),
samples/http-entry-boundaries (independent fixtures), and extended
HttpEntryBoundaryRegressionTests (SQLite/combine/both readers, deterministic
reports, graph exclusion, ambiguous/scope/unsupported negatives).

Development findings retained: initial missing VB registration and root Src bug;
competing unresolved registrations; target directive support; exact graph exclusion;
framework OnInit/OnLoad may be declared on TemplateControl/UserControl/Page,
not only Control. No fake signed framework types added. Exact supported signatures
are documented. Cache evidence for the new joins without weakening the existing
external-enumeration regression. Historical version literals updated only where
asserting the current extractor.

Validation before final freeze: original HTTP 17/17; intermediate adjacent suite
163/163; relevant Python diagnostics 60/60. Broad Python discovery was an invalid
invocation of SQL command-text tests (32 setup errors, CONFIG unset); it is not a
pass. Preliminary broad .NET filter was cancelled after final review edits and is
not an acceptance receipt. Final focused/full/CLI results are recorded in validation-receipt.md.

No third-party app build/restore/hosting or endpoint/database execution. Corpora
remain outside the checkout. No push, PR, external reviewer or workflow mutation
authorized. Windows/private and other pinned adapter checks remain separate gates.

## Historical paused checkpoint — 2026-10-15

Paused at the user's request to change locations. Implementation commit:
38b6e33db2682b1fb24cc640d0b83ca645bb03ae. The following checkpoint adds
page/base-scope receiver identity checks: conflicting inherited fields refuse a
property candidate; an explicitly matching field remains supported. Candidate
boundaries still leave runtime receiver identity and getter invocation unproven.
The shadow fixture first reproduced the false positive, then the focused real
framework regression passed (1/1) with -warnaserror after the fix.
Log: /tmp/tracemap-873-shadow-checkpoint.log.

Validation status is PARTIAL, not completion of issue #873:
- Earlier corrected HTTP suite: 18/18; relevant Python diagnostics: 60/60.
- Earlier broad focused run: 596/597; the remaining test expectation was corrected
  and covered by the subsequent 18/18 HTTP run. Rerun the full focused selection.
- At 38b6e33d the solution and trusted corpus diagnostic built with zero warnings.
  Full frozen suite was deliberately cancelled after 3,184 observed passes and no
  observed failures when receiver shadowing was found. This is NOT a full pass.
- Final static corpus counts at that earlier head: Kaliko 7 containment entries,
  CsrFence 0 new entries. Update the corpus readback's preliminary 13 count.
- Runtime hashes from the earlier freeze are obsolete after this follow-up build.

Resume here: review the receiver-scope change; update coverage/rule documentation
and fixture README; run focused regressions. Then freeze the new local HEAD and
runtime files, run the complete .NET suite without concurrent builds, relevant
Python diagnostics, public CLI sample smoke, and static-only pinned corpus reads.
Record exact commands, results, hashes and limitations in validation-receipt.md;
update tasks.md only after those acceptance steps finish. Create the final local
handoff. Do not push or open/merge a PR without approval.

Supporting logs/scripts are under /tmp/tracemap-873-* and may be ephemeral. The
worktree and committed fixtures are the durable resume source. No work is running
on this task after this checkpoint.

## Completed local handoff — 2026-10-15

Resumed and reviewed the receiver-scope fix; updated matrix/rule/fixture docs.
Frozen tested HEAD: 1266f4a3d5bcb0d29b286b975ac4077f46fda02e. Full .NET:
4,367 passed, one authentic-Windows skip, zero failures. Focused 597/597;
Python diagnostics 60/60 plus artifact-validator 7/7; zero-warning solution/tool
builds; public CLI scan/combine/repeated-report/positive-negative selectors passed.
All 593 runtime hashes matched before/after. Both pinned static corpora were clean
and produced the documented bounded results. See validation-receipt.md for exact
commands, hashes, retries, skipped/deferred checks and limitations.

Only this receipt, state note and task checklist changed after the frozen run;
no implementation or fixture changes. All issue #873 local implementation tasks
are complete. Owner review/publication approval is the next step; no push, PR,
reviewer request, merge or workflow mutation is authorized. No process is left
running. The original checkout and unrelated work remain untouched.

## PR #876 owner-authorized review repair

Owner authorized fixing current-head issues after ACK held the batch due to its
Baz exact-head evidence mismatch. Ten verified findings are repaired together;
the projectless array-index finding is non-actionable because the predicate is
not evaluated on empty input, now pinned by a regression. Lane policy unchanged.
HTTP-entry tests: 19/19 with warnings as errors after the final getter tightening.
Broader focused regression suite: 598/598 before that final narrow tightening.
Full-suite evidence above belongs to the prior implementation head; new-head
hosted validation and ACK readback will be recorded on PR #876.
All 11 original findings have code/test evidence prepared for exact-thread
disposition after push. No lane policy, ACK source or call traversal was changed.
