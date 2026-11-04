# Workspace configuration reference

The workspace uses strict JSON in `workspace.local.json` under
the explicit `--root`. Unknown and duplicate properties fail. Begin with
[Start a source collection workspace](WORKSPACE_GETTING_STARTED.md).

## Configuration

| Field | Meaning |
| --- | --- |
| `schemaVersion` | Required: `tracemap-workspace.v1` for source-only, or `tracemap-workspace.v2` for prepared native fresh collection |
| `repositories` | Required: 1–64 configured source repositories for verify/collect; init creates an empty template |
| `scopeFile` | Optional repository-ID filter file; resolved relative to the workspace |
| `webFormsReport` | Optional, default `false`; render a Web Forms modernization packet after the combined report |
| `templateProvenance` | Optional init-template generator/input SHA-256 values; does not attest subsequent edits |
| `compiledReview` | V2 only and required in v2: `{ "repositoryId": "web", "nativeConfigPath": "native-evidence/review-config.local.json" }`; see the [compiled guide](WORKSPACE_COMPILED_GETTING_STARTED.md) |

`schemaVersion: tracemap-workspace.v1` remains source-only. V2 requires one prepared
native fresh config and its selected web repository. Native configuration owns
web project/folder/page selection; workspace web project/solution/glob/target
overrides are rejected. Framework entries retain their normal selection fields.
No native config is rewritten and no correspondence attestation is generated.

Each repository has:

| Field | Meaning |
| --- | --- |
| `id` | Required unique lowercase identifier, beginning with a letter; letters, digits and hyphens, up to 48 characters |
| `path` | Required local source directory; relative paths resolve from the workspace |
| `projects` | Required array of repository-relative project paths, at most 64; `[]` delegates discovery/fallback to the scanner |
| `solution` | Optional repository-relative solution path; cannot be combined with nonempty `projects` |
| `include`, `exclude` | Optional arrays of scanner file globs; distinct from the repository-ID filter |
| `targetFramework` | Optional application-project target selection, passed to scan; does not change TraceMap's own target framework |

Example explicit project selection:

```json
{
  "id": "framework",
  "path": "C:/source/GenericFramework",
  "projects": ["src/GenericFramework.csproj"],
  "include": [],
  "exclude": []
}
```

Sources need Git commits. Verification checks only selected repositories' paths,
projects and HEADs; excluded repositories are not scanned. Existing path aliases
are resolved before output/input overlap checks. Paths that cannot be resolved
or change to linked paths during checking are refused. Output must be fresh and
must not overlap a selected source, the configuration files or the running tool.

## Overrides and limits

- `--scope` replaces `scopeFile` for that invocation. The CLI path is relative to
  the current shell; entries are configured IDs, with blank lines and `#` comments.
- Config paths resolve from the workspace; project/solution paths from their
  selected repository. No configuration depends on persistent shell variables.
- Configuration is bounded to 1 MiB and a scope file to 64 KiB. JSON nesting uses
  the standard parser limit. Unknown options and repeated options fail.
- Both commands should use the same filter. Collect independently repeats
  verification; a previous verify result is not an authorization receipt.
- The launcher defaults to Release. Use `-BuildConfiguration Debug` for both its
  preparation and later calls if you intentionally choose Debug. SDK availability
  and application-project toolchains remain separate prerequisites.

## Run record

Source-only `workspace-run.local.json` uses `tracemap-workspace-run.v1` and rule
`workflow.workspace.source-collection.v1`. It records `outcome`, `stage`, source
commits/build status/analysis level, exact CLI assembly SHA-256 and an input map.
The input-map values hash the actual configuration, optional scope bytes,
selected repository path/HEAD pairs, consumed scan manifests/indexes and the
combined index when those stages finish. `boundedInputSha256` hashes that
ordinally sorted map serialized as indented UTF-8 JSON using the CLI's standard
System.Text.Json encoder. Failed runs record only inputs reached before failure.

The record is local-only, not a shareable evidence projection or a replacement
for scanner receipts. `partial` means orchestration finished with reduced source
analysis, unsuccessful/unattempted build status, or known scan gaps. `completed`
still does not claim complete extraction, runtime validation, compiled binding,
SQL contracts or migration readiness. No application build/restore/fetch is
requested by this orchestration. Normal scanner project evaluation is unchanged.

V2 records use `tracemap-workspace-run.v2` and
`workflow.workspace.compiled-collection.v1`. The input map adds the exact native
input fingerprints and completed preflight/checkpoint hashes. `compiledReview`
records the admitted raw scan and native report bundle locations and hashes.
The existing native completed-run verifier rechecks the checkpoint chain,
artifacts and runtime identity; native and framework inputs are rechecked after
combination and before completion. The combined index itself is also rechecked.
V2 remains partial because static/native correspondence gaps remain authoritative.

Private SQL admission, application build/publish orchestration, workspace
retained-source attachment, retained-scan reuse and resume are follow-up
contracts. The workspace rejects
invented fields for those capabilities instead of silently ignoring them.
