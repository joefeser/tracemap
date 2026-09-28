# Native compiled Web Forms workflow

Status: first slice — preflight and private run contract only.

The proven PowerShell proof/report workflow remains supported. This command does
not replace it or execute source scans, compiled binding, graph traversal,
reports, publishing, resume or cleanup:

```text
tracemap webforms-review preflight --config <private-json> --out <new-durable-run-root>
```

The new output must not exist and must be outside source, publish and retained
parent scan roots (and must not contain those roots). It contains only
`run-manifest.json` and `README.md`. There is no timestamp/TEMP search. The
manifest owns this run ID, explicit input hashes, effective budgets, gaps and
pending phase states. Its successful creation is **not a successful scan**.
Retain the external dependencies; relocation and cleanup are not supported yet.

## Private configuration

This is a public-safe shape example, not a runnable private configuration. Replace
the roots and commit with actual values. Root paths are absolute; every declared
file is relative to its configured root. No assembly discovery or upload occurs.

```json
{
  "schemaVersion": "webforms-compiled-review-config.v1",
  "operation": "fresh",
  "sourceRoot": "/authorized/source",
  "sourceCommitSha": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
  "projectMode": "projectless",
  "solutionRelativePath": null,
  "projectRelativePaths": [],
  "sourceFolders": ["."],
  "pageMode": "selected",
  "pageRelativePaths": ["Pages/Lookup.aspx"],
  "publishedRoot": "/authorized/published",
  "primaryAssemblies": ["bin/App_Code.dll", "bin/App_Web_public.dll"],
  "dependencyAssemblies": ["bin/PublicFramework.dll"],
  "bindingReceipts": [],
  "pdbInputs": [],
  "pageMaps": [],
  "parentScanRoot": null,
  "budgets": {
    "maxInputFiles": 128,
    "maxAssemblyBytes": 67108864,
    "maxRetainedArtifactBytes": 17179869184,
    "maxTotalHashBytes": 68719476736,
    "ilMaxWork": 30000000,
    "graphMaxDepth": 20,
    "graphMaxPaths": 256,
    "graphMaxWork": 2000000,
    "metadataMaxWork": 500000,
    "metadataMaxText": 8192,
    "ilMaxText": 16384,
    "maxParentFacts": 5000000,
    "maxFactLineChars": 1048576
  }
}
```

- `fresh` inventories inputs for a future source-plus-compiled run.
- `attach` requires `parentScanRoot`. Its five required scan artifacts are hashed
  using streaming reads. Source commit and parent manifest commit must match;
  parent repository/index identity and source snapshot validation remain deferred.
  A derived run never appends files to or modifies the parent scan.
- `projectMode` is exactly `projectless`, `solution` with one solution path, or
  `projects` with a nonempty native JSON path array. Selected files must exist.
- `sourceFolders` is a bounded array of source-relative directories; `.` explicitly
  selects the source root. These declarations are preserved for future execution.
- `pageMode` is `selected` with nonempty `.aspx` paths, or `all` with an empty page
  list. This preflight does not claim that all-page extraction/reporting works.
- Assemblies, receipts, PDBs and maps are explicit lists under the published root.
  Scattered/external DLLs are deliberately not discovered; explicit external
  locator support belongs to a later configuration slice.
- The managed PE header is inspected without loading/executing the assembly.
  Architecture-specific dependencies do not require a runtime load for this check.
- Receipt schema and artifact-hash candidates are inspected, not admitted as
  bindings. Missing/ambiguous candidates remain explicit gaps. Authoritative
  identity, safe-locator, repository, build and commit-relation validation remains
  pending under the existing scanner policy. A candidate never establishes source
  ownership or source-line identity.
- PDB/map bytes are hashed, not semantically validated. Missing optional inputs
  and deferred validations are visible gaps, not inferred clean coverage.

## Limits and provenance

File count includes config, selected pages/projects and parent artifacts, not only
DLLs. Parent artifact size has a separate configured limit from assembly size;
there is no `Int32`/2-GiB hashing ceiling. Total hashing is bounded independently.
Config/receipt JSON is capped at 1 MiB, selected files and parent manifest at
4 MiB. All hashing is streaming; the managed metadata header uses a bounded PE
reader. These facts are not an eight-times corpus performance acceptance claim.

IL and graph work limits are recorded for future execution, **not consumed or
enforced by a nonexistent scan/report phase**. No budget is automatically raised.
The input canonicalization is UTF-8 camel-case indented JSON of config SHA-256
and ordered input records (role then physical path). The manifest records its
exact CLI-assembly generator hash and actual bounded input hash. Run ID is not
part of the deterministic input digest. This artifact contains private paths,
commit identities and file hashes; it is local-only, never a shareable projection.

Source commit preflight checks current Git HEAD; it does not independently attest
cleanliness or freeze source bytes. Later source-snapshot/index and binding gates
must pass before execution. Input hashes are rechecked immediately before the
new run is published, and an existing output is never overwritten.

## Internal execution validation gate

The next native execution layer has a tested internal input gate. It is not a
new operator command and is not called by inventory-only `preflight` yet.

Managed inspection reuses the exact scanner readers, receipt classification,
dependency resolution and global gap policy through `ManagedMetadataExtractor.InspectInputs`.
Hash matches alone never become bindings. Receipt-based `bound` still does not
prove source ownership, authentic compilation, runtime dispatch or SQL execution.
Metadata work/text bounds are separate from IL work/text bounds.

Parent validation checks current Git/root identity against the retained manifest,
its snapshot identity against the complete embedded SQLite manifest, SQLite
integrity, and every streamed NDJSON fact against its indexed identity, rule,
tier, symbols, spans, extractor and properties. Duplicate/missing rows fail.
Facts and SQLite row projections are bounded; duplicate detection uses SQLite's
file-backed temporary store with a bounded cache instead of an unbounded managed
set. Active WAL/SHM/journal sidecars are rejected. A sidecar-free checkpointed WAL
index is opened immutably without creating files in its parent folder. Native
SQLite checks can be interrupted on cancellation.

All preflight input hashes are rechecked before and after validation. A retained
parent snapshot is **not** a validation of current source bytes; fresh scans must
establish their own actual source snapshot. Public gate tests cover receipt
identity/locator/ambiguity/staleness, parent tampering and count/row limits, escaped
filesystem names, cancellation and byte-for-byte parent immutability. They do not
establish private Windows or representative scale acceptance. Orchestration,
resume, fresh/attach execution and reporting integration remain outstanding.

## Next slices and acceptance

1. Authoritative source/parent/binding validation and resumable .NET phase execution.
2. Integration of grouped method chains into normal workbench navigation and an
   additive chain index in lossless JSON, without changing page verdicts.
3. Evidence-first Claude handoff updates and bounded retrieval over larger inputs.
4. Representative scale benchmarks, relocation and dry-run dependency cleanup.

Do not retire compatibility wrappers or the original working proof until public
parity tests and an authorized private Windows run validate the new flow.

Public tests: `WebFormsReviewPreflightTests`. The first slice validates real managed
headers, typed input failures, explicit gaps, parent immutability, deterministic
input hashing, cancellation, output separation, bounded JSON configuration,
and a native CLI invocation against a disposable public Git repository.
