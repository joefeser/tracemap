# Microsoft Access File-First Scan

On Windows with Microsoft Access/DAO installed, TraceMap can scan one explicitly
selected local `.accdb` or `.mdb` without requiring the operator to create a Git
repository:

```powershell
tracemap-access scan-file `
  --database <authorized-local.accdb-or-mdb> `
  --out <new-scan-directory>
```

The selected database must be on a local filesystem. UNC paths and mapped
network drives are rejected before copying or launching Access. Snapshot Git
operations use the same bounded `--timeout-seconds` value as the scan and run
with inherited `GIT_*` routing and repository variables removed.

The output is the standard TraceMap artifact set:

```text
scan-manifest.json
facts.ndjson
index.sqlite
report.md
logs/analyzer.log
```

## Provenance and cleanup

TraceMap still preserves the repository/commit evidence invariant. Internally,
`scan-file`:

1. validates the extension, size, regular-file shape, reparse-point chain, and
   new output path;
2. hashes the original;
3. copies it to a restricted scratch directory under the generic name
   `database.accdb` or `database.mdb`;
4. verifies the copy hash;
5. creates a deterministic local Git commit in a no-remote disposable
   repository;
6. invokes the existing conservative Access scanner against that private
   snapshot only;
7. re-hashes the original and fails closed if its bytes changed;
8. deletes the snapshot repository and scratch directory on success,
   failure, or cancellation.

Evidence is labeled `provenanceKind=local-file-snapshot`. The local commit is
real deterministic provenance for the verified private copy; it is not
presented as an upstream source-control commit. The original filename, absolute
path, username, scratch path, and command text are not persisted.

The existing repository-oriented command remains available when a database is
already a clean tracked file:

```powershell
tracemap-access scan `
  --repo <git-worktree> `
  --database <repo-relative.accdb-or-mdb> `
  --out <new-scan-directory>
```

## Evidence improvements

Declared relationship evidence retains the raw DAO attribute mask and adds
normalized flags for unique/one-to-one, not-enforced, inherited, update
cascade, delete cascade, and left/right default joins. Unknown bits remain
explicit. These are static declarations; they do not prove enforcement,
permissions, data validity, or that an update/delete operation occurred.

Query dependency and parameter-limit gaps retain their owning query stable key
and supporting query declaration fact whenever the reader established that
owner. An owner is not invented when metadata failed before query identity was
available.

## Safety boundary

`scan-file` uses the shipped count-only form/report, VBA, and macro reader. It
does not add COM catalog reads, read rows, execute queries, render or invoke
forms/reports, acquire VBA source, enumerate macro identities/bodies, refresh
links, or contact a remote.

The file remains potentially hostile input. Continue to use the documented
isolated Windows/VM posture for representative databases.
