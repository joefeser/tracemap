# Package Upgrade Impact Design

## Command

Add:

```text
tracemap package-impact --index <index.sqlite> --package-delta <delta.json> --out <path> [--format <markdown|json>] [selectors]
```

Selectors:

- `--source <label>` filters one combined source label or the single-index default label.
- `--package <name>` filters delta changes by package name.
- `--ecosystem <name>` filters delta changes by ecosystem.
- `--max-findings <n>` caps evidence rows.
- `--max-gaps <n>` caps gap rows.
- `--exit-code` returns `1` when static package evidence findings are present.

## Delta Schema

MVP accepts:

```json
{
  "version": "package-delta.v1",
  "sourceRepo": "https://example.invalid/org/repo.git",
  "sourceCommitSha": "0123456789abcdef0123456789abcdef01234567",
  "changes": [
    {
      "id": "pkg-1",
      "packageName": "Newtonsoft.Json",
      "ecosystem": "nuget",
      "changeType": "updated",
      "oldVersion": "13.0.1",
      "newVersion": "13.0.3"
    }
  ]
}
```

`sourceRepo`, `sourceCommitSha`, `ecosystem`, `oldVersion`, and `newVersion` are optional. `sourceRepo` may also appear on an individual change to override the top-level delta source. Repository provenance is hashed in reports; raw repository values are not rendered. Version values are rendered only if they pass the existing safe-version shape; otherwise TraceMap renders a hash.

Supported ecosystem names are plain strings. V1 documents and tests the required ecosystem families `nuget`, `npm`, `maven`/`gradle`, and `pip`, but matching remains evidence-driven: a package only appears in findings when an adapter emitted deterministic package evidence for that ecosystem.

## Evidence Model

The reporter reuses `CombinedSurfaceProjection` through `CombinedDependencyReporter.BuildSurfaces` so package evidence has the same safe projection used by dependency report, diff, impact, reverse, portfolio, and snapshot diff.

Single indexes are adapted into `CombinedFactRow` with source label `default`; combined indexes preserve `index_sources.label`.

Candidate rows are limited to real `PackageReferenced` facts with non-empty package names. This includes lockfile package rows if an adapter emits them as `PackageReferenced`. Config-key surfaces can also use `package-config` in other reports, but they are not package-upgrade findings.

Import/usage and package-call evidence is adapter-dependent. V1 may match those evidence families only when an adapter emits deterministic facts with stable package identity, rule IDs, and documented limitations. The current implementation does not synthesize package usage from raw imports or calls.

Findings carry:

- package-impact rule ID: `package.upgrade.impact.v1`
- extractor rule ID from the matching package fact
- evidence tier from the matching package fact
- file path and line span from the matching package fact
- scan ID and commit SHA from the matching source
- safe package metadata only
- hashed delta source provenance when present

## Coverage

Coverage is reduced when any source has non-succeeded build status, reduced analysis level, known gaps, missing/unknown commit SHA, or explicit package evidence ambiguity. No-match changes under reduced coverage emit `UnknownAnalysisGap`.

## Output

Markdown renders summary, package findings, gaps, sources, and limitations. JSON uses camelCase property names and stable sorted arrays. No generated timestamps are emitted.

## Non-Goals

- Registry, vulnerability, license, changelog, or release-note lookup.
- Compatibility scoring.
- Transitive dependency solving.
- Runtime package loading or deployment inference.
- New language-adapter package extraction in this slice.
