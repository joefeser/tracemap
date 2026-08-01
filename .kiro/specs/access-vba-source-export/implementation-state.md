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
original mutation. The exporter now fails closed only for
`AccessVbaOriginalSourceChanged`; it records pre/post disposable-copy hashes
and either `AccessVbaWorkingCopyUnchanged` or `AccessVbaWorkingCopyChanged` in
the private and normalized manifests. Filesystem read-only mode was not added:
the observed Access bookkeeping occurs on the disposable copy, and read-only
opening has not been validated for this export mechanism. A new isolated
Windows synthetic smoke run is required to verify the explicit outcome.

Deferred: standard/class classification beyond form/report naming, dynamic
expressions, nonzero argument functions, callbacks, macro code, section-level
metadata, runtime behavior/order, and representative validation. Event bindings
resolve to procedure line spans only when the static same-module resolution is
unique; the source line for a design-property declaration remains unavailable.
