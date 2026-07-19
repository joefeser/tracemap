# Microsoft Access Adapter v0 Runway Requirements

## Introduction

TraceMap needs a narrow Microsoft Access evidence lane for teams that must
understand an existing `.accdb` or `.mdb` design without uploading the database,
reading business records, executing queries, or treating a generated narrative
as proof. The useful v0 outcome is a deterministic map of design-time database
objects, relationships, saved-query shape, forms/reports, bindings, and VBA
surfaces with explicit gaps where Access runtime behavior cannot be proven.

The adapter is a Windows-only local scanner because Access design metadata is
available through installed Microsoft Access/DAO COM automation. The resulting
artifacts must remain compatible with TraceMap's existing .NET readers and may
be reviewed on another machine after the local scan finishes.

This runway does not add LLM calls, embeddings, vector databases, prompt-based
classification, cloud upload, database execution, record inspection, or
runtime instrumentation. It preserves the TraceMap contract: no conclusion
without evidence, no evidence without a rule ID, no rule without documented
limitations, and no scan without repository and commit SHA.

## V0 Scope Decisions

- Support `.accdb` first and `.mdb` only when the installed Access/DAO provider
  can open it through the same bounded path. Unsupported or provider-incompatible
  files produce gaps; the adapter does not convert database formats.
- Run extraction locally on Windows with a supported installed Microsoft Access
  version. macOS and Linux callers receive a deterministic unsupported-platform
  diagnostic and no successful scan artifact set.
- Require every selected database to be inside the selected Git worktree at a
  concrete commit. A customer database that is not already in a repository must
  first be copied into a local, access-controlled evidence workspace and committed
  there. TraceMap does not silently invent non-repository provenance.
- Inspect a controlled temporary copy, not the selected original file. Startup
  macros and automation code must be disabled before opening the copy.
- Do not read table rows, row counts, attachment contents, OLE values, field
  defaults that require evaluation, calculated results, query resultsets, or
  linked data.
- Do not execute saved queries, action queries, pass-through queries, VBA,
  macros, form/report events, startup forms, or external links.
- Do not persist raw query SQL, connection strings, linked-table source strings,
  absolute paths, remote URLs, private server names, credentials, macro command
  bodies, form/report expressions, record-source text, control-source text, or
  VBA source.
- V0 evidence is static design metadata. It never proves runtime reachability,
  production database state, current record contents, successful query execution,
  permissions, deployment, release approval, or that a change is safe.

## Requirements

### Requirement 1: Windows-Local CLI and Git Provenance

**User Story:** As a cautious maintainer, I want a local Access scan tied to a
specific repository revision so that the evidence is reproducible and does not
leave my controlled environment.

#### Acceptance Criteria

1. WHEN the user runs the Access scan command with `--repo`, `--database`, and
   `--out` THEN the adapter SHALL require Windows, a supported installed Access
   COM server, a Git worktree, and a concrete commit SHA.
2. WHEN the selected database is outside the Git worktree, is not a regular
   file, or resolves through a symlink/reparse point outside the worktree THEN
   the adapter SHALL fail before opening Access or writing success artifacts.
3. WHEN Git metadata is unavailable THEN the adapter SHALL fail before writing
   a successful `scan-manifest.json`.
4. WHEN the database differs from the bytes at the selected commit because it is
   modified or untracked THEN the adapter SHALL fail by default and SHALL NOT
   attribute those bytes to the commit. A future explicit dirty-input mode is
   outside v0.
5. V0 SHALL attribute the scan to the worktree's checked-out `HEAD`. Scanning a
   historical commit SHALL require checking out that commit first; an arbitrary
   `--commit` selector is deferred.
6. WHEN Access or the required DAO provider is unavailable or incompatible THEN
   the adapter SHALL exit non-zero with a bounded diagnostic that contains no
   local absolute path or database secret.
7. WHEN the user requests `--help` or `--version` THEN the command SHALL work on
   every supported TraceMap host without initializing COM.
8. WHEN a scan succeeds THEN its manifest SHALL include repository identity,
   commit SHA, repository-relative database path, database SHA-256, adapter
   version, Access version, provider capability labels, and extraction coverage.

### Requirement 2: Non-Executing Input Safety

**User Story:** As a database owner, I want TraceMap to inspect design metadata
without allowing startup behavior or query execution to run.

#### Acceptance Criteria

1. BEFORE opening the database copy, the adapter SHALL force Office automation
   security to disable macros and SHALL configure Access so startup UI remains
   hidden.
2. WHEN preparing input THEN the adapter SHALL make a private controlled copy,
   verify the copy hash against the selected file hash, inspect only the copy,
   and delete the copy on success or failure where the operating system permits.
3. WHEN cleanup cannot remove the working copy or lock file THEN the adapter
   SHALL emit a bounded local diagnostic and SHALL NOT copy the path into public
   artifacts.
4. WHEN enumerating tables, relationships, queries, forms, reports, modules, or
   macros THEN the adapter SHALL use design collections and SHALL NOT open a
   recordset, request a row count, enumerate query fields for pass-through/action
   queries, refresh a linked table, or invoke an object.
5. WHEN an `AutoExec` macro, startup form, startup function, event procedure,
   action query, pass-through query, linked table, or data macro exists THEN the
   adapter SHALL inventory or gap it without executing it.
6. WHEN the database is password-protected, encrypted, corrupt, exclusively
   locked, or requires an interactive trust prompt THEN v0 SHALL fail or emit a
   reduced-coverage gap without accepting a password on the command line.
7. WHEN the input is malformed or hostile THEN the adapter SHALL bound object
   counts, string lengths, module sizes, extraction duration, and diagnostic
   volume; v0 SHALL document that operating-system/VM isolation remains an
   operator control rather than a claim proved by TraceMap.
8. WHEN the COM worker exceeds the configured total timeout or stops responding
   THEN the supervising process SHALL terminate only the worker and the Access
   process instance recorded for that worker, clean its controlled copy where
   possible, and fail without a successful artifact set.

### Requirement 3: Standard Artifact Contract

**User Story:** As a TraceMap user, I want Access evidence to work with the same
review tools as other adapters.

#### Acceptance Criteria

1. WHEN an Access scan succeeds THEN it SHALL write `scan-manifest.json`,
   `facts.ndjson`, `index.sqlite`, `report.md`, and `logs/analyzer.log`.
2. WHEN evidence is emitted THEN every fact SHALL contain a deterministic fact
   ID, scan ID, repository and commit SHA, fact type, rule ID, evidence tier,
   repository-relative file path, valid line span, extractor ID/version, and
   sorted safe properties.
3. WHEN an Access logical object has no source-file line model THEN its evidence
   span SHALL use the owning `.accdb`/`.mdb` repository-relative path with
   `startLine=1` and `endLine=1`; the logical object identity SHALL live in safe
   properties and SHALL NOT be represented as a fabricated source line.
4. WHEN VBA module text supplies real line coordinates THEN VBA declaration,
   call, and event facts SHALL use those module line numbers while retaining the
   owning database file as the evidence path and a safe module identity property.
5. WHEN coverage is incomplete THEN the manifest SHALL use reduced coverage,
   `FailedOrPartial`, and rule-backed `AnalysisGap` facts. It SHALL NOT describe
   missing evidence as a clean absence.
6. WHEN artifacts are written twice for the same repository bytes, commit,
   database selection, adapter version, and normalized options THEN scan IDs,
   fact IDs, NDJSON, SQLite evidence rows, report evidence sections, and logs
   SHALL be stable apart from documented timestamps.

### Requirement 4: Database Schema and Relationship Evidence

**User Story:** As a maintainer, I want to understand the Access schema design
without exposing its records.

#### Acceptance Criteria

1. WHEN local, non-system tables are present THEN the adapter SHALL emit safe
   structural evidence for table identity, field identity, declared Access data
   type, declared size/precision where statically available, nullability/required
   flags, and primary or unique index membership.
2. WHEN relationships are declared THEN the adapter SHALL emit source table,
   source field, target table, target field, relationship attributes, and stable
   supporting IDs where the endpoints are unambiguous.
3. WHEN composite keys or relationships exist THEN field ordering SHALL be
   preserved deterministically.
4. WHEN system, temporary, hidden, or Access-internal objects are encountered
   THEN the adapter SHALL omit them by default and record summary counts without
   exposing unsafe names.
5. WHEN a table or field identifier fails the shared safe-value policy or looks
   secret-bearing THEN the adapter SHALL retain a role-separated SHA-256 identity
   and SHALL omit the raw identifier.
6. WHEN table design metadata is unavailable or ambiguous THEN the adapter SHALL
   emit a schema coverage gap and SHALL NOT infer the missing design from query
   text or row values.
7. Schema facts SHALL be `Tier2Structural`; the adapter SHALL NOT describe Access
   catalog metadata as compiler-resolved semantic evidence.

### Requirement 5: Saved Query Evidence and Secret Safety

**User Story:** As a reviewer, I want saved-query dependencies and operation
shapes without leaking SQL or causing external execution.

#### Acceptance Criteria

1. WHEN a saved query definition is available THEN the adapter SHALL emit query
   kind, parameter metadata, query-text SHA-256, query-text length, and safe
   referenced local object identities derived in memory.
2. WHEN query SQL is read for hashing/projection THEN the raw SQL SHALL remain
   in process memory only and SHALL NOT be written to any TraceMap artifact or
   diagnostic.
3. WHEN a query is pass-through, action, DDL, crosstab, union, data-definition,
   dynamic, malformed, or otherwise unsafe to inspect deeply THEN the adapter
   SHALL avoid result-field enumeration and SHALL emit conservative shape or gap
   evidence.
4. WHEN a pass-through connection property or ODBC connect string exists THEN
   the adapter SHALL store at most a role-separated full SHA-256 and safe provider
   family classification; it SHALL NOT store or render the raw value, server,
   database, user, credential, DSN, URL, or path.
5. WHEN query references cannot be parsed deterministically from supported Access
   SQL shapes THEN the adapter SHALL emit an explicit query dependency gap and
   SHALL NOT guess targets.
6. Query facts SHALL reuse shared `QueryPatternDetected`/`SqlTextUsed` evidence
   only where their existing contracts fit; Access-specific declarations and
   dependency edges MAY use new fact types with cataloged limitations.
7. Query evidence SHALL NOT claim execution success, runtime parameter values,
   branch selection, linked-source availability, provider behavior, permissions,
   result shape, or production use.

### Requirement 6: Forms, Reports, Controls, and Bindings

**User Story:** As an Access developer, I want to see how forms and reports are
bound to database objects and events.

#### Acceptance Criteria

1. WHEN forms and reports are present THEN the adapter SHALL emit structural
   surface facts with safe object identity, module presence, bound/unbound state,
   control counts by safe type, and design hash.
2. WHEN a record source or control source is a direct safe object/field identity
   THEN the adapter SHALL emit a binding edge to that identity.
3. WHEN a record source, row source, control source, filter, ordering expression,
   validation expression, or calculated-control expression is complex THEN the
   adapter SHALL store only kind, length, hash, and safe referenced identifiers
   where deterministically parsed.
4. WHEN event properties are present THEN the adapter SHALL classify them as
   embedded macro, event procedure, expression, none, dynamic, or unknown without
   storing command bodies or raw expressions.
5. WHEN forms/reports or controls cannot be opened safely in design metadata mode
   THEN the adapter SHALL emit a surface coverage gap and continue with other
   evidence where possible.
6. Surface and binding evidence SHALL NOT prove runtime rendering, navigation,
   visibility, enabled state, user access, submitted values, event execution,
   filter outcome, printed output, or production use.

### Requirement 7: VBA Inventory and Static Call Evidence

**User Story:** As an Access maintainer, I want a bounded map of VBA modules and
obvious calls without storing source code.

#### Acceptance Criteria

1. WHEN VBA code modules are readable THEN the adapter SHALL compute module
   hashes, line counts, declaration spans, procedure kinds, safe procedure names,
   and bounded static call candidates in memory.
2. WHEN VBA source is inspected THEN no source text, full declaration text,
   comments, string literal values, connection strings, SQL, command bodies, or
   local paths SHALL be persisted by default.
3. WHEN direct, statically named calls such as local procedure calls,
   `DoCmd.OpenForm`, `DoCmd.OpenReport`, `DoCmd.OpenQuery`, DAO query/table
   references, domain functions, or `OpenRecordset` are visible THEN the adapter
   MAY emit Tier3 call/navigation/data-surface candidates using safe literal
   targets or role-separated hashes.
4. WHEN calls use variables, concatenation, `Eval`, `Run`, reflection-like
   dispatch, callbacks, events, COM add-ins, external libraries, or conditional
   construction THEN the adapter SHALL emit a VBA dynamic-boundary gap rather
   than inventing a target.
5. WHEN form/report event properties name `[Event Procedure]` THEN the adapter
   MAY map them to same-module procedures only by exact deterministic Access
   naming rules; ambiguity or missing procedures SHALL emit gaps.
6. VBA evidence SHALL NOT claim runtime dispatch, branch feasibility, COM target
   resolution, user interaction, error-handler outcome, transaction behavior,
   query execution, record access, external process execution, or production use.

### Requirement 8: Macros, External Links, and Unsupported Features

**User Story:** As a reviewer, I want dangerous and unsupported Access features
made visible without their sensitive contents being exported.

#### Acceptance Criteria

1. WHEN UI macros, named macros, data macros, AutoExec, startup forms/functions,
   add-ins, or embedded macro event handlers exist THEN v0 SHALL emit inventory
   or gap evidence without exporting macro command bodies or arguments.
2. WHEN linked tables, linked SharePoint lists, ODBC sources, external Access
   files, text/Excel links, or pass-through queries exist THEN the adapter SHALL
   emit a safe external-boundary fact with source kind and hashed external identity.
3. WHEN a raw external value contains credentials, a connection string, private
   host, URL, UNC path, drive path, DSN, or local path THEN only role-separated
   hashes and safe classifications may persist.
4. WHEN Access features are unsupported, unavailable through the installed COM
   version, disabled by trust settings, or require execution THEN the adapter
   SHALL emit rule-backed gaps and continue when useful evidence remains.
5. Macro and external-link evidence SHALL NOT prove live connectivity, linked
   schema freshness, authentication, authorization, reachable files/servers,
   runtime command execution, or production state.

### Requirement 9: Identity, Ordering, and Cross-Object Composition

**User Story:** As a tool maintainer, I want stable Access object identities so
evidence can be compared and composed safely.

#### Acceptance Criteria

1. Stable Access object keys SHALL include a versioned discriminator, source
   label/database hash identity, object kind, safe normalized name or full
   role-separated name hash, and an occurrence discriminator only when required.
2. Stable keys SHALL NOT include local absolute paths, timestamps, process IDs,
   temporary-copy paths, output paths, raw SQL, connection strings, or raw VBA.
3. WHEN table, query, form, report, control, macro, and VBA objects reference one
   another unambiguously THEN the adapter SHALL emit explicit relationship rows
   or facts with sorted supporting fact IDs.
4. WHEN multiple candidates match THEN the adapter SHALL emit ambiguity gaps and
   SHALL NOT choose an arbitrary target.
5. Collections and properties SHALL be sorted by stable ordinal keys before IDs
   and output are generated, regardless of COM enumeration order.
6. Identifier normalization SHALL be case-aware for Access matching while
   preserving a safe display form where allowed; case-only collisions SHALL not
   be silently merged.

### Requirement 10: Coverage, Reporting, and Downstream Compatibility

**User Story:** As a reviewer, I want an honest Access report that can enter the
existing TraceMap evidence workflow.

#### Acceptance Criteria

1. V0 Access scans SHALL use `Level1SemanticAnalysisReduced` with
   `FailedOrPartial` when useful structural evidence is emitted, because COM
   catalog evidence and bounded VBA/query parsing do not prove full semantics.
   This reuses the existing reduced-coverage vocabulary; it SHALL NOT upgrade
   any Access fact above its independently assigned Tier2/Tier3 evidence tier.
2. WHEN no supported Access database can be opened or no useful design evidence
   can be emitted THEN the command SHALL fail rather than write a clean scan.
3. The report SHALL summarize database-object counts, evidence rules, evidence
   tiers, coverage, gaps, limitations, and omitted sensitive categories without
   exposing raw protected material.
4. Existing `export`, `combine`, `report`, evidence-docs, vault, and release-review
   readers SHALL either preserve Access facts safely or declare an explicit
   unsupported-consumer gap; they SHALL NOT silently drop Access provenance.
5. Public/demo export paths SHALL default Access object evidence to hidden until
   a separate public-safe fixture and claim review promote it.
6. Reports SHALL repeat the non-claims: no row inspection, no SQL/query/macro/VBA
   execution, no runtime reachability, no linked-source connectivity, no
   production state, no release approval, and no “safe to change” conclusion.

### Requirement 11: Validation Fixtures and Security Regression Tests

**User Story:** As a security-conscious maintainer, I want repeatable fixtures
that prove both useful coverage and non-execution.

#### Acceptance Criteria

1. The implementation SHALL include a generator for a synthetic zero-row Access
   fixture rather than committing a customer database or private export.
2. The fixture SHALL cover local tables, composite and simple relationships,
   select/parameter/action/pass-through queries, linked-source metadata, forms,
   reports, controls, event procedures, VBA, AutoExec/startup behavior, and
   unsupported macro coverage where the installed Access version permits.
3. A hostile-canary fixture SHALL make any accidental startup macro, VBA, query,
   linked-source, or row access observable without contacting a real network or
   external system; tests SHALL fail if the canary fires.
4. Tests SHALL plant fake credentials, connection strings, private hostnames,
   absolute Windows paths, raw SQL markers, VBA literal markers, and macro
   argument markers and assert they do not appear in artifacts or logs.
5. Tests SHALL prove deterministic IDs and byte-stable evidence outputs apart
   from documented timestamps across repeated scans and varied output paths.
6. Cross-platform CI SHALL build and test non-COM logic on existing runners;
   Windows Access integration tests MAY run only on an explicitly provisioned,
   local/self-hosted runner and SHALL otherwise be reported as a documented
   validation gap, not silently marked passed.
7. Validation SHALL include full .NET build/test, private-path checking, diff
   checking, and relevant downstream artifact-contract smokes.
8. Tests SHALL cover an unsupported/provider-incompatible `.mdb` path, COM
   worker timeout, aggregate fact/output limits, case-only identifier collisions,
   concurrent scans of the same source, and distinct per-scan working copies.

## V0 Non-Claims

Even a successful Access scan does not prove:

- that any record exists or has a particular value;
- that any query, macro, VBA procedure, event, form, or report executes;
- runtime control flow, navigation, branch feasibility, or error behavior;
- external/linked source availability, schema freshness, credentials, or access;
- database integrity, repairability, encryption strength, or malware safety;
- effective permissions, user roles, deployment, current production use, or
  release/change approval;
- that a proposed change is complete, harmless, or safe to make.
