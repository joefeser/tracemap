# Tasks

- [x] 1. Add local-only legacy validation manifest support.
  - Define `.tmp/legacy-codebase-validation/repos.local.json` as the only
    accepted path source.
  - Validate labels and reject unsafe/public output candidates.
  - Reject or fail validation if `.tmp/legacy-codebase-validation/` files become
    git-tracked.
  - Ensure local paths never appear in committed files.

- [x] 2. Add a legacy validation script.
  - Add `scripts/validate-legacy-codebases.sh`.
  - Run `tracemap scan` per sample label.
  - Capture exit code, duration, artifact existence, fact counts, coverage
    labels, build/project-load state, and analyzer gap counts.
  - Enforce default bounds of 20 minutes per sample and 500 MB per sample output
    directory unless `timeoutSeconds` or `maxArtifactBytes` are supplied in the
    ignored local manifest.

- [x] 3. Add legacy UI event evidence probes.
  - Query method declaration facts for handler-like symbols or contract
    elements.
  - Query call-edge facts from handler-like methods to downstream methods or
    dependency surfaces.
  - Search safe fact properties for event wiring tokens such as `+=`, `Click`,
    `OnClick`, and `InitializeComponent` without rendering raw snippets.
  - Distinguish semantic, structural, syntax/text, and missing-evidence cases.
  - Record follow-up gaps when current scanner output is insufficient; an
    all-gaps UI event result is acceptable and should feed a
    `legacy-ui-event-surfaces` follow-up.
  - State that static handler wiring does not prove runtime execution.

- [x] 4. Add large repository smoke reporting.
  - Capture scan duration, output size, fact count, coverage, and truncation or
    failure reasons for the large sample label.
  - Mark timeout or artifact-size bound exceedance as truncated/deferred with a
    visible limitation.

- [x] 5. Add redacted summary generation.
  - Write Markdown and JSON summaries under ignored `.tmp/`.
  - Include sample labels, counts, coverage labels, rule IDs, and limitations.
  - Exclude raw paths, remotes, snippets, SQL, config values, and secrets.

- [x] 6. Add safety tests.
  - Test manifest validation rejects non-`.tmp` paths for output.
  - Test redaction rejects local absolute paths, private path fragments, raw
    remotes, private repo names, raw SQL, connection strings, config values,
    secrets, and source snippets.
  - Test validation fails if `.tmp/legacy-codebase-validation/` files are
    accidentally git-tracked.
  - Test summary shape remains deterministic.

- [x] 7. Validate.
  - Run `dotnet build src/dotnet/TraceMap.sln`.
  - Run `dotnet test src/dotnet/TraceMap.sln`.
  - Run `./scripts/check-private-paths.sh`.
  - Run `git diff --check`.
  - Run local legacy validation only from ignored `.tmp/` inputs.
