# Microsoft Access Local Review Bundle

TraceMap can turn an existing Microsoft Access scan into one private local
review directory:

```bash
tracemap access-review create \
  --scan-output <access-scan-directory> \
  --out <new-local-review-directory>
```

The command is cross-platform and read-side only. It does not invoke Microsoft
Access, COM, a scanner, queries, forms, reports, VBA, or macros.

## Contents

```text
README.md
access-review-manifest.json
release-review/release-review.md
release-review/release-review.json
explorer/index.html
explorer/assets/
explorer/data/
```

The release-review files contain only the `access-evidence` scope. The explorer
uses the `hidden-local` safety profile and can be opened from disk. The
deterministic bundle manifest records the source commit, coverage/status,
counts, limitations, and SHA-256 hashes for the generated files.

The bundle remains local by default. It is not a public evidence pack.

## Evidence available

- database inventory and adapter capability evidence;
- tables, fields, declared types/sizes, required flags, and index membership;
- declared relationships;
- saved-query kind, parameter, and bounded dependency-shape evidence;
- external-boundary category evidence;
- form/report, VBA-module, and macro counts;
- explicit gaps for unavailable identity, source, body, or design coverage;
- upstream rule, tier, coverage, span, commit, extractor, support, and
  limitation provenance.

## Non-claims

The bundle does not prove:

- row contents, row counts, attachment/OLE contents, or query results;
- query, macro, VBA, form, or report execution;
- runtime reachability, provider behavior, linked-source availability, or
  effective permissions;
- production state, correctness, compatibility, operational safety, release
  approval, or DBA approval.

Machine-local absolute paths, raw SQL, query hashes, connection material,
credentials, private object display names, captions, expressions, VBA, macro
bodies, and infrastructure identities are not rendered. The repository-relative
database path remains the rule-bound evidence span.

## Parallels synthetic dogfood

Use the isolated Windows VM with Microsoft Access installed. Keep networking
and broad host sharing disabled. Stage only the two CLI publish directories and
the checked-in validation scripts through the established scoped read-only
share.

Run the existing synthetic smoke with a new durable review path outside its
disposable root:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\access-validation\Invoke-AccessSmoke.ps1 `
  -AccessCli <durable-tool-root>\tracemap-access.exe `
  -TraceMapCli <durable-tool-root>\tracemap.exe `
  -Generator <durable-tool-root>\New-SyntheticAccessFixture.ps1 `
  -SmokeRoot <new-disposable-smoke-root> `
  -Phase9CheckpointPath <durable-sanitized-checkpoint-base> `
  -ReviewBundlePath <new-durable-local-review-directory>
```

The harness validates the bundle contract and includes it in protected-marker
checks. When `-ReviewBundlePath` is omitted, the same validation runs inside
the disposable smoke root.

## Representative local review

Only the local operator may choose and authorize a representative database.
Keep its path and identity local:

```powershell
.\scripts\access-validation\Invoke-AccessRepresentativeSmoke.ps1 `
  -AccessCli <durable-tool-root>\tracemap-access.exe `
  -TraceMapCli <durable-tool-root>\tracemap.exe `
  -DatabasePath <explicitly-authorized-local-database> `
  -ScratchRoot <new-restricted-disposable-root> `
  -CheckpointBasePath <durable-sanitized-checkpoint-base> `
  -ReviewBundlePath <new-durable-local-review-directory> `
  -InputExplicitlyAuthorized
```

The representative harness opens only a private verified copy, observes the
existing non-execution and no-visible-surface canaries, gives the copied
database a generic repository-relative name, validates the retained local
bundle, and checks it for the original path/name markers. It does not authorize
upload or publication.

After review:

1. Close the local explorer.
2. Delete the retained bundle unless the owner still needs it.
3. Remove disposable scratch, staged tools, and sanitized checkpoints according
   to the existing validation procedure.
4. Confirm no Access or worker process remains and the original is unchanged.

## Reruns

Use a new output path for ordinary runs. `--force` replaces only an intact
TraceMap-generated bundle whose manifest, file inventory, sizes, and hashes
still match. It refuses modified or caller-owned directories.
