# Build bounded SQL Server schema admission and parameter evidence

## Objective and authority

Implement the next offline slice after merged PR #909: a versioned SQL Server
schema-evidence admission contract and deterministic comparison of explicitly
bound stored-procedure calls against parameter declarations. Use independently
authored public synthetic C# and VB fixtures. This is a bounded continuation of
#908 and the parameter portion of #870, not completion of either issue.

Build, test, commit and push on a new `codex/` branch from freshly fetched
`origin/dev`. Do not merge. Do not start with another operator questionnaire:
inspect current capabilities, make documented conservative format decisions,
and exhaust local synthetic tests. If a genuinely new authority decision is
required, return all blockers together once.

Read AGENTS.md, docs/VALIDATION.md, live issues #908 and #870,
docs/validation/DNN_SQL_VALIDATION.md, the existing harness spec/state, and
the current database fact/projection/report contracts before editing. PR #909
merged as `fe48109124912d0b22516ce009ee34e7d59ff7bd`; verify ancestry rather
than assuming this remains the latest dev head. Preserve unrelated worktrees.

## Scope

1. Create a small spec with requirements, design, tasks and implementation-state
   under `.kiro/specs/sqlserver-parameter-evidence/`. Keep it current as work
   completes. Reuse existing components where their contracts actually fit;
   PostgreSQL validation and harness logs are not SQL Server schema admission.
2. Define and document one explicit versioned schema-evidence format, with a
   deterministic producer for the supported synthetic declarations. Prefer an
   existing SQL parser if available; do not build a general SQL interpreter.
   Unsupported declarations must fail admission or retain typed gaps, never be
   guessed. Bind repo/revision, evidence origin, generator bytes, bounded input,
   source locations and supported extractor version. Synthetic declarations,
   DDL-derived facts, catalog-described metadata and runtime observations are
   different origins; this phase implements only the supported offline origin.
3. Provide explicit opt-in admission through a native CLI path and exercise it
   in an end-to-end smoke. Validate schema version, hashes, duplicate/ambiguous
   identities, malformed fields and expected input identity before comparison.
   Every derived machine-readable artifact needs exact generator SHA-256 and
   bounded-input SHA-256. Public output hashes only privacy-projected inputs.
   Repeated identical inputs must produce identical semantic output; do not
   introduce wall-clock fields into deterministic artifacts.
4. Represent exact logical server/database/schema/procedure identity plus an
   explicit source-to-schema binding. Synthetic identity is not a deployed
   server observation. A bare procedure name or hash is insufficient. Do not
   infer DNN owner/object-qualifier substitutions or connection identity.
   Missing, conflicting, stale or ambiguous bindings yield unknown/gap, not match.
5. Extend bounded method-local command evidence only as needed to associate
   each supported parameter's name, SQL type, declared size/units and direction
   with the correct command and execution site. Preserve unsupported aliases,
   mutation, branching, dynamic names and helper boundaries as gaps. Existing
   command-level witness lists cannot masquerade as per-parameter evidence.
   Cover both C# and VB without regressing existing call/path reporting.
6. Compare exactly bound StoredProcedure calls with admitted parameter
   declarations. Emit bounded match, mismatch or unknown with rule IDs, tiers,
   source/schema locations, revision and extractor identities. A partial
   comparison must never become an aggregate clean verdict. Distinguish input,
   output and input/output; preserve ReturnValue separately from procedure
   parameters, affected-row counts and result sets. Respect supported default/
   optional-parameter semantics or explicitly report them unsupported. Do not
   invent a pure output-only declaration from SQL Server's OUTPUT syntax.
7. Document length semantics explicitly: bytes versus characters, nvarchar,
   varchar and MAX, absent/default sizes and provider uncertainty. Do not claim
   differing sizes prove runtime truncation or incompatibility. Name/type aliases,
   identifier case/collation and precision/scale must be resolved under an
   explicit supported policy or remain unknown; do not silently normalize away
   differences. Text commands remain outside stored-procedure comparison.
8. Carry comparisons and limitations into machine-readable and human-readable
   reporting using a documented native seam. Keep call reachability, declared
   schema evidence and comparison conclusions distinct. Do not upgrade tiers or
   retrofit an unsupported schema into existing closed receipt contracts.

## Regression matrix and acceptance

Use the existing DNN SQL synthetic fixtures where practical, adding independently
authored cases as needed. No fixture application/database method executes.

- Exact bound positive case in C# and VB; wrong database/schema/procedure and
  missing binding never join by short name.
- Independent type, size and direction mismatches; missing/extra parameters;
  supported defaulted parameter behavior or explicit unknown.
- ReturnValue, OUTPUT, input/output, affected rows and result sets remain distinct.
- Unicode length units, MAX, missing size, unsupported type/precision/collation
  policies, duplicate names and ambiguous declarations.
- Two commands in one method cannot share parameter state accidentally; dynamic
  names, conditional assignment, alias mutation and unsupported wrappers stay
  unresolved. Explicit Text commands are not treated as stored procedures.
- Malformed/unsupported export version, tampered hashes, duplicate object identity,
  incorrect revision/binding and substituted inputs fail closed or produce the
  documented gap. Self-consistent hashes are integrity, not authentication or
  deployment proof.
- Deterministic repeated output, exact public-input provenance, serialization
  round trip, native CLI admission-to-report smoke, and preserved gaps/truncation.
- Existing DNN static validation and Python harness guards still pass. Preserve
  the exact inventory, scalar-version and deterministic receipt fixes in #909.

Run focused tests while developing, then the full Debug solution suite with
`-warnaserror` for shared-core changes. Follow docs/VALIDATION.md for relevant
language adapter smokes; report actual passes/skips/deferred gates. Run the CLI
against synthetic inputs at the committed head. Ordinary dependency restore is
allowed; default tests must not need a database or download public corpora.

## Explicit non-goals / boundaries

No private FFP/employer source or schema, work-machine artifacts, historical
acceptance files or credentials. No SSH, dockercache probes, Docker calls, image
pulls, EULA acceptance, SQL execution, IIS or application deployment. Existing
optional live-harness approval requirements remain unchanged.

No result-column/consumer comparison, result-set inference, full DNN binding,
general SQL semantics, runtime parameter values, dynamic SQL interpretation,
framework dispatch proof, transaction behavior or migration/deployment readiness.
The real DNN selected reader join remains unavailable unless separately proven;
synthetic parameter comparison does not clear that limitation. Do not close
#870 or imply private SQL-A acceptance.

## Delivery

Push the bounded implementation with a concise evidence-backed summary and one
consolidated follow-up prompt. Include the supported declaration/command subset,
exact fixture denominator, native commands, expected match/mismatch/unknown
matrix, provenance checks, tests and remaining gaps. Leave live activation and
result-column comparison as separate future decisions, not required work-machine
round trips for this phase.

If opening/updating a PR, first read the current pr-review-loop skill and verify
the canonical ACK executable/version/provenance plus normal repo doctor/preflight.
Preserve lane policy, review ceilings and human merge gates; never request Qodo.
Use durable handoffs and let ACK classify exact-head review evidence. No merge
or new paid review spend is authorized by this prompt.
