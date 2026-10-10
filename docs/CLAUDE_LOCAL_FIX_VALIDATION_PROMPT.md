# Claude prompt: validate the existing fix locally

## Latest continuation: close the original case

The owner reports that the new build passed 85 focused tests and three script checks, and a private normal requery for a different handler retained 101 chains and 211 variants. This is useful evidence, but does not establish acceptance for the original failing handler. Do not repeat those completed checks without a reason.

Use the already-built fix and the original handler/review-root selection retained in this local session or its existing configuration. The owner cannot copy prompts or diagnostic material between machines. Perform the local work yourself; do not ask for more screenshots or individual alias lookups.

1. Run **normal requery for the original failing dropdown-initialization handler**, not the different history-loading handler used in the successful validation, and not graph-only mode. Recover the exact original handler and review-root identities from local command history/configuration/evidence; do not guess them. Verify supported script options before executing.
2. Confirm the receipt binds the new generator, the intended root/index, and `pathEnumerationPerformed=true`. Inspect the actual handler-to-database chains, variant counts, and output path. Do not run a full-site scan or full test suite before this focused acceptance check.
3. If the original case succeeds, report the concrete retained route and counts, with remaining evidence limitations. Preserve the committed fix. Since pushing is unavailable here, export a Git patch of the fix and tests locally, excluding private artifacts, and report its location for later integration. Do not claim the remote contains this fix.
4. If the original case still fails, identify the first missing/rejected connection using the local source and retained evidence. Then create a portable sanitized reproduction of the relevant page and dependent files as one coherent bundle, rather than another sliced graph export. Use one consistent private mapping across identifiers and references. Preserve signatures, overloads, inheritance, getters, control flow, string-expression shape, and the publication relationships involved in the failure. Remove secrets; do not merely rename them. Keep the mapping and originals private, outside the shareable bundle.
5. For the sanitized build, generate fresh assemblies, hashes, and receipts. Do not reuse original receipts after transforming their inputs. Include one build command, one reproduction command, and an assertion identifying the same missing connection. Validate locally that the package reproduces the original failure, rather than an unrelated missing-dependency or zero-path result. If it does not reproduce, adjust it locally before asking the owner to transfer anything. Mark the bundle as synthetic and satisfy repository provenance requirements for derived machine-readable artifacts.

The immediate completion target is the **original handler working on the new build**. The sanitized reproduction remains the requested portable regression work and becomes the immediate diagnostic fallback if that check fails; it must not delay the focused acceptance check. Record completed work and exact remaining steps so compaction does not restart the investigation.

The instructions below retain the safety and validation requirements. Where they refer to tests already completed against this exact build, use that evidence rather than rerunning them automatically.

Continue in the existing Windows checkout. Read `AGENTS.md` and inspect current HEAD and working-tree changes. Preserve the committed publication-member bridge fix and its tests. Do not restart diagnosis or discard existing work.

You do not need to push. The owner can test this checkout directly. This prompt supersedes the push requirement in the earlier continuation prompt.

Your previous summary reported removal of the `provenanceState == "bound"` prefilter from the `ManagedMethodDeclared` input to `PublishMemberCandidateIndex`, while retaining the downstream assembly-hash check. It reported 48 focused tests passing, but review-execution tests ran against the old binary. Those old-binary results do not validate the new implementation. Private normal-report acceptance remains unverified.

Do the following now:

1. Build the committed fix locally. Verify subsequent tests and commands actually load the newly built assemblies. Do not rebuild concurrently with provenance-sensitive tests or requery operations.
2. Run the relevant focused normal-requery regression against that new build. Confirm valid publication receipts without full build provenance can retain the supported source-handler-to-database route, while missing/mismatched receipts or hashes and ambiguous members still fail closed. Do not repeatedly run the full suite during this step.
3. Using the owner's existing private review root and exact handler from the prior local run/configuration, rerun the **normal handler-to-database report**, not method-graph mode. Inspect the script's actual supported options first. Do not invent paths or handler identities. Do not rescan, refresh, or overwrite original evidence unless genuinely required; explain any such requirement before expanding the operation.
4. Verify the new receipt confirms path enumeration was performed, identifies the newly generated normal report, and binds the relevant input and generator. Inspect that report and its handoff for actual chain and variant counts. Do not select an old report merely because its filename matches.
5. Report the observed counts, exact local commit/build used, completed tests, and exact output location. Nonzero graph nodes or SQL endpoint nodes alone do not establish normal-path success. If the normal result is still zero, trace the next proven divergence using this local evidence rather than declaring success.

Keep private data local. Do not upload or commit source, SQL, screenshots, graph exports, private paths, or identities. Preserve evidence tiers and provenance limitations. Unknown runtime values may remain symbolic; do not fabricate values or relax admission to produce paths.

Do not stop because pushing is unavailable. Do not ask the owner to repeat operations you can perform in this checkout. If local access to the required review root is genuinely unavailable, state that precise limitation and provide one verified normal-report command using the known configuration, rather than another broad diagnostic sequence.
