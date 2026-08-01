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

Local validation:

- PowerShell syntax parsing passed for the exporter, synthetic fixture, and
  synthetic smoke harness.
- focused reader/composition/source-export tests: 58/58 passed;
- all Access-named tests: 172/172 passed;
- full solution build: passed with 0 warnings and 0 errors;
- private-path guard and `git diff --check`: passed.

Deferred: standard/class classification beyond form/report naming, dynamic
expressions, nonzero argument functions, callbacks, macro code, runtime
behavior, and representative validation.
