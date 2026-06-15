# Package Upgrade Impact Requirements

## Problem

TraceMap should summarize deterministic static evidence for package upgrades across existing package evidence. A dependency upgrade can affect many repositories, but v1 must not claim runtime compatibility, vulnerability status, loaded packages, or transitive dependency behavior.

## Scope Decisions

- MVP input is one TraceMap index, either a single-language `index.sqlite` or a combined index from `tracemap combine`.
- MVP package delta input is JSON with `version = "package-delta.v1"`, optional delta provenance (`sourceRepo`, `sourceCommitSha`), and a non-empty `changes` array.
- MVP supports static package upgrade evidence for NuGet, npm, Maven/Gradle, and pip ecosystems when adapters emit deterministic package facts for those ecosystems.
- MVP matches package declarations or lockfile package rows already emitted as `PackageReferenced` package surfaces. If adapters later emit deterministic import/usage or package-call facts with stable package identity, those facts SHALL be matched only after their rule IDs and limitations are documented.
- MVP supports exact case-insensitive package-name matching and optional exact ecosystem matching. When an ecosystem is specified in the delta, evidence without ecosystem metadata SHALL NOT match.
- MVP output is Markdown by default, JSON with `--format json`, and both files for directory output.
- MVP opens indexes read-only and does not mutate inputs.
- MVP does not inspect package registries, lockfile solvers, vulnerability databases, changelogs, licenses, runtime classpaths, deployed artifacts, or transitive dependency graphs.
- MVP does not call LLMs, use embeddings, query vector databases, or infer compatibility.

## Requirements

1. WHEN `tracemap package-impact --index <path> --package-delta <path> --out <path>` is run THEN TraceMap SHALL read the index and delta file and emit a deterministic package impact report.
2. WHEN the input index is a single-language TraceMap index THEN TraceMap SHALL read `scan_manifest` and `facts` and project package-config surfaces from existing facts.
3. WHEN the input index is a combined TraceMap index THEN TraceMap SHALL read `index_sources` and `combined_facts` and preserve source labels, scan IDs, commit SHAs, rule IDs, evidence tiers, and file spans.
4. WHEN a delta contains top-level or per-change `sourceRepo` or `sourceCommitSha` THEN TraceMap SHALL preserve provenance without rendering raw repository values; unsafe repository identifiers SHALL be hashed.
5. WHEN a delta change contains `packageName` and optional `ecosystem` THEN TraceMap SHALL match exact package names case-insensitively and ecosystems case-insensitively when supplied.
6. WHEN matching package evidence is found THEN each finding SHALL include the package change ID, package name, ecosystem, change type, old/new safe version values or hashes where available, source label, scan ID, commit SHA, file path, line span, evidence tier, extractor rule ID, and the package-impact rule ID.
7. WHEN no matching package evidence is found and coverage is reduced THEN TraceMap SHALL emit an analysis-gap row rather than implying absence.
8. WHEN no matching package evidence is found under full package evidence coverage THEN TraceMap MAY report `NoStaticPackageEvidence` but SHALL still include the scan manifest evidence and limitations.
9. WHEN package versions or metadata contain unsafe values THEN TraceMap SHALL not render raw unsafe values and SHALL prefer existing safe version hashes/redaction reasons.
10. WHEN output is a directory or extensionless path THEN TraceMap SHALL write `package-impact-report.md` and `package-impact-report.json`.
11. WHEN selectors `--source`, `--package`, or `--ecosystem` are supplied THEN TraceMap SHALL filter deterministically without changing the delta interpretation.

## Limitations

- Static package declaration evidence does not prove package restore, installed package versions, transitive dependency resolution, runtime loading, deployment, or API compatibility.
- Lockfile, import/usage, and package-call evidence is adapter-dependent and is only included when a deterministic adapter fact exposes stable package identity with documented rule limitations.
- Missing package evidence under reduced coverage is an analysis gap.
- Exact package-name matching can miss aliases, relocated packages, shaded dependencies, generated manifests, imported build files, and dynamically declared dependencies.
- Version comparisons are descriptive in v1; TraceMap does not interpret semantic versioning or compatibility.
