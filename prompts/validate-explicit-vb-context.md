# Validate explicit projectless VB compilation

Use `codex/evidence-admission-hardening` at the pushed implementation HEAD. Record
HEAD and working-tree state; preserve unrelated edits. Read
`docs/validation/EXPLICIT_VB_CONTEXT.md`. Use the installed security-patched .NET 10
SDK; record its version without treating a patch difference as a failure.

First build the test project once (eight-minute ceiling), then run the exact
focused filter in `.github/workflows/vb-compilation-context-matrix.yml` once
(ten-minute ceiling, two-minute test-host hang timeout). Do not run the full suite
or retry failed/hung runs automatically. Report pass/fail/skip and sanitized
failure names. A timeout or crash is not a pass.

Then determine whether an operator-approved **explicit compilation context** for
the private application's current source commit is already available. Do not
guess the reference set, reuse the scanner's own runtime assemblies, infer build
membership from every file in a directory, create a fake project, install reference
packs, or claim compiled DLL provenance establishes original source compilation.
If the context is absent, stop with the precise missing inputs: source membership,
root namespace/options/imports, exact approved framework/application reference
files. This is an input requirement, not a request for another diagnostic rescan.

If the operator supplies that context, verify it conforms to the documented
schema and limits. Run one direct `tracemap scan` into a fresh local directory
with `--vb-compilation-context <local-json>` and the same authorized source scope.
This new switch is not yet accepted by the separate WebForms governed review
runner; do not mutate or resume its existing run receipts. If that runner is
required, report the integration limitation rather than bypassing it.

Compare with the accepted baseline by family, not summed denominators. Verify
context labeling, sanitized compiler errors, preserved syntax fallback for files
outside the context, registration candidates, five parameter facts and four
candidate operation paths. Record partial/failed status honestly. No promise of
removing all 167 semantic/HTTP notices or the separate compiled gaps. Share only
aggregate counts and generic failure categories; never upload the context,
reference hashes, source snippets, paths, DLLs, or private scan artifacts.
