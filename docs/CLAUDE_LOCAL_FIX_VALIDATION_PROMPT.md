# Claude prompt: validate the existing fix locally

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
