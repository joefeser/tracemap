# Build the portable, sanitized handler reproduction

Read `AGENTS.md` first. Work in the existing Windows environment, where the owner has already made the private application source and review evidence available. Inspect existing work and preserve it. Do not restart the solved zero-chain investigation or rerun the full suite.

## Outcome

Create and locally validate a coherent sanitized source reproduction of the original dropdown-initialization handler and its dependencies. The owner has spent many hours relaying photographs; use the local source and retained evidence yourself. Do not ask for screenshots, individual alias resolutions, or manual copying of code. Pushing is unavailable in this environment and is not a completion requirement.

The original handler's normal requery now retains 28 chains and 116 variants with the publication-member fix. This is a baseline, not a completeness claim. The remaining depth cutoffs and virtual-dispatch uncertainty must not be erased or falsely declared solved. The sanitized reproduction must preserve the relevant source use case, not merely produce any nonzero path count.

## Collect the relevant source together

Recover the original handler and source paths from the local review configuration, command history, and retained graph. Start with the page, code-behind, and the four supporting files previously inspected. Follow the relevant dependency methods through business logic, data access, property getters, and SQL wrappers. Include additional source/configuration/build inputs when required; four files is not an arbitrary hard limit.

Preserve the relationships and syntax relevant to the case: namespaces and nesting, inheritance, signatures and overloads, optional parameters, getters, receiver creation, field initialization, cache/session access, impersonation branches, early returns, Try/Catch/Finally, Using, lambdas, argument rewrites, string construction, and database command setup. Preserve the source/projectless deployment layout and source-to-published-assembly mapping conditions.

For proprietary dependency methods, use sanitized source when locally available. Where only a DLL is available, first inventory what is missing. Do not silently ship that DLL or substitute a behaviorless stub for an essential link. Record a precise missing dependency boundary if a faithful source equivalent cannot be constructed from authorized available evidence. Do not execute the application's SQL, network, authentication, or business operations.

## One private mapping for the entire bundle

Build one consistent original-to-synthetic mapping across every included source file, configuration reference, markup reference, and expected graph identity. Use neutral synthetic names, not original names with a prefix. Handle VB case-insensitivity, scopes, overloaded members, properties/accessors, markup bindings, and string-based references deliberately. Avoid blind global replacements that alter framework APIs or unrelated identifiers.

Keep standard framework identifiers unchanged. Replace business SQL with fictional SQL preserving the same concatenation/parameter/command-type structure. Replace credentials, connection strings, endpoints, personal information, and other sensitive values with safe placeholders. Omit unnecessary comments, logs, original paths, repository URLs, and source snippets from the shareable package. Do not include a reversible private map, private binary, PDB, original receipt, original graph, or Git history in that package.

Keep originals and the private mapping in a clearly separate local directory outside both the shareable tree and the repository's tracked files. Avoid displaying sensitive mapping entries in summaries. Track unresolved transformation cases locally. Do not describe the result as safe to share until it has passed the checks below and owner review.

## Build and prove the reproduction locally

1. Create a standalone synthetic fixture with its build files, safe configuration, dependencies, and an entry-point description. Reuse existing repository fixture infrastructure when appropriate, without overwriting existing fixtures. Do not copy the entire private application by default.
2. Compile sanitized source and generate fresh publication mappings/receipts for those outputs. Preserve the deploy-only condition: the relevant compiled inputs lack a full compiled build-binding receipt, but the publication receipt binds the actual assembly hash. Never reuse private receipt hashes after modifying their inputs.
3. Provide one build command and one reproduction/verification command. Automate the checks so another machine does not require UI interaction or private files. Follow repository provenance requirements for derived machine-readable artifacts; label synthetic evidence clearly. Keep generated output and its timestamps/paths out of source fixtures where unnecessary.
4. Pin explicit expected relationships from the original local evidence: exact synthetic source-handler selection; source-to-compiled candidate bridge; relevant business/data-access/getter links; database endpoint(s); command-expression shape; and symbolic runtime inputs with their uncertainty. Retained graph visibility alone is insufficient: check normal requery, receipt, handoff, and HTML.
5. Demonstrate before/after behavior. With the old `provenanceState == "bound"` prefilter, the expected source-handler connection must fail. With the fixed implementation, the expected normal database route must be retained. Use isolated builds/worktrees or a narrowly controlled test setup; do not reset the owner's checkout or mutate assemblies while tests are running. If testing an old implementation, establish that the regression fails at the intended connection, not at a build or dependency error.
6. Verify negative controls: missing publication mapping, wrong assembly hash, mismatched receipt identity, and ambiguous members must not manufacture a bridge. Preserve relevant dispatch and runtime-value uncertainty. Do not force coverage or path counts by weakening admission.
7. Compare the relevant route and evidence, not exact 28/116 counts after reducing the application. If the sanitized case does not reproduce the same defect, refine it locally before asking the owner to transfer anything. Report any material simplifications and their effect on coverage.

## Privacy and portability checks

Audit the complete proposed shareable tree, including filenames, source, configs, manifests, archives, and build outputs. Check for original identifiers/literals using the private mapping without printing their values, and also inspect for secrets, private paths, domains, comments, and resources outside that mapping. Mapping-based checks alone do not prove sanitization. Review retained binaries/PDBs and generated receipts for embedded private paths; prefer rebuilding from the sanitized source on the receiving machine.

Run the documented commands from a clean copy of the shareable source tree with no access to original paths/configuration. Verify there are no private external project references, machine-specific dependencies, or implicit references to the original application. Record prerequisites and unavailable tool limitations explicitly. Do not install unapproved software or upload anything automatically.

## Deliverables and stopping condition

- Sanitized buildable source tree, safe build/configuration files, and required dependency source.
- A short README with the two commands, prerequisites, expected route, before/after assertions, and limitations.
- Automated regression and negative-control checks.
- Public-safe validation summary with actual commands, outcomes, and provenance for generated artifacts.
- A reviewed-candidate archive containing only an explicit allowlist of shareable files. Private mapping/originals must remain outside it. Owner approval is required before transfer; do not upload or commit private inputs.

Keep a concise local implementation-state note with completed work, validation, outstanding dependencies, and the next step so compaction does not restart the task. Do not stop merely to ask whether to continue. Finish by reporting the sanitized package's exact local location, what the before/after checks proved, privacy-review status, and any real blocker. Do not substitute another diagnostic-only JSON slice for this source reproduction.
