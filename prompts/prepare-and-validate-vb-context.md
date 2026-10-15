# Prepare, approve, and validate a projectless VB context on Windows

Work on the current pushed `codex/evidence-admission-hardening` HEAD. Preserve
unrelated files. Use the installed patched .NET 10 SDK and Python 3.9+; do not
install tools/reference packs or run the full solution suite.

Read `docs/validation/EXPLICIT_VB_CONTEXT.md`. Build once (eight-minute ceiling),
run `python scripts/validation/test_vb_context_preparation.py`, then run the exact
focused filter in `.github/workflows/vb-compilation-context-matrix.yml` once
(ten-minute ceiling). Report failure rather than automatically rerunning.

Do not stop simply because a context JSON does not exist. Use the new preparer.
Inspect the already-authorized private source root, published application folder,
existing build/configuration evidence and known framework reference locations,
read-only. Choose narrowly scoped candidate VB directories and reference
directories. Never substitute the scanner's own assemblies. Existing reference
packs may be proposed, but their presence is not proof they match the application.
Do not execute a build to discover inputs without separate authorization.

Run the documented `draft` command into a fresh local location outside the source
checkout. Supply root namespace, strict/infer/explicit and imports explicitly.
Use available configuration/build evidence for those proposals; where unavailable,
label them unknown/proposed and explain the proposed choice rather than asserting
an observed value. Preserve all proposal and context files locally only.

Present one compact approval request: source directories/count, reference
directories/count and intended framework, proposed compiler options/imports, and
the assumption that this source roster compiles as one assembly. Distinguish
current observed file bytes from proposed membership/settings. Ask only about
missing choices and approval of this concrete proposal, not for hand-authored
hashes or a preexisting context file. If necessary inputs cannot be located,
identify those exact missing inputs; do not fabricate them.

After explicit human approval, run the documented `approve --operator-approved`
command. It will recheck all hashes and HEAD. Place the resulting context under
the existing receipt root (without overwriting files), and prepare a new
fresh/projectless review configuration with `vbCompilationContextRelativePath`.
Keep the previously authorized scan scope, report settings and budgets. If the
new explicit inputs exceed existing budgets, report the required delta for
approval. Never edit/resume old run receipts with new inputs.

Run one new governed preflight and scan using that configuration. The runner now
forwards the context and hashes every referenced source/DLL for run/resume checks.
Compare aggregate family counts against the last accepted baseline: registration
candidates, four operation paths, five parameters, semantic/HTTP notices,
unresolved calls and compiled gaps separately. Preserve explicit context labels
and partial/compiler gaps. Refusal or partial compilation is not success merely
because the command completed. Do not promise all 167 notices will disappear.

Return HEAD, test counts, approved proposal decisions, scan status and aggregate
deltas. Do not publish private source, paths, DLLs, contexts, source/reference
hashes or raw logs. Full solution testing and broader compatibility remain
separate from this bounded acceptance.
