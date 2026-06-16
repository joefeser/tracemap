# Legacy Sample Evidence Pack Tasks

## Implementation Tasks

- [x] 1. Define evidence-pack schemas and rule catalog entries. Requirements: 2, 3, 4.
  - [x] Add `legacy-evidence-pack.v1` JSON model with pack identity, claim level, source labels, source classifications, coverage, extractor versions, command provenance, summary counts, evidence sections, gaps, limitations, and safety fields.
  - [x] Add generated Markdown shape derived from the JSON model.
  - [x] Add closed vocabularies for claim level, section status, identity kind, redaction profile, and claim boundary.
  - [x] Add rule catalog entries such as `legacy.evidence-pack.summary.v1`, `legacy.evidence-pack.section.v1`, `legacy.evidence-pack.claim-boundary.v1`, `legacy.evidence-pack.safety-validation.v1`, `legacy.evidence-pack.command-provenance.v1`, and `legacy.evidence-pack.input-availability.v1` with documented limitations before emitting rows that cite them.
  - [x] Define deterministic `packId` derivation from neutral label, purpose, claim level, schema version, explicit injected date or fixture-pinned date, and a deterministic content-derived safe suffix for create collisions.
  - [x] Add injected date handling and fail public-safe or demo-safe pack creation when no explicit or fixture-pinned date is available.
  - [x] Ensure JSON serialization sorts keys, arrays, maps, sections, gaps, limitations, and count rows deterministically.

- [x] 2. Implement input readers and source classification. Requirements: 1, 3, 6.
  - [x] Read raw scan outputs only from ignored local roots and reject unignored in-repo raw scan inputs.
  - [x] Add `.tmp/legacy-evidence-packs/` to `.gitignore` if not already present and add a guard proving the local-only output root is ignored.
  - [x] Read and validate existing legacy validation summaries where available.
  - [x] Read and validate legacy baseline manifests where available.
  - [x] Read and validate public demo summaries where available.
  - [x] Classify each input as `local-only`, `demo-safe`, `public-safe`, or `rejected` before pack generation.
  - [x] Preserve reduced coverage, failed build, timeout, truncation, missing schema, and extractor-unavailable states as gaps or limitations.
  - [x] Avoid copying raw facts, SQLite rows, scan manifests, analyzer logs, raw report prose, SQL, config values, snippets, paths, remotes, or private sample names into the pack model.

- [x] 3. Implement evidence-section aggregation. Requirements: 2, 4.
  - [x] Aggregate total fact counts, gap counts, rule ID counts, evidence tier counts, fact type counts, extractor versions, build/project-load status, scan status, and output artifact availability.
  - [x] Add section builders for build diagnostics, WebForms event flow, WCF/service-reference metadata, static flow composition, SQL/query surfaces, legacy data metadata, package/config metadata, and baseline regression movement when input summaries support them.
  - [x] Represent unsupported, not requested, unavailable, deferred, reduced, truncated, and rejected sections with the closed status vocabulary.
  - [x] Include rule IDs, evidence tiers, source labels, coverage labels, safe provenance references, and limitations for every evidence row.
  - [x] Keep Tier3 syntax/text fallback evidence visibly Tier3 and cap wording at review-needed static evidence where appropriate.
  - [x] Add high fan-out, ambiguous name, reduced coverage, and missing extractor caveats where source summaries expose those conditions.

- [x] 4. Implement command provenance and identity redaction. Requirements: 1, 2, 5.
  - [x] Record TraceMap version, pack generator version, source schema versions, extractor versions, normalized command name, sanitized option names, input classifications, generation mode, and validation command names.
  - [x] Omit raw input paths, raw arguments, remotes, environment values, usernames, machine names, secrets, and local output roots from public-safe provenance.
  - [x] Record enum option values only when they are from closed vocabularies, and record free-text or path-bearing options name-only or category-only.
  - [x] Preserve public commit SHAs only when source classification explicitly allows them.
  - [x] Preserve private commit identity as redacted deterministic proof with `shaPresent: true`; public-safe packs must not silently omit commit identity.
  - [x] Represent private repository identity as omitted, category-only, redacted deterministic proof, or local-only according to claim level.
  - [x] Use existing stable hash helpers or context-separated length-prefixed SHA-256 input for safe-to-hash values.
  - [x] Omit or category-only represent secret-like, credential-like, low-entropy private, enumerable private, or source-derived values instead of hashing them in public-safe packs.

- [x] 5. Implement safety validator and generated-output sentinel. Requirements: 3, 5.
  - [x] Validate pack JSON and Markdown for local absolute paths, home fragments, private sample names, raw remotes, raw SQL, config values, connection strings, endpoint addresses, credentials, secrets, tokens, snippets, analyzer log fragments, raw diagnostics, and unescaped Markdown.
  - [x] Write local-only `validation-result.json` with validator schema version, checked file names, requested claim level, observed classification, sanitized categories, exit status, and deterministic date metadata.
  - [x] Fail with sanitized categories and generated file paths without echoing unsafe values.
  - [x] Validate public-safe packs contain only approved neutral labels, safe counts, rule IDs, evidence tiers, coverage labels, extractor versions, limitations, safe provenance, approved public SHAs, and approved safe hashes.
  - [x] Validate Markdown table cells and inline display fields are escaped or omitted.
  - [x] Add a prohibited-claim wording check over every string-valued JSON leaf and generated Markdown text for runtime execution, vulnerability status, production usage, release approval, business impact, service reachability, query execution, and contract impact.
  - [x] Version the prohibited-claim phrase list through `safety.validatorVersion` and update tests whenever the phrase list changes.
  - [x] Force the top-level pack classification to `rejected` when any included section has status `rejected`.
  - [x] Ensure tracked promoted packs pass `./scripts/check-private-paths.sh` without allowlisting private strings.
  - [x] Treat the pack safety validator as the generated-output sentinel for pack files, or extract a reusable file-targetable sentinel and document the chosen path.

- [x] 6. Add CLI or script workflow. Requirements: 3, 6.
  - [x] Add `tracemap evidence-pack create`, `tracemap evidence-pack validate`, and `tracemap evidence-pack promote`, or document a temporary script fallback and blocker in `implementation-state.md`.
  - [x] Support input kinds for raw scan output, legacy validation summary, legacy baseline manifest, and public demo summary where schemas are available.
  - [x] Require neutral `--label`, `--purpose`, `--claim-level`, explicit or fixture-pinned `--date`, and `--out`.
  - [x] Reject labels that contain path separators, URI schemes, `.git`, `@` identities, hostnames, organization/user patterns, home fragments, Windows drive prefixes, or private-looking tokens.
  - [x] Support `--dry-run` for create and promote so they report classification, gaps, destination, and planned output files without writing tracked files.
  - [x] Refuse local-only output inside the repository unless `git check-ignore` proves the destination is ignored, and treat any `git check-ignore` error or non-zero exit as refusal.
  - [x] Promote only validated public-safe JSON and Markdown files to approved tracked roots such as `docs/evidence-packs/legacy/<pack-id>/`.
  - [x] Rerun `tracemap evidence-pack validate`, the pack safety validator or generated-output sentinel, and `./scripts/check-private-paths.sh` during promotion before copying files.
  - [x] Ensure the promotion safety validator or sentinel inspects candidate JSON and Markdown files directly before copy or after staging, because `check-private-paths.sh` only inspects tracked files.
  - [x] Ensure `--force` overrides only the destination-exists check and never bypasses validation, sentinel, tracked-root, ignored-destination, or private-path gates.
  - [x] Maintain the approved tracked-root allowlist as an implementation constant, initially only `docs/evidence-packs/legacy/`.
  - [x] Reject promotion to ignored destinations or destinations outside the approved tracked root allowlist.
  - [x] Run `git check-ignore` against promotion destinations and reject zero-exit ignored paths.
  - [x] Refuse to overwrite an existing promoted pack unless an explicit `--force` option is supplied.
  - [x] Keep raw inputs and validation scratch outputs local-only.

- [x] 7. Add synthetic fixtures and tests. Requirements: 1, 2, 3, 4, 5, 6, 7.
  - [x] Create the fixture before running task 9 validation commands that reference it.
  - [x] Add a synthetic legacy evidence-pack fixture with public-safe summary inputs only, including a fixture-pinned date, two rule IDs, two evidence tiers, two fact categories, extractor versions, one gap, and one limitation.
  - [x] Test deterministic pack ID and byte-stable JSON/Markdown for identical inputs.
  - [x] Test byte stability with injected or fixture-pinned dates and failure when public-safe or demo-safe creation lacks a date.
  - [x] Test local-only pack creation without `--date` follows the documented behavior: it either succeeds with local-only timestamp metadata or fails with a deterministic diagnostic.
  - [x] Test local-only pack creation without `--date` is documented as excluded from byte-stability expectations.
  - [x] Test create adds deterministic safe suffixes when two inputs share the same base pack ID.
  - [x] Test the create suffix is computed from the normalized input fingerprint and is not self-referential to final pack JSON.
  - [x] Test promotion overwrite refusal unless `--force` is supplied.
  - [x] Test `--force` promotion with a validator-failing pack still aborts.
  - [x] Test `local-only`, `demo-safe`, `public-safe`, and `rejected` classifications.
  - [x] Test `validate --expected-claim-level public-safe` fails against a `demo-safe` or `local-only` pack.
  - [x] Test raw scan input rejection when input is not under an ignored local root.
  - [x] Test `git check-ignore` error exits and non-ignored paths are refusals, not approvals.
  - [x] Test promotion to an ignored path is rejected.
  - [x] Test raw path, remote, SQL, config value, connection string, endpoint address, snippet, log, diagnostic, secret, token, private identity, and unsafe Markdown rejection.
  - [x] Test validator diagnostics do not echo planted unsafe values.
  - [x] Test `validation-result.json` contains required local-only fields and omits unsafe values.
  - [x] Test secret-like pattern examples, including password keys, bearer tokens, JWT-shaped values, private key markers, connection strings, and credential-like long tokens.
  - [x] Test every evidence row includes rule ID, evidence tier, coverage label, source label, limitation, and safe provenance.
  - [x] Test top-level aggregate summary fields are valid when the summary object cites `legacy.evidence-pack.summary.v1`.
  - [x] Test a rejected included section forces top-level pack classification to `rejected`.
  - [x] Test reduced coverage, failed build, timeout, truncation, missing schema, missing extractor version, and unsupported section behavior.
  - [x] Test command provenance redaction and allowed public-safe fields.
  - [x] Test public-safe packs preserve commit identity proof without raw private repository identity.
  - [x] Test public-safe command provenance omits or fixed-placeholder represents `--label`.
  - [x] Test hostile `--label` values cannot flow into command provenance or Markdown.
  - [x] Test safe-to-hash, omit-only, category-only, and local-only identity boundary cases.
  - [x] Test demo-safe packs reject hashed low-entropy or enumerable private identities.
  - [x] Test Markdown rendering escapes table cells and omits unsafe display fields.
  - [x] Test prohibited claim wording checks recursively scan nested JSON strings such as `limitations[].message`.
  - [x] Test each rejected label class separately: path separator, URI scheme, `.git`, `@` identity, hostname, organization/user pattern, home fragment, Windows drive prefix, and private-looking token.
  - [x] Test standalone `validate` against an externally supplied pack JSON with no original inputs present.
  - [x] Test `promote` reruns validation and refuses non-public-safe packs.
  - [x] Test promotion fails when the candidate files contain private-path sentinel content before they are tracked.
  - [x] Test promotion fails cleanly when the approved tracked root placeholder is missing.
  - [x] Test `git check-ignore .tmp/legacy-evidence-packs/example` or the chosen local output root.

- [x] 8. Document workflow for future consumers. Requirements: 3, 6, 7.
  - [x] Add docs explaining evidence packs as redacted summaries over TraceMap artifacts, not source-of-truth scan outputs.
  - [x] Create `docs/evidence-packs/legacy/README.md` as the tracked promotion root placeholder before promotion validation.
  - [x] Document regeneration commands with neutral placeholders only.
  - [x] Document local-only, demo-safe, public-safe, and rejected claim levels.
  - [x] Document safe and unsafe wording examples for docs/site consumers.
  - [x] Document that site implementation belongs in a future `site-*` spec.
  - [x] Document validation commands and pinned smoke deferral rules.

- [x] 9. Validate implementation. Requirements: 7.
  - [x] Run focused evidence-pack tests.
  - [x] Run `dotnet build src/dotnet/TraceMap.sln`.
  - [x] Run `dotnet test src/dotnet/TraceMap.sln`.
  - [x] Run dry-run evidence-pack creation against checked-in synthetic fixtures.
  - [x] Run non-dry-run evidence-pack creation to ignored `.tmp/legacy-evidence-packs/`.
  - [x] Run `tracemap evidence-pack validate` against generated JSON.
  - [x] Run promotion validation against a public-safe fixture without committing raw inputs; this requires task 8 to have created `docs/evidence-packs/legacy/README.md`.
  - [x] Verify `rules/rule-catalog.yml` contains all six `legacy.evidence-pack.*` entries before running implementation tests that emit evidence-pack rule IDs.
  - [x] Run `git check-ignore .tmp/legacy-evidence-packs/example`.
  - [x] Run relevant pinned smoke checks from `docs/VALIDATION.md` if implementation touches scanner, shared report rendering, public demo, release review, or language adapters; otherwise record an explicit deferral.
  - [x] Run `./scripts/check-private-paths.sh`.
  - [x] Run `git diff --check`.

## Deferred Follow-Ups

- Site pages that consume promoted evidence packs.
- Hosted public evidence-pack artifacts.
- Portfolio-level packs across multiple public-safe sample labels.
- Optional local-only drilldown packs for private validation sessions.
- Visual exploration over `legacy-evidence-pack.v1` JSON.
