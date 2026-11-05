# Workspace configuration reference

The workspace uses strict JSON in `workspace.local.json` under
the explicit `--root`. Unknown and duplicate properties fail. Begin with
[Start a source collection workspace](WORKSPACE_GETTING_STARTED.md).

For existing wizard setup plus offline SQL, [init-from-wizard](WORKSPACE_FROM_WIZARD.md)
creates a verified V3 config from the selected saved prepared attempt and manifest.
It preserves existing configuration and performs no scan or attestation.

## Configuration

| Field | Meaning |
| --- | --- |
| `schemaVersion` | Required: `tracemap-workspace.v1` for source-only, or `tracemap-workspace.v2` for prepared native fresh collection, or `tracemap-workspace.v3` for SQL definitions with optional compiled collection |
| `repositories` | Required: 1–64 configured source repositories for verify/collect; init creates an empty template |
| `scopeFile` | Optional repository-ID filter file; resolved relative to the workspace |
| `webFormsReport` | Optional, default `false`; render a Web Forms modernization packet after the combined report |
| `templateProvenance` | Optional init-template generator/input SHA-256 values; does not attest subsequent edits |
| `compiledReview` | Required in V2, optional in V3, unavailable in V1: `{ "repositoryId": "web", "nativeConfigPath": "native-evidence/review-config.local.json" }`; see the [compiled guide](WORKSPACE_COMPILED_GETTING_STARTED.md) |
| `sqlEvidence` | Required in V3, unavailable in V1/V2: `{ "repositoryId": "web", "manifestPath": "sql-input.local.json", "sourceRoot": "sql-inputs" }`; see the [SQL guide](WORKSPACE_SQL_GETTING_STARTED.md) |

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

Broader SQL dump compatibility, application build/publish orchestration, workspace
retained-source attachment, retained-scan reuse and resume are follow-up
contracts. The workspace rejects
invented fields for those capabilities instead of silently ignoring them.

## V3 offline SQL definitions

`tracemap-workspace.v3` requires `sqlEvidence: { repositoryId, manifestPath, sourceRoot }`
and permits the existing optional `compiledReview`. SQL context must match a selected
repository ID and HEAD. It produces a separate local SQL sidecar, never a raw scan
merge or automatic source-call binding. See [SQL collection](WORKSPACE_SQL_GETTING_STARTED.md)
for the exact manifest, bounds, supported subset and known refusals.

The SQL manifest can also use the separate [whole-batch selection input](SQL_DEFINITION_SELECTION.md)
for larger original install templates; workspace records bind the original file hashes.

## Scope preview before collection

`workspace verify` and `workspace collect` print a local scope preview after input
admission and before any collection stage. It lists repositories omitted by the
ID filter, each selected root and selection mode, explicit project/solution paths,
include/exclude globs and target framework. For native collection it shows the
native source folders, page mode/list, publication root, primary/dependency DLLs,
PDBs and maps. Native configuration remains the sole authority for web selection.

A workspace source project selection requires `.csproj`, `.vbproj` or `.fsproj`; a solution selection
requires `.sln` or `.slnx`. Putting a solution in `projects`, a project in `solution`,
or an unrelated file in either field refuses with `WORKSPACE_SELECTION_FILE_KIND`
before scanning. File-kind validation does not parse project contents or guarantee
MSBuild loading. Native solution admission retains its own stricter contract.

The preview also looks at **at most 257 immediate names** under each selected
source root; it never recurses, reads project files or follows child links. At
more than 256 entries it reports `WORKSPACE_ROOT_PREVIEW_LIMIT` and omits that
root-name list rather than displaying an arbitrary subset. This preview does not
measure nested scope or decide what constitutes the complete application.

Other warnings are advisory and never change selection automatically:

- `WORKSPACE_DISCOVERY_SELECTION`: multiple root project/solution names with
  discovery selected; consider making project/solution selection explicit.
- `WORKSPACE_WEBSITE_WITHOUT_ROOT_PROJECT`: web.config without an immediate project
  name in discovery mode; possible projectless website or nested projects. This
  does not establish projectless semantic compilation.
- `WORKSPACE_PROJECTLESS_WITH_ROOT_PROJECT`: native projectless mode coexists with
  immediate project names; confirm the intended application type.
- `WORKSPACE_SOLUTION_SCOPE`: source solution selection does not establish coverage
  of projects outside that solution.
- `WORKSPACE_PAGE_SCOPE_IS_NOT_REPOSITORY_SCOPE`: native all/selected pages remain
  within declared scope, not a complete repo or historical compiler-input closure.

Review this output before collecting. Warnings are configuration diagnostics, not
new evidence facts or a completeness verdict. The preview writes no new artifact;
all paths remain local. Existing run records, receipt admission, source discovery,
semantic fallback and failure/partial policies are unchanged. No automatic repair,
project-type conversion or widening of configured scope occurs.

For a bounded work-machine check of an existing config without collection, use
[the verification-only prompt](../prompts/verify-workspace-scope-before-collection.md).
