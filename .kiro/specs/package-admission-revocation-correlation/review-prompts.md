# Package Admission and Revocation Correlation Review Prompts

Use these prompts after an implementation exists. This PR is specification only; the prompts below review the spec itself now and the implementation slices later. They are review-focused and must not ask the model to invent facts not present in the spec, code, or artifacts.

## Spec Review Prompt (use now, against this PR)

You are reviewing the TraceMap specification for GitHub issue #690, package admission and revocation correlation. The PR must contain only `.kiro/specs/package-admission-revocation-correlation/` files; no product code.

Review stance:

- The spec is a contract, not an implementation. Judge identity precision, evidence honesty, determinism, privacy, and boundary discipline.
- Issue #690 does not authorize implementation. Flag any task or design text that invites implementing product code in this PR.
- TraceMap must not become a package authority, malware scanner, or deployment control, and must not depend on 88mph internals or issue #689 landing first.

Context to read:

- `AGENTS.md`
- GitHub issue #690 (and #689 for the boundary only)
- `.kiro/specs/package-dependency-surfaces/requirements.md`
- `.kiro/specs/package-upgrade-impact/requirements.md`
- `.kiro/specs/multi-index-portfolio-report/requirements.md`
- `.kiro/specs/combined-dependency-paths/requirements.md`
- `.kiro/specs/combined-dependency-diff/requirements.md`
- `.kiro/specs/reverse-impact-query/requirements.md`
- `.kiro/specs/sql-validation-summary-ingestion/design.md`
- `.kiro/specs/access-source-neutral-design-evidence-ingestion/design.md`
- `.kiro/specs/package-admission-revocation-correlation/` (all five files)
- `rules/rule-catalog.yml` (package-related entries)

Questions to answer:

1. Does the artifact identity contract bind everything issue #690 requires (ecosystem, normalized name, exact resolved version, registry origin, digest, lockfile identity, direct/transitive, snapshot and commit, record identity and digest, producer/policy version and decision time, provenance relationship)?
2. Is the evidence ladder closed, ordered, and honest? Can exact and possible matches ever collapse in a summary, rollup, or exit code?
3. Does the external decision contract keep the authenticity boundary intact: digests prove integrity, not authority; verification belongs upstream; malformed/unsigned/unsupported/unverifiable records fail closed with closed-set classifications?
4. Does the rule ownership decision (three new rules plus the neutral `package-decision.v1` envelope) avoid overloading `package.upgrade.impact.v1`, portfolio, paths, diff, and reverse rules whose limitations and identity contracts differ?
5. Is the adapter capability matrix truthful against the current `dev` implementation, and does the spec turn every missing capability into an explicit gap rather than silence or a guessed match?
6. Does the correlation behavior cover portfolio snapshots, direct/transitive paths, before/after artifact changes, imports/surfaces only where adapters prove them, optional build/deployment references, bounded framework-implied exposure profiles, and deterministic gaps?
7. Is revocation output bounded to exact matches, possible matches, excluded/unknown sources, stale/runtime-unproven references, and focused-review inputs — with no remediation commands or operational authority?
8. Are storage decisions (read-only, single versus combined versus portfolio, no v1 persistence, package-impact and release-review reuse) consistent with the existing composition commands?
9. Does the synthetic fixture matrix include every case issue #690's smallest proof requires, plus adversarial privacy values and determinism?
10. Are the boundaries complete: no download/execution, no malware/vulnerability/SAST claims, no admission/revocation decisions, no patching/blocking/rollback/deployment/approval, no runtime-load claims, no credentials/raw malicious content/private paths/customer identity, no LLM/embeddings/vectors, no 88mph internals?
11. Are tasks ordered, independently testable, spread across PRs, with explicit validation commands and stop conditions?
12. Do the proposed `package.decision.*` rules follow the catalog conventions (id shape, tiers, emits, non-empty limitations, deferred-until-implemented status)?

Output format:

- Findings ordered by severity with file and section references.
- For each finding: why it matters and the smallest credible spec fix.
- Then open questions and unresolved owner decisions worth surfacing.
- End with a validation summary (diff scope, checks run) and residual risk.

## Implementation Review Prompt (use per slice, after implementation exists)

You are reviewing a TraceMap implementation slice for GitHub issue #690, package admission and revocation correlation, against `.kiro/specs/package-admission-revocation-correlation/`.

Check only these things:

- Every emitted row carries a rule ID, evidence tier or explicit external-claim label, and full provenance (source label, scan ID, repo identity or hash, full commit SHA, fact chain, extractor identity). No row is emitted before its rule-catalog entry with limitations exists.
- Exact and possible matches are never merged, summed together, or labeled equivalently in JSON, Markdown, summaries, or exit codes; digest-absent evidence can never produce `ExactArtifactMatch`.
- Record admission fails closed: closed-set input classifications, no silent drops, duplicate/conflict policy per design §2.3, constant-time digest compares, whole-input limits.
- No wall clock, registry fetch, package execution, signature verification, or trust inference from provenance appears anywhere.
- Privacy: adversarial fixture values absent from every artifact; repo identities hashed or host-only; no free-text fields ingested; `./scripts/check-private-paths.sh` passes.
- Adapter changes are additive (`PackageReferenced` optional properties), update their rule's catalog limitations and `docs/VALIDATION.md` entries, and emit capability gaps instead of guesses.
- Staleness and runtime-unproven references render as bounded context and never upgrade or downgrade a rung.
- Outputs are byte-stable across repeated runs; sort orders match design §6.3; truncation emits `TruncatedByLimit` without suppressing coverage gaps.
- Tests cover the slice's fixtures from design §12 and the slice's validation commands were run or explicitly deferred with reasons.

Files to read: the spec directory, the implementation diff, relevant tests, `rules/rule-catalog.yml`, `docs/VALIDATION.md` sections touched.

Output format: findings first (highest severity first) with exact file/line references; then missing validation commands; then a short residual-risk note.

## Artifact Inspection Prompt (use when generated reports exist)

Inspect the generated artifacts for a `tracemap package-decision` run:

- `package-decision-report.json`
- `package-decision-report.md`
- the input `package-decision.json` (and advisory/deployment inputs when present)

Verify:

1. Every correlation row has a classification from the closed ladder, rule ID, evidence tier or external-claim label, and provenance back to a fact with file path, line span, and commit SHA.
2. Exact rows always show digest algorithm and digest on both sides; possible rows never claim exactness; digest mismatches are reported as their own classification; summary counts keep all rungs separate.
3. Decision Records section shows admitted and rejected records with classifications; no record silently disappeared.
4. Stale and runtime-unproven references carry the required limitations wording; advisory claims show producer/version/digest provenance and stay outside correlation rungs.
5. No artifact contains credentials, raw URLs with credentials, local absolute paths, private paths, free prose from inputs, or remediation commands.
6. Re-running the command with identical inputs produces byte-identical JSON and Markdown.
7. Reduced coverage, unknown commit SHAs, and unsupported capabilities appear as explicit gaps; exclusion is claimed only under full credible coverage.

Report findings with artifact path and JSON path or line reference where possible.
