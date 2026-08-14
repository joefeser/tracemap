# Guided Local Review Workflow Requirements

## Introduction

TraceMap's .NET CLI already produces deterministic scan artifacts, a bounded
`scan-execution-receipt.v1`, Web Forms modernization packets, and a local
static explorer. Today, operators generally need a source checkout and must
manually connect those commands and artifacts.

This specification defines issue #666: a versioned local distribution and a
guided command surface that makes the safe path discoverable without changing
scanner, reducer, packet, or explorer evidence semantics.

The workflow is local-only. It does not upload source or artifacts, restore
dependencies, access the network, mutate source, execute application behavior,
or call an AI service unless a future separately authorized contract says so.

## Scope

In scope:

- evaluate .NET tool, framework-dependent archive, self-contained archive, and
  offline/container bundle candidates using reproducible packaging fixtures;
- select only a candidate that satisfies the validation gates in this spec;
- install, verify, upgrade, and uninstall documentation for Windows, macOS,
  and Linux;
- a version/readiness command that does not require a source checkout;
- a guided local workflow over existing immutable stages;
- deterministic machine-readable workflow results and concise terminal
  summaries;
- output-collision refusal, input-hash continuity, typed failure readback, and
  safe next actions;
- deterministic tests, package smoke tests, documentation, and validation.

Out of scope:

- new evidence extraction, reducer classifications, or explorer inference;
- automatic `dotnet restore`, package restore, source edits, git mutation,
  network access, uploads, telemetry, or hosted execution;
- LLM calls, embeddings, vector databases, or prompt classification;
- runtime reachability, application correctness, migration completeness,
  release approval, or architecture recommendations;
- silently placing absolute paths or protected values in portable artifacts.

## Requirements

### Requirement 1: Evidence-Based Distribution Decision

**User Story:** As a maintainer, I want TraceMap's distribution mechanism
selected from reproducible evidence rather than convenience assumptions.

#### Acceptance Criteria

1. The implementation SHALL evaluate at least .NET tool,
   framework-dependent archive, self-contained archive, and offline/container
   bundle candidates against one documented matrix.
2. The matrix SHALL cover supported host OS and architecture, required runtime,
   artifact size, offline install behavior, command naming, upgrade/uninstall,
   deterministic contents, signing/authenticity boundary, native dependency
   behavior, and CI reproducibility.
3. A candidate SHALL NOT be selected until synthetic package/install/run/remove
   smoke tests demonstrate the claimed host behavior.
4. The selected mechanism SHALL be versioned and SHALL run without a TraceMap
   source checkout.
5. If no candidate satisfies the gates, the implementation SHALL publish the
   evaluation and keep source-checkout runbooks authoritative rather than
   claiming packaging readiness.
6. Package hashes prove bytes and integrity only; they SHALL NOT be described
   as signer identity, publisher authority, or trust unless a separate signing
   mechanism proves those properties.

### Requirement 2: Version And Readiness Verification

**User Story:** As an operator, I want to verify exactly which TraceMap build I
am about to run and whether its required local dependencies are available.

#### Acceptance Criteria

1. The installed CLI SHALL expose a command such as `tracemap version --json`
   with a stable versioned schema.
2. The result SHALL include tool version, distribution kind, target/runtime
   compatibility, schema version, and a closed readiness outcome.
3. Readiness checks SHALL be bounded and local. They MAY observe the current OS,
   architecture, .NET runtime, Git availability, and MSBuild availability, but
   SHALL NOT probe a repository, network, credentials, or private environment
   values.
4. Human output SHALL remain concise and SHALL provide a typed next action when
   readiness is reduced or unavailable.
5. Portable JSON SHALL omit absolute executable paths, usernames, home
   directories, environment values, package feeds, and raw diagnostic text.
6. Repeated verification of the same installed build and local capability state
   SHALL be byte-stable except for fields explicitly documented as local
   observations; timestamps SHALL NOT enter deterministic identity.

### Requirement 3: Guided Workflow Contract

**User Story:** As an operator, I want one safe command to run a scan and
discover compatible review outputs without memorizing internal commands.

#### Acceptance Criteria

1. The CLI SHALL expose one explicit guided command with a versioned result
   schema. The final command name SHALL be selected during design validation
   and protected by CLI tests.
2. The first supported workflow SHALL accept an explicit repository and a new
   output root and SHALL run the existing `scan` stage without changing scan
   options or evidence semantics.
3. Optional downstream stages SHALL invoke existing public APIs or command
   handlers for Web Forms modernization and explorer generation; they SHALL NOT
   reimplement extraction or parse private scanner internals.
4. A downstream stage SHALL run only when its required input artifact exists,
   passes its existing compatibility contract, and matches the authoritative
   repository, commit, scan, and source-snapshot identity where those fields are
   available.
5. Unsupported or unavailable downstream stages SHALL be recorded as skipped,
   unavailable, or partial with a typed reason. They SHALL NOT turn absence into
   successful evidence.
6. The workflow SHALL preserve advanced scanner flags through an explicit,
   documented allowlist. Unknown options SHALL fail before scanning.
7. The workflow SHALL not enable restore, network access, uploads, source
   mutation, or hidden telemetry by default.

### Requirement 4: Immutable Stage Inputs And Hash Continuity

**User Story:** As a reviewer, I want proof that each guided stage consumed the
artifact produced by the preceding stage.

#### Acceptance Criteria

1. Before each downstream stage, the workflow SHALL compute SHA-256 hashes for
   its required input artifacts and record relative artifact names and hashes in
   the workflow result.
2. After the stage, the workflow SHALL verify the required upstream artifacts
   are unchanged. A mismatch SHALL stop later stages and emit a typed
   input-mutation failure.
3. Repository, commit SHA, scan ID, source snapshot digest, schema version, and
   claim level SHALL be preserved or explicitly marked unavailable; conflicting
   values SHALL fail closed.
4. Stage receipt IDs and workflow IDs SHALL exclude duration, timestamps, and
   absolute paths.
5. The workflow SHALL compose `scan-receipt.json`; it SHALL NOT translate its
   operational observations into `CodeFact` evidence.
6. Hashes SHALL not be described as proof that an artifact was truthful,
   approved, or produced by a trusted publisher.

### Requirement 5: Output Safety And Collision Refusal

**User Story:** As an operator, I want a guided run to preserve existing files
and leave an understandable safe state after failure.

#### Acceptance Criteria

1. The output root SHALL be absent or an empty directory unless a documented
   `--force` mode proves every replaced file is TraceMap-generated.
2. The workflow SHALL stage outputs under a sibling temporary directory and
   publish them atomically where the host filesystem permits.
3. It SHALL never overwrite user-authored files, treat an unresolved output
   symlink or reparse point as authoritative, delete outside the canonically
   resolved and authorized output root, or place output
   inside the scanned repository unless the operator explicitly selects a safe
   ignored location and the collision guard accepts it.
4. Portable artifacts SHALL contain only relative paths. Interactive terminal
   output MAY show absolute local output locations, but those values SHALL not
   enter deterministic IDs, portable JSON, Markdown, logs, or explorer data.
5. Failure cleanup SHALL report one of a closed set such as `completed`,
   `not-required`, `failed`, or `unknown` and SHALL preserve the highest proven
   safe stage.
6. A failed run SHALL not fabricate normal scan, packet, or explorer outputs.

### Requirement 6: Machine Result And Human Readback

**User Story:** As an operator or automation author, I want both a stable JSON
result and a concise terminal summary derived from the same structured state.

#### Acceptance Criteria

1. The workflow SHALL emit a schema-versioned JSON result containing workflow
   ID, tool version, repository identity hash, commit SHA, source snapshot
   digest, outcome, coverage, stage records, artifacts, counts, gaps,
   limitations, last safe state, cleanup result, retryability, and next action.
2. Stage outcome, retryability, next action, artifact kind, and coverage values
   SHALL come from documented closed vocabularies.
3. Each artifact record SHALL include a safe relative path, schema/kind,
   SHA-256, producer stage, and availability status.
4. Evidence and gap summaries SHALL be bounded counts and safe identifiers;
   raw source values, snippets, SQL, URLs, remotes, connection material,
   exception messages, and analyzer log text SHALL be omitted.
5. Human output SHALL be rendered from the structured result and SHALL show the
   analyzed commit/snapshot, coverage/build status, artifact locations, bounded
   evidence/gap counts, and next valid commands.
6. Partial analysis SHALL be plainly labeled partial. Failed build SHALL not be
   summarized as a clean repository.

### Requirement 7: Typed Failure And Recovery

**User Story:** As an operator at 2:17 AM, I want the failure point, safe state,
and next permitted action without needing repository tribal knowledge.

#### Acceptance Criteria

1. Expected failures SHALL use categorical public error codes rather than raw
   exception messages.
2. The result SHALL distinguish invalid arguments, identity unavailable,
   output collision, scan failure, partial scan, incompatible downstream input,
   input mutation, packet failure, explorer failure, cleanup failure, and
   unsupported host/distribution.
3. Every failure record SHALL state the last proven safe state and one bounded
   next action such as correct input, choose new output, inspect scan receipt,
   review gaps, retry unchanged, or contact owner.
4. A retry recommendation SHALL not claim that retrying will succeed.
5. Cancellation and timeout SHALL be distinct from product failure and SHALL
   preserve any authoritative scan receipt already written.
6. The command SHALL return deterministic documented exit-code classes.

### Requirement 8: Cross-Platform Installation Documentation

**User Story:** As a local user, I want install, upgrade, verification, and
uninstall instructions for my supported platform.

#### Acceptance Criteria

1. Documentation SHALL cover Windows PowerShell, Windows Command Prompt where
   materially different, macOS, and Linux for the selected distribution.
2. Instructions SHALL use published package/archive placeholders or a local
   offline package path; they SHALL not require cloning this repository.
3. Documentation SHALL identify required runtime/tool prerequisites, supported
   architectures, PATH behavior, upgrade semantics, uninstall semantics, and
   offline limitations.
4. Documentation SHALL include `version`, one minimal guided run, output
   interpretation, and recovery from the most common typed failures.
5. Instructions SHALL not tell users to bypass signing, quarantine, execution
   policy, or operating-system security controls.

### Requirement 9: Compatibility And Determinism

**User Story:** As an existing TraceMap user, I want packaging and orchestration
to preserve current commands and artifacts.

#### Acceptance Criteria

1. Existing CLI commands and their schemas SHALL remain compatible unless a
   separately versioned breaking change is approved.
2. The guided workflow SHALL call the same scanner, packet reporter, and
   explorer generator implementation used by their standalone commands.
3. Identical immutable stage artifacts, options, selected stages, and tool
   version SHALL produce byte-identical portable workflow projections and
   downstream artifacts. A fresh scan may retain its existing observational
   `scannedAt` value; the workflow SHALL NOT rewrite that producer-owned field
   or claim two independently executed scans are byte-identical.
4. Stage ordering and artifact ordering SHALL be deterministic.
5. A standalone command and the corresponding guided stage SHALL produce
   equivalent deterministic artifacts.
6. Any downstream feature not yet compatible, including #667 before it ships,
   SHALL remain an explicit deferred/unavailable stage rather than a hidden
   dependency.

### Requirement 10: Validation And Non-Claims

**User Story:** As a maintainer, I want packaging and guided execution proven
without overstating what a local scan establishes.

#### Acceptance Criteria

1. Tests SHALL cover candidate package creation, install/run/uninstall,
   source-checkout independence, deterministic version output, workflow stage
   ordering, collisions, symlink/reparse safety, input mutation, partial scans,
   typed failures, and safe rendering.
2. At least one synthetic Web Forms repository SHALL prove standalone-versus-
   guided scan and packet parity.
3. Supported host claims SHALL be backed by CI or owner-controlled smoke
   evidence for that host; untested hosts SHALL be labeled unverified.
4. Validation SHALL include full .NET build/test, private-path guard, diff
   check, package-content inspection, and platform-specific smoke tests.
5. The feature SHALL document that it does not prove runtime execution,
   workflow completion, application correctness, complete coverage, migration
   safety, release approval, publisher identity, or production state.
6. No core scanner/reducer LLM, embedding, vector database, or prompt
   classification capability SHALL be added.
