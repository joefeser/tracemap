# Vault Export Hidden Safety Tasks

## Implementation Tasks

This implementation PR addresses the hidden/local evidence-location failure
path from issue #171 and keeps unchecked items as remaining scope for the full
approved spec.

- [ ] 1. Define the hidden/local safety classifier. Requirements: 1, 2, 3.
  - [x] Add a closed value-context enum for repo-relative paths, evidence
        locations, symbol display names, route/action/model/member names,
        stable TraceMap IDs, rule IDs, closed-vocabulary labels, diagnostics,
        and raw external/data values.
  - [ ] Define classifier outcomes: allow raw, allow hash, allow category,
        omit with gap, and reject.
  - [ ] Define `StableTraceMapId` as raw only for already-stable internal IDs
        produced by TraceMap after source validation; otherwise transform under
        the original value context before ID construction.
  - [ ] Validate every source component under its semantic context before using
        it in stable ID construction; omit the node or edge and emit a safety
        gap when a required component rejects.
  - [x] Preserve strict public-safe/demo-safe behavior for secret-like strings.
  - [x] Add hard-fail handling for raw secrets, credentials, connection
        strings, local absolute paths, raw remotes, raw URLs, raw SQL, snippets,
        private sample identifiers, and production data.
  - [x] Strengthen hard-fail detection for traversal segments, temp roots, UNC
        paths, drive-rooted paths, home shorthand, environment-home prefixes,
        API keys, private keys, authorization headers, and session identifiers.

- [ ] 2. Implement hidden/local safe-context transforms. Requirements: 2, 4, 5.
  - [x] Normalize and validate repo-relative paths without allowing absolute
        paths, home fragments, drive roots, URI forms, UNC paths, temp roots, or
        traversal segments.
  - [x] Normalize evidence locations into safe relative path/span, category, or
        hash representations.
  - [ ] Normalize symbol, route, action, model, and member display names with
        bounded printable validation.
  - [ ] Preserve safe repo-relative paths and evidence locations when useful
        for local navigation, and prefer category or context hash for display
        names when raw hidden/local display is not needed.
  - [x] Treat safe action/member names containing SQL action words as display
        names, not raw SQL, when they are not SQL text.
  - [x] Treat safe repo-relative paths containing SQL action words as paths, not
        raw SQL, when they are not SQL text.
  - [ ] Implement stable ID construction and transformation from validated
        components, ensuring nodes or edges are omitted and safety gaps are
        emitted on component rejection.
  - [x] Ensure transforms happen before final Markdown and `graph.json`
        validation and before any files are written.

- [ ] 3. Integrate transforms with final generated-output validation.
      Requirements: 1, 2, 3, 5.
  - [x] Replace or extend context-free string validation so each generated JSON
        leaf and Markdown line is validated with claim level, value context, and
        classifier decision.
  - [x] Keep public/demo validation strict for every rendered string.
  - [x] Allow classifier-approved hidden raw values only in their approved
        contexts.
  - [x] Ensure hash/category/gap labels are safe strings that cannot
        self-trigger final validation.
  - [x] Pre-validate closed-vocabulary labels, gap kinds, rule IDs,
        frontmatter enum values, and diagnostic categories before they can be
        emitted.
  - [ ] Fail any unclassified rendered string rather than bypassing validation.

- [ ] 4. Add exporter-created safety gaps and limitations. Requirements: 4, 7.
  - [x] Reuse `vault-export.validation.unsafe-value-rejected.v1` for rejected
        unsafe values unless a new validation rule is proven necessary.
  - [ ] Reuse or extend `vault-export.gap.unsafe-symbol-omitted.v1` for hidden
        display-name hash/category behavior when its limitation fits.
  - [x] Add documented `vault-export.*.v1` rule IDs only for genuinely new
        non-symbol hidden safe-context omissions or category-only evidence
        locations.
  - [x] Add rule catalog entries with purpose, evidence tier, and limitations.
  - [x] Emit `Tier4Unknown` safety gaps unless a stronger tier is explicitly
        justified by static evidence.
  - [ ] Mark hidden/local exports partial when safety omissions affect graph
        interpretation.
  - [x] Keep claim-level hidden-evidence omission gaps distinct from hidden
        safety gaps.

- [ ] 5. Preserve deterministic rendering. Requirements: 5, 6.
  - [ ] Document and implement context-separated hash prefixes and truncation
        length.
  - [x] Document exact hash truncation length for each value context in
        exporter constants.
  - [ ] Keep node, edge, gap, limitation, link, tag, frontmatter, and array
        ordering deterministic after safety transforms.
  - [x] Recompute Markdown content hashes and `graph.json` content hash after
        all transforms.
  - [x] Prove output bytes are stable across reruns and output roots.
  - [x] Ensure validation failures leave existing output unchanged.

- [ ] 6. Preserve generated file collision behavior. Requirements: 6.
  - [x] Keep valid generated file replacement behavior after new safety checks.
  - [x] Keep stale generated file failure unless `--force` is supplied.
  - [x] Prove `--force` does not bypass claim-level, redaction, schema, raw
        secret, local path, private-path, or new-content safety gates; it only
        permits replacing stale generated files after new content passes.
  - [x] Keep non-generated user note collision failure in every claim level.

- [ ] 7. Add focused tests. Requirements: 1, 2, 3, 4, 5, 6, 8.
  - [x] Hidden/local export succeeds with safe secret-like repo-relative file
        paths.
  - [x] Hidden/local export succeeds with safe secret-like member, model,
        route/action, symbol, and evidence-location names.
  - [x] Public-safe and demo-safe exports reject or filter the same
        secret-like safe-context names according to strict policy.
  - [x] Raw secret material rejects in hidden/local and public/demo modes
        without echoing the value.
  - [x] Local absolute paths reject in hidden/local and public/demo modes.
  - [x] Raw remotes, raw URLs, raw SQL, source snippets, connection strings,
        captured credentials, private sample identifiers, and production data
        are not rendered.
  - [x] Traversal segments, temp roots, UNC paths, drive-rooted paths, home
        shorthand, environment-home prefixes, API keys, private keys,
        authorization headers, and session identifiers are rejected.
  - [x] Classifier-approved hidden raw values survive final validation in both
        Markdown and `graph.json`.
  - [x] Hash/category/gap labels do not self-trigger unsafe-value validation.
  - [x] Hidden safe display names containing SQL action words are not
        misclassified as raw SQL.
  - [x] Hidden safe repo-relative paths containing SQL action words are not
        misclassified as raw SQL.
  - [ ] Hidden display names that are empty, whitespace-only, or contain only
        non-printable characters are rejected or represented by an approved
        category/gap.
  - [ ] Rejected stable ID source components omit the affected node or edge and
        emit a sanitized safety gap without using the rejected raw value.
  - [x] Path normalization, hash input encoding, and LF normalization are stable
        across platform-style path separators and line endings.
  - [x] The same inputs that succeed as hidden fail or filter appropriately when
        rerun as demo-safe or public-safe.
  - [x] Safety gap IDs, ordering, and content are stable across reruns.
  - [ ] Safety omissions mark the export partial when graph interpretation is
        affected.
  - [x] Claim-level hidden-evidence omission gaps remain distinct from hidden
        safety gaps when both are present.
  - [x] Deterministic Markdown and `graph.json` bytes are stable across reruns
        and output roots.
  - [x] Generated collision tests cover valid generated replacement, stale
        generated file failure, `--force`, and non-generated user files.
  - [x] Every exporter-created safety gap has a documented rule ID and evidence
        tier.

- [x] 8. Update docs. Requirements: 7.
  - [x] Update `docs/VAULT_EXPORT.md` with strict public/demo behavior,
        hidden/local safe-context behavior, hard-fail categories, gap/category
        behavior, and validation commands.
  - [x] Use only generic placeholders and synthetic examples.
  - [x] Avoid site files and site specs.

- [x] 9. Validate implementation. Requirements: 8.
  - [x] Run `dotnet test src/dotnet/TraceMap.sln --filter VaultExport`.
  - [x] Run broader `dotnet test src/dotnet/TraceMap.sln`, or document the
        reason if deferred.
  - [x] Run `./scripts/check-private-paths.sh`.
  - [x] Run `git diff --check`.

## Deferred Follow-Ups

- Public/demo safe-context relaxation, if future evidence warrants it.
- Shared safety helper across vault export and evidence pack validation.
- Optional local-only debug logs outside generated vault output.
- Site consumption of future public-safe summaries.
