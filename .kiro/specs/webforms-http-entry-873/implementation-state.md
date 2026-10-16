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

Round 2 follow-up: the owner-authorized repair also covers the two current-head
Baz findings: C# field declarations now emit the receiver assembly token (signed
and unsigned compiler-backed regression coverage), and refused known getters keep
the exact member target and ID through reports. Missing ordinary C# property
witnesses remain unavailable; this does not broaden C# property extraction.

Follow-up validation: HTTP-entry, C# semantic, legacy WebForms and current-version
regressions passed 87/87 with warnings as errors. Prior repair head c2dc16d7
passed the frozen 598-test suite and its hosted .NET adapter check.

Round 3 follow-up: three executable validation prompts derive the WebForms
extractor expectation from ScannerVersions in the selected tool revision rather
than pinning 0.13.12. Verified all three references resolve to the current 0.14.2
constant; historical receipts and the prompts' bounded private-validation scope
are unchanged. This is a documentation-only correction; no private scan was run.

## Main promotion PR #877 — 2026-10-16

Integration branch `codex/integration-dev-main-20261016` starts at merged dev
`ef5c2b1c524d9a361c1b31f8049710fe5e128886`; target main baseline
`e189513360a2daf1aee572949b2f12a13c76db69`. ACK 0.5.6 release installed and
verified; round 1 authorized the settled 12-thread repair batch.

WebForms fixes retain all competing registrations in ambiguous-gap provenance
and refuse Shared/static getters (new semantic staticness evidence). The missing
source-field suggestion is dispositioned against the documented Tier3 markup
candidate contract: runtime receiver binding remains explicitly unproven.

Base44 companion repairs cover possible nested alias overwrites, module-level
assignments, finally completion paths, null-prototype guarded key domains,
canonical numeric SDK versions, and historical UI producer validation. Query
method spellings remain opaque redacted references; documentation and a custom
method regression prohibit built-in/string-result inference. Prior positive
map-guard fixtures now explicitly use null prototypes; ordinary inherited-key
negative fixtures pin the refusal rather than silently narrowing their domain.

Validation before publication: TypeScript build + 299 tests passed; .NET focused
87 tests passed with warnings as errors. Final registration-digest mutation
assertion passed in the 19-test HTTP regression rerun. Current-head hosted CI and
ACK remain the promotion gates. Full .NET historical counts above are not a new
promotion-head full-suite receipt. Existing npm dependency advisories (vitest,
@vitest/mocker, source-map-js) are unchanged by this promotion.

Any review-fix commits on this branch require a separate explicit reconciliation
PR back to dev; do not merge unrelated main-only work into dev. No main merge
authorized.

Round 2: Baz found a destructuring false negative in the alias-write guard.
The target walker now ignores property keys and default-value reads, follows
actual object/array/rest/default assignment targets, and checks lexical identity.
The sibling for-in/of destructuring surface uses the same target walker.
TypeScript build and all 310 tests passed, including 11 target/read/shadow cases.

Round 3: hosted CI was clean, but Baz requested distinct producer identity for
the destructuring operation-set change. The owner authorized up to six rounds;
this bounded follow-up uses that authorization after the lane's default two
hosted fix cycles. Producer 0.18.2 preserves prior 0.18.1 packet acceptance.
No lane policy changed and main merge remains unapproved.
TypeScript build and all 311 tests pass after the version bump, including exact
0.18.2 operation identities and historical 0.18.1 UI packet acceptance.

Round 5 fresh Codex found enclosing-header writes and awaited generator return
shape confusion. Both repaired with producer 0.18.3 and retained older allowlists.
The control-flow matrix covers braced/unbraced if, while/do/for headers, iterator
targets, switch discriminants, try/catch/finally, and repeated loop writes; safe
branch/try/loop positives remain. Sync/async generators both refuse awaited and
unawaited result-shape inference; ordinary awaited helpers retain their tests.
TypeScript build and 333 tests passed. The final sixth review round follows
publication and exact-thread disposition; no additional merge authority granted.
