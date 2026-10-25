# Tasks

- [x] Verify branch/base, instructions, validation, live issues and parser license/target/version.
- [x] Specify separate DDL origin, admission, bounds and fail-closed policies.
- [x] Implement parser/export/admission and native comparison/report route.
- [x] Add independent grammar/refusal/provenance and C#/VB regression matrix.
- [x] Run focused .NET and offline Python harness guards.
- [x] Commit implementation before frozen full Debug -warnaserror gate.
- [x] Run committed-head native CLI smokes; record pinned adapter deferrals/results.
- [x] Run privacy/whitespace guards, consolidate report/follow-up and push.

## Independent review round (after 0a48b40)

Re-verification of the delivered head against the original brief, without
assuming the recorded gates still hold.

- [x] R1. Parser recursion inside the documented token bounds aborted the process
  (uncatchable stack overflow, exit 134). Parse on a fixed-size dedicated stack.
- [x] R2. Enforce the documented six-type spelling vocabulary; reject ANSI
  synonyms (`integer`, `dec`, `character varying`, `national ...`) and quoted
  type names instead of silently normalizing them.
- [x] R3. Strict UTF-8 BOM policy: documented as admitted, but the parser rejected
  BOM-prefixed files as malformed. Make code and documentation agree.
- [ ] R4. Re-run focused tests and Python guards; commit; frozen full Debug
  -warnaserror gate; committed-head JSON and DDL smokes; guards; report; push.
