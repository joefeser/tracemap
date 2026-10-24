# Agent: build the bounded DNN SQL validation harness (#908)

Implement https://github.com/joefeser/tracemap/issues/908, a bounded first slice of
#870. Work locally on a Mac or personal Windows/Linux development host. Do not
access the private work machine, FFP checkout, private schema or prior acceptance
artifacts. This is public tooling development, not another private evidence prompt.

## Start and ownership

Read AGENTS.md, live #908/#870, docs/VALIDATION.md, the SQL database matrix and
current relevant implementation-state notes. Fetch origin/dev and use a clean
codex/ branch/worktree; preserve unrelated work. Inventory existing extraction,
schema/catalog and reporting contracts before adding new ones. Do not infer that
historical issue prose describes current capabilities. Keep a small implementation
state note with pinned inputs, decisions, validation and deferred work.

Proceed with independently answerable implementation work without serial questions
to the operator. At the end return one consolidated blocker list. Missing optional
live infrastructure must not block static tests or harness preparation.

## Public corpus and bounded implementation

1. Fetch public DNN Platform source from its canonical upstream. Pin an exact
   commit and record the associated release if any. Inspect license and notice
   obligations before copying any files. Prefer a documented opt-in fetch at a
   pinned revision plus independently authored tiny synthetic regression fixtures.
   No network fetch in the default unit-test suite.
2. Inspect at most three complete representative caller -> wrapper -> procedure
   definition -> consumer routes. Choose from actual source evidence; record the
   expected command type, parameters, result use and unresolved joins before
   evaluating TraceMap output. DNN owner/object qualifier substitutions must stay
   explicit; do not join procedure strings by name similarity or fabricate bindings.
   If one route is incomplete, record the precise limit and use another bounded
   candidate rather than launching a whole-CMS investigation.
3. Run existing TraceMap capabilities against the pinned public slice and compare
   observations with that independent expectation. Document supported, failed,
   unavailable and untested cells. Separate scanner shortcomings from missing build
   references, schema inputs, dynamic behavior and tooling/environment failures.
4. Implement focused synthetic C#/VB caller/wrapper + SQL fixtures and end-to-end
   extraction/report tests. Cover StoredProcedure versus Text, parameter type/size/
   direction mismatches, missing or renamed consumer columns, unknown schema/wrapper
   identity, conditional/dynamic result shapes, and distinct RETURN/OUTPUT/result
   sets. Attribute actual DNN coverage only to inspected routes; synthetic coverage
   does not prove DNN execution. Reuse current fixture/report formats where possible.
5. Fix demonstrated defects within these cases. Do not implement broad schema
   inference or an unrestricted SQL interpreter to finish the matrix. If a required
   schema-admission/comparison capability is absent, document its exact seam and
   bounded proposed contract; continue independent tests/harness work and report
   the scope decision needed before expanding production architecture.

Every finding must retain its rule, tier, location, revision, extractor and limits.
Keep source setup, DDL declarations, inferred results, observed execution, historical
binary identity and deployment correspondence separate. DDL alone may not resolve
dynamic SQL or conditional result shapes. No false clean results from missing input,
no removal of truncation, no source-name-only framework/provider identity.

## Prepare the optional integration lane; do not activate it yet

Add an opt-in harness/runbook for a disposable SQL Server 2022 Linux database using
the official Microsoft image. Resolve an explicit version to an immutable digest
when available; document how to verify RepoDigests and native Linux x86-64 support.
If a digest cannot be verified, fail closed rather than inventing one or running
mutable latest. Record tested version only after actual authorized execution.

The proposed `dockercache` host is not yet verified or approved for activation.
Do not SSH/provision/pull images/start containers/run SQL merely because the host
was mentioned. Prepare host-preflight commands and an approval checklist for host,
CPU/memory/disk bounds, nonproduction edition/EULA, credentials, network bindings,
owned disposable database/volume and cleanup. Do not put a placeholder into a
configuration that could silently deploy to the default Docker context.

Default invocation must be non-mutating or explicitly refuse to run without opt-in.
Tests use synthetic seed data and a narrowly selected schema subset, not automatic
full DNN installation. No public port binding, privileged container, host-wide prune,
existing-volume removal, source-mounted secret material or committed passwords.
Cleanup targets only resources created by this harness and must preserve unknown
resources. Keep health/readiness checks bounded; failures produce actionable logs
without credentials. Do not automatically launch IIS or the DNN application.

Separate metadata-description checks from execution tests. When eventually approved,
record server version/image digest/schema and seed hashes plus generator/input
provenance, and verify parameter/result behavior within that disposable database.
No deployed/private application acceptance follows from these synthetic results.

## Validation and handoff

Run required touched lanes and a synthetic CLI smoke. Shared-core changes require
the full Debug -warnaserror suite; obey final-head merge validation policy. Default
tests run without SQL Server, network access, IIS or private inputs. Test opt-in
guards/configuration without silently running containers. Label unexecuted live
tests NOT RUN, not passed or skipped-as-accepted.

New derived machine-readable artifacts need exact generator SHA-256 and bounded
input SHA-256; public projections must hash only their privacy-projected inputs.
Prefer native artifacts. Do not invent unproven provenance for a hand-written matrix.
Document license attribution, upstream pin, sample denominator, expected/observed
results, fixture limits, commands and a single consolidated next-step runbook.

Commit and push the implementation and follow-up prompt. Report branch/SHA, test
counts, actual DNN coverage versus synthetic coverage, demonstrated fixes, and all
remaining prerequisites together. Do not merge or activate the container environment.
If opening/reviewing a PR is subsequently requested, reread the current pr-review-loop
skill and verify the canonical /opt/homebrew/bin/agent-control installation and repo
doctor/preflight on macOS. Preserve lane policy; never request retired Qodo reviews.
