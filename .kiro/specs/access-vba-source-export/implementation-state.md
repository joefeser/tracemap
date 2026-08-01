# Access VBA Source Export Implementation State

Status: local implementation in progress; no Windows execution has occurred.

Branch: `codex/access-vba-source-export`

Base: `origin/dev` at `094a83a6a6ae4f053293c96c3aea1cb0c359b748`

Decision: `SaveAsText(acModule, ...)` fits this separately reviewed source
export lane because it is the same non-invoking mechanism already used for
form/report metadata. It does not widen the normal product COM reader and does
not use VBE or source-line APIs. Module identity access is a guarded capability
and requires Windows synthetic validation before representative use.

Scope: synthetic fixtures, exporter, protected-bundle contract, and pure
composition only. No Autonomy database, isolated VM, real source bundle,
production COM reader, or public artifact has been touched.

The private source bundle retains complete form/report definitions already
produced by the metadata exporter, with generic artifact names, roles, hashes,
and line counts. It does not parse or project layout today; that retention
avoids reopening the database for a future private layout phase.

Local validation:

- PowerShell syntax parsing passed for the exporter, synthetic fixture, and
  synthetic smoke harness.
- focused reader/composition/source-export tests: 58/58 passed;
- all Access-named tests: 172/172 passed;
- full solution build: passed with 0 warnings and 0 errors;
- private-path guard and `git diff --check`: passed.

Lifecycle follow-up validation:

- focused lifecycle/projector/composition tests passed;
- all Access-named tests: 173/173 passed;
- full solution build: passed with 0 warnings and 0 errors;
- private-path guard and `git diff --check`: passed.
- `dotnet format --verify-no-changes` was attempted but currently reports
  pre-existing formatting drift across unrelated projects on the fresh base;
  this lane does not modify those files.

Windows synthetic smoke follow-up: fixture generation initially stopped before
private data use because `OnAfterUpdate` is a design-text label, not the
ListBox COM property. The fixture now assigns `AfterUpdate` and the repository
test asserts that exact contract while rejecting `OnAfterUpdate`. PowerShell
syntax parsing and the relevant source-export/projector tests pass locally;
the corrected head still requires a fresh Windows synthetic smoke run.

The corrected smoke reached the exporter and reported the old combined
`AccessVbaSourceChanged` classification. Original and disposable-copy hashes
were previously treated as one gate, so that result does not establish an
original mutation. The exporter now gives original and supplied copies distinct
strict classifications and records pre/post inner-working-copy hashes and a
typed outcome. Filesystem read-only mode was not added because it is unvalidated
for the Access open/export mechanism.

The next synthetic smoke reached `OpenCurrentDatabase` and stopped at the
generation sentinel. The fixture has an intentional `StartupForm` whose
`Form_Open` writes that sentinel, while the exporter opened the supplied copy
directly. The exporter now follows the reviewed metadata exporter pattern: it
creates a bounded inner scratch copy, clears only `StartupForm` with DAO,
closes DAO, then opens the inner copy with disabled automation and invisible
UI. Original and supplied-copy hashes remain integrity gates; inner-copy
pre/post hashes and outcome are recorded, and the inner scratch is removed.
Visible UI, generation canary, and extraction canary now have distinct typed
classifications. A fresh synthetic Windows smoke run is required at this head.

That fresh smoke reached metadata-record handling and stopped before source
projection because the isolated VM has Windows PowerShell 5.1, which lacks the
hashtable conversion switch used by the exporter. The exporter now uses ordered
`PSCustomObject` property access and property-based sorting/grouping instead.
This preserves record order and schema while requiring no additional runtime.
A fresh synthetic Windows smoke run is required at the corrected head.

The next run reached the smoke's final canary assertion and exposed an
unparenthesized `Test-Path` command joined with `-or`; Windows PowerShell bound
the second `-LiteralPath` as a duplicate parameter. Each invocation is now
parenthesized. A regression scans every Access validation script for this
unparenthesized `Test-Path` boolean form. Cleanup completed, and a fresh
synthetic Windows smoke run is required at the corrected head.

Deferred: standard/class classification beyond form/report naming, dynamic
expressions, nonzero argument functions, callbacks, macro code, section-level
metadata, runtime behavior/order, and representative validation. Event bindings
resolve to procedure line spans only when the static same-module resolution is
unique; the source line for a design-property declaration remains unavailable.
