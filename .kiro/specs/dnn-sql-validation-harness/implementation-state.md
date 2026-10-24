# Bounded DNN SQL harness #908

Branch: `codex/dnn-sql-validation-908`
Base: freshly fetched `origin/dev` `b4494b01ef7b8b06a5b1ed10b787033469fdd0b9`.
Status: static validation passed; commit/push pending. No PR, merge, container activation or SQL execution.

## Inputs and decisions

Live #908 and parent #870 read on 2026-10-24. DNN canonical upstream pinned to
v9.13.8 / `33b8fbe440ab665b8423599789db1f34d2a500fc`; root MIT LICENSE and
NOTICE inspected. No upstream implementation file/binary is vendored; the static
verifier attributes its short source markers with the full MIT notice. Opt-in fetch retains
12 public files plus exact source revision and generated setup provenance.

One logical route inspected: DNNRoleProvider.GetRoleGroups, cache callback,
DataProvider/SqlDataProvider/SqlDatabaseConnectionProvider/PetaPocoHelper/DataUtil,
consolidated DDL and RoleGroupInfo/BaseEntityInfo/CBO result consumption. Earlier
GetRoleGroup candidate is a cached collection filter; no same-name procedure
binding inferred. Owner/object qualifiers, external provider identity, hydration
and runtime dispatch stay unresolved. Full upstream installer/build never ran.

Current native public slice: 6,031 facts, Level3SyntaxAnalysis, build NotRun;
three incidental Tier3 nonquery candidates, zero proved end-to-end selected
reader paths. 539 rule-backed gaps remain. Original .SqlDataProvider is not
SQL-file inventory. See docs/validation/DNN_SQL_VALIDATION.md for exact locations,
expectation/observation cells and limitations.

Synthetic source uses ten reader controls, Channels, and scalar wrapper terminal
per language (12 execution sites each), plus wrapper caller/unknown-dispatch
controls. StoredProcedure/Text and local enum witnesses are asserted; exact
sizes, consumer-column comparison and SQL Server catalog admission are absent
production seams, documented rather than fabricated. VB constructor-only text
was unavailable in the local walker, so the explicit CommandText assignment
exercises the supported witness contract. No production extractor/report/schema
change or demonstrated production defect fix is claimed.

MCR manifest/config metadata were fetched and hash-verified, no layers pulled:
2022-CU20-ubuntu-22.04, Linux amd64,
`sha256:7c29dfbac885ad7519e219c7fe4aee0e67283e21a10e9c252d13b0fbde1866f8`.
The generated public metadata receipt records resolver/input hashes; tested
server version is null. Harness has no default context or container activation,
requires every explicit approval flag and exact endpoint, uses network none,
no ports/source mounts/persistent volume, bounded resources and owned tmpfs.
Mocked lifecycle tests do not establish live compatibility.

## Validation so far

- Final focused DnnSqlValidationTests: 4 passed, including 12-site per-language
  denominator, SQLite/grouped path readback and shallow-query truncation.
  Full Debug -warnaserror solution suite: 4,586 passed, 0 failed, 3 skipped;
  32m 54s. Skips: opt-in full oracle sweep, unset Python provenance interop,
  Windows-only deep ASP.NET publish. These are not SQL acceptance.
- Python harness guards/provenance/mocked lifecycle: 13 passed.
- Both fixture projects built successfully with Debug -warnaserror, no warnings.
- Native synthetic CLI scan succeeded: 525 facts, Level1SemanticAnalysis, all five
  required artifacts. Final-head smoke still to run after commit.
- Public slice fetch/scan and offline DNN expectation/observation check passed
  without evaluating upstream projects; missing/wrong source input guards pass.
- Offline defaults manually checked: no fetch/resolution/activation.
- Private-path guard and whitespace checks passed.
- Initial reduced lane was intentionally interrupted after test ProjectReferences
  made the full lane mandatory; no reduced-lane pass is claimed.

## Deferred/prerequisites

No private inputs or work-machine access. No host/SSH/Docker daemon calls,
provisioning, pull, SQL, IIS, PR or merge. Live lane remains NOT RUN. Consolidated
host/resources/EULA/credentials/network/disposable cleanup approvals and separate
schema-comparison scope decision are in docs/validation/DNN_SQL_VALIDATION.md and
prompts/continue-dnn-sql-validation-harness.md. sqlcmd verifies a row and distinct
SQL RETURN/OUTPUT, captures two rowsets; ADO.NET result ordinal/type assertions
and negative mismatch execution remain deferred. No deployment or binary parity.
