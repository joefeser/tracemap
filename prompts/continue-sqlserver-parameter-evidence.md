# Continue bounded SQL Server parameter evidence

Read AGENTS.md, docs/VALIDATION.md, docs/validation/SQLSERVER_PARAMETER_EVIDENCE.md,
.kiro/specs/sqlserver-parameter-evidence/ (especially implementation-state.md), live
issues #908/#870 and the current branch/head. Refresh evidence; do not assume this
slice completes either issue, real DNN selected reader coverage or private SQL-A.

Review the supported independent synthetic declaration origin, exact logical
server/database/schema/procedure binding and projected source-evidence digest.
Use native `sqlserver-schema produce` and `compare` plus the offline public smoke
on a new output directory. Verify its exact 52 selected cases (8 bounded matches,
12 mismatches, 32 unknowns), per-report 25 unbound unknowns, generator/public-input
hashes, current revision and deterministic repeats. Preserve the unchanged DNN
12-site-per-language inventory, SQLite/paths/truncation tests and Python harness
guards, including #909's inventory/scalar-version/deterministic-receipt fixes.

Any PR work must first read the current pr-review-loop skill and verify canonical
ACK executable/version/provenance and normal doctor/preflight. Preserve current
lane policy/review ceilings/human merge gate; no Qodo or new paid review spend.
Do not merge. No paid or live activation authority is created by this handoff.

Future scope choices, not implementation blockers or work-machine questionnaires:
1. Whether to add a separate bounded SQL DDL parser origin or catalog-description
   origin, with independent provenance/admission and unchanged runtime boundaries.
2. Whether to support additional parameter mutation/alias/control-flow forms,
   optional-default semantics, ANSI provider encoding, precision/scale or collation.
3. Whether to build result-column/consumer comparison with its own admitted schema
   identity and static/dynamic/result-set limitations. No result-set inference now.
4. Whether to activate the existing disposable SQL Server harness. Only after the
   separately documented host/resources/EULA/credentials/isolation/disposable-cleanup
   approvals; no SSH, Docker calls, pulls, provisioning or SQL merely to review this.

Exhaust authorized local synthetic tests first. Return any genuinely new authority
blockers together once. No private employer or work-machine artifacts, credentials,
provider execution, IIS or deployment. Synthetic evidence does not establish actual
connection identity, installed schema, framework dispatch, deployment or runtime.
