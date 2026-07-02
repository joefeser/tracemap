# Swift Real-World API-Client Smoke Requirements

Status: implemented
Public claim level: demo after generated summaries are reviewed for publication.

## Context

GitHub issue #431 requested pinned public Swift/iOS repositories that call real
backend systems, so Swift v0 validation can move beyond checked-in fixtures.

## Requirements

1. The smoke SHALL clone pinned public Swift repositories into an operator
   supplied cache directory.
2. The smoke SHALL scan each selected repository with `tracemap-swift scan`.
3. The smoke SHALL verify each scan produced `scan-manifest.json`,
   `facts.ndjson`, `index.sqlite`, `report.md`, and `logs/analyzer.log`.
4. The smoke SHALL write sanitized local summaries with repository slugs,
   pinned commit SHAs, artifact labels, counts, rule IDs, coverage labels, and
   limitations.
5. Generated summaries SHALL NOT include local absolute paths, clone URLs, raw
   remotes, raw source snippets, raw SQL, credentials, config values,
   hostnames, private labels, or runtime observations.
6. The smoke SHALL remain static evidence only: no Xcode builds, SwiftPM
   dependency resolution, simulators, devices, app execution, network calls,
   credentials, auth flows, production telemetry, LLM calls, embeddings, vector
   databases, or prompt-based classification.
7. Documentation SHALL list the pinned repositories, SHAs, reason for inclusion,
   command, focus labels, and limitations.
