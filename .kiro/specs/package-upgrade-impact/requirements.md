# Package Upgrade Impact Requirements

## Problem

TraceMap should summarize deterministic static evidence for package upgrades across existing package evidence. A dependency upgrade can affect many repositories, but v1 must not claim runtime compatibility, vulnerability status, loaded packages, or transitive dependency behavior.

## Scope Decisions

- MVP input is one TraceMap index, either a single-language `index.sqlite` or a combined index from `tracemap combine`.
- MVP package delta input is JSON with `version = "package-delta.v1"` and a non-empty `changes` array.
- MVP matches package declarations already emitted as `PackageReferenced`/`package-config` surfaces.
- MVP supports exact case-insensitive package-name matching and optional exact ecosystem matching.
- MVP output is Markdown by default, JSON with `--format json`, and both files for directory output.
- MVP opens indexes read-only and does not mutate inputs.
- MVP does not inspect package registries, lockfile solvers, vulnerability databases, changelogs, licenses, runtime classpaths, deployed artifacts, or transitive dependency graphs.
- MVP does not call LLMs, use embeddings, query vector databases, or infer compatibility.

## Requirements

1. WHEN `tracemap package-impact --index <path> --package-delta <path> --out <path>` is run THEN TraceMap SHALL read the index and delta file and emit a deterministic package impact report.
2. WHEN the input index is a single-language TraceMap index THEN TraceMap SHALL read `scan_manifest` and `facts` and project package-config surfaces from existing facts.
3. WHEN the input index is a combined TraceMap index THEN TraceMap SHALL read `index_sources` and `combined_facts` and preserve source labels, scan IDs, commit SHAs, rule IDs, evidence tiers, and file spans.
4. WHEN a delta change contains `packageName` and optional `ecosystem` THEN TraceMap SHALL match exact package names case-insensitively and ecosystems case-insensitively when supplied.
5. WHEN matching package evidence is found THEN each finding SHALL include the package change ID, package name, ecosystem, change type, old/new safe version values or hashes where available, source label, scan ID, commit SHA, file path, line span, evidence tier, extractor rule ID, and the package-impact rule ID.
6. WHEN no matching package evidence is found and coverage is reduced THEN TraceMap SHALL emit an analysis-gap row rather than implying absence.
7. WHEN no matching package evidence is found under full package evidence coverage THEN TraceMap MAY report `NoStaticPackageEvidence` but SHALL still include the scan manifest evidence and limitations.
8. WHEN package versions or metadata contain unsafe values THEN TraceMap SHALL not render raw unsafe values and SHALL prefer existing safe version hashes/redaction reasons.
9. WHEN output is a directory or extensionless path THEN TraceMap SHALL write `package-impact-report.md` and `package-impact-report.json`.
10. WHEN selectors `--source`, `--package`, or `--ecosystem` are supplied THEN TraceMap SHALL filter deterministically without changing the delta interpretation.

## Limitations

- Static package declaration evidence does not prove package restore, installed package versions, transitive dependency resolution, runtime loading, deployment, or API compatibility.
- Missing package evidence under reduced coverage is an analysis gap.
- Exact package-name matching can miss aliases, relocated packages, shaded dependencies, generated manifests, imported build files, and dynamically declared dependencies.
- Version comparisons are descriptive in v1; TraceMap does not interpret semantic versioning or compatibility.
