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

Continue with `prompts/prepare-and-validate-vb-context.md`, skipping its build/test
steps if already completed at this exact HEAD. Do not stop because a context JSON
is absent: the new preparer creates a local candidate proposal, and emits the
consumable context after explicit human approval. Do not infer original build
membership from directory contents or confuse proposed settings with observed
compiler inputs. The governed runner now supports `vbCompilationContextRelativePath`
on a new fresh/projectless configuration and rechecks context/source/reference
hashes. Never mutate or resume old receipts with newly added inputs.

Compare with the accepted baseline by family, not summed denominators. Verify
context labeling, sanitized compiler errors, preserved syntax fallback for files
outside the context, registration candidates, five parameter facts and four
candidate operation paths. Record partial/failed status honestly. No promise of
removing all 167 semantic/HTTP notices or the separate compiled gaps. Share only
aggregate counts and generic failure categories; never upload the context,
reference hashes, source snippets, paths, DLLs, or private scan artifacts.
