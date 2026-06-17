# Vault Export Hidden Safety Tasks

## Implementation Tasks

This spec PR is spec-only. Leave implementation tasks unchecked until a later
product-code PR performs the work.

- [ ] 1. Define the hidden/local safety classifier. Requirements: 1, 2, 3.
  - [ ] Add a closed value-context enum for repo-relative paths, evidence
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
  - [ ] Preserve strict public-safe/demo-safe behavior for secret-like strings.
  - [ ] Add hard-fail handling for raw secrets, credentials, connection
        strings, local absolute paths, raw remotes, raw URLs, raw SQL, snippets,
        private sample identifiers, and production data.
  - [ ] Strengthen hard-fail detection for traversal segments, temp roots, UNC
        paths, drive-rooted paths, home shorthand, environment-home prefixes,
        API keys, private keys, authorization headers, and session identifiers.

- [ ] 2. Implement hidden/local safe-context transforms. Requirements: 2, 4, 5.
  - [ ] Normalize and validate repo-relative paths without allowing absolute
        paths, home fragments, drive roots, URI forms, UNC paths, temp roots, or
        traversal segments.
  - [ ] Normalize evidence locations into safe relative path/span, category, or
        hash representations.
  - [ ] Normalize symbol, route, action, model, and member display names with
        bounded printable validation.
  - [ ] Preserve safe repo-relative paths and evidence locations when useful
        for local navigation, and prefer category or context hash for display
        names when raw hidden/local display is not needed.
  - [ ] Treat safe action/member names containing SQL action words as display
        names, not raw SQL, when they are not SQL text.
  - [ ] Treat safe repo-relative paths containing SQL action words as paths, not
        raw SQL, when they are not SQL text.
  - [ ] Ensure transforms happen before final Markdown and `graph.json`
        validation and before any files are written.

- [ ] 3. Integrate transforms with final generated-output validation.
      Requirements: 1, 2, 3, 5.
  - [ ] Replace or extend context-free string validation so each generated JSON
        leaf and Markdown line is validated with claim level, value context, and
        classifier decision.
  - [ ] Keep public/demo validation strict for every rendered string.
  - [ ] Allow classifier-approved hidden raw values only in their approved
        contexts.
  - [ ] Ensure hash/category/gap labels are safe strings that cannot
        self-trigger final validation.
  - [ ] Pre-validate closed-vocabulary labels, gap kinds, rule IDs,
        frontmatter enum values, and diagnostic categories before they can be
        emitted.
  - [ ] Fail any unclassified rendered string rather than bypassing validation.

- [ ] 4. Add exporter-created safety gaps and limitations. Requirements: 4, 7.
  - [ ] Reuse `vault-export.validation.unsafe-value-rejected.v1` for rejected
        unsafe values unless a new validation rule is proven necessary.
  - [ ] Reuse or extend `vault-export.gap.unsafe-symbol-omitted.v1` for hidden
        display-name hash/category behavior when its limitation fits.
  - [ ] Add documented `vault-export.*.v1` rule IDs only for genuinely new
        non-symbol hidden safe-context omissions or category-only evidence
        locations.
  - [ ] Add rule catalog entries with purpose, evidence tier, and limitations.
  - [ ] Emit `Tier4Unknown` safety gaps unless a stronger tier is explicitly
        justified by static evidence.
  - [ ] Mark hidden/local exports partial when safety omissions affect graph
        interpretation.
  - [ ] Keep claim-level hidden-evidence omission gaps distinct from hidden
        safety gaps.

- [ ] 5. Preserve deterministic rendering. Requirements: 5, 6.
  - [ ] Document and implement context-separated hash prefixes and truncation
        length.
  - [ ] Document exact hash truncation length for each value context in
        exporter constants.
  - [ ] Keep node, edge, gap, limitation, link, tag, frontmatter, and array
        ordering deterministic after safety transforms.
  - [ ] Recompute Markdown content hashes and `graph.json` content hash after
        all transforms.
  - [ ] Prove output bytes are stable across reruns and output roots.
  - [ ] Ensure validation failures leave existing output unchanged.

- [ ] 6. Preserve generated file collision behavior. Requirements: 6.
  - [ ] Keep valid generated file replacement behavior after new safety checks.
  - [ ] Keep stale generated file failure unless `--force` is supplied.
  - [ ] Prove `--force` does not bypass claim-level, redaction, schema, raw
        secret, local path, private-path, or new-content safety gates; it only
        permits replacing stale generated files after new content passes.
  - [ ] Keep non-generated user note collision failure in every claim level.

- [ ] 7. Add focused tests. Requirements: 1, 2, 3, 4, 5, 6, 8.
  - [ ] Hidden/local export succeeds with safe secret-like repo-relative file
        paths.
  - [ ] Hidden/local export succeeds with safe secret-like member, model,
        route/action, symbol, and evidence-location names.
  - [ ] Public-safe and demo-safe exports reject or filter the same
        secret-like safe-context names according to strict policy.
  - [ ] Raw secret material rejects in hidden/local and public/demo modes
        without echoing the value.
  - [ ] Local absolute paths reject in hidden/local and public/demo modes.
  - [ ] Raw remotes, raw URLs, raw SQL, source snippets, connection strings,
        captured credentials, private sample identifiers, and production data
        are not rendered.
  - [ ] Traversal segments, temp roots, UNC paths, drive-rooted paths, home
        shorthand, environment-home prefixes, API keys, private keys,
        authorization headers, and session identifiers are rejected.
  - [ ] Classifier-approved hidden raw values survive final validation in both
        Markdown and `graph.json`.
  - [ ] Hash/category/gap labels do not self-trigger unsafe-value validation.
  - [ ] Hidden safe display names containing SQL action words are not
        misclassified as raw SQL.
  - [ ] Hidden safe repo-relative paths containing SQL action words are not
        misclassified as raw SQL.
  - [ ] Hidden display names that are empty, whitespace-only, or contain only
        non-printable characters are rejected or represented by an approved
        category/gap.
  - [ ] Rejected stable ID source components omit the affected node or edge and
        emit a sanitized safety gap without using the rejected raw value.
  - [ ] Path normalization, hash input encoding, and LF normalization are stable
        across platform-style path separators and line endings.
  - [ ] The same inputs that succeed as hidden fail or filter appropriately when
        rerun as demo-safe or public-safe.
  - [ ] Safety gap IDs, ordering, and content are stable across reruns.
  - [ ] Safety omissions mark the export partial when graph interpretation is
        affected.
  - [ ] Claim-level hidden-evidence omission gaps remain distinct from hidden
        safety gaps when both are present.
  - [ ] Deterministic Markdown and `graph.json` bytes are stable across reruns
        and output roots.
  - [ ] Generated collision tests cover valid generated replacement, stale
        generated file failure, `--force`, and non-generated user files.
  - [ ] Every exporter-created safety gap has a documented rule ID and evidence
        tier.

- [ ] 8. Update docs. Requirements: 7.
  - [ ] Update `docs/VAULT_EXPORT.md` with strict public/demo behavior,
        hidden/local safe-context behavior, hard-fail categories, gap/category
        behavior, and validation commands.
  - [ ] Use only generic placeholders and synthetic examples.
  - [ ] Avoid site files and site specs.

- [ ] 9. Validate implementation. Requirements: 8.
  - [ ] Run `dotnet test src/dotnet/TraceMap.sln --filter VaultExport`.
  - [ ] Run broader `dotnet test src/dotnet/TraceMap.sln`, or document the
        reason if deferred.
  - [ ] Run `./scripts/check-private-paths.sh`.
  - [ ] Run `git diff --check`.

## Deferred Follow-Ups

- Public/demo safe-context relaxation, if future evidence warrants it.
- Shared safety helper across vault export and evidence pack validation.
- Optional local-only debug logs outside generated vault output.
- Site consumption of future public-safe summaries.
