# Tasks

- [ ] 1. Add local-only legacy validation manifest support.
  - Define `.tmp/legacy-codebase-validation/repos.local.json` as the only
    accepted path source.
  - Validate labels and reject unsafe/public output candidates.
  - Ensure local paths never appear in committed files.

- [ ] 2. Add a legacy validation script.
  - Add `scripts/validate-legacy-codebases.sh`.
  - Run `tracemap scan` per sample label.
  - Capture exit code, duration, artifact existence, fact counts, coverage
    labels, build/project-load state, and analyzer gap counts.

- [ ] 3. Add legacy UI event evidence probes.
  - Query current indexes for event-handler-like facts.
  - Distinguish semantic, structural, syntax/text, and missing-evidence cases.
  - Record follow-up gaps when current scanner output is insufficient.

- [ ] 4. Add large repository smoke reporting.
  - Capture scan duration, output size, fact count, coverage, and truncation or
    failure reasons for the large sample label.

- [ ] 5. Add redacted summary generation.
  - Write Markdown and JSON summaries under ignored `.tmp/`.
  - Include sample labels, counts, coverage labels, rule IDs, and limitations.
  - Exclude raw paths, remotes, snippets, SQL, config values, and secrets.

- [ ] 6. Add safety tests.
  - Test manifest validation rejects non-`.tmp` paths for output.
  - Test redaction rejects local absolute paths and private path fragments.
  - Test summary shape remains deterministic.

- [ ] 7. Validate.
  - Run `dotnet build src/dotnet/TraceMap.sln`.
  - Run `dotnet test src/dotnet/TraceMap.sln`.
  - Run `./scripts/check-private-paths.sh`.
  - Run `git diff --check`.
  - Run local legacy validation only from ignored `.tmp/` inputs.

