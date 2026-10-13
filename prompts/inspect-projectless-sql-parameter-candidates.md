# Inspect projectless SQL parameter candidates locally

Use the published scanner with vb-syntax/0.4.9 or later. This increment adds
DatabaseParameterCandidate facts for explicit SqlParameter constructors and
typed command.Parameters.Add(name, SqlDbType) calls, not automatic
wrapper-to-procedure argument mapping.
The Add-site and declaration checks now share relative-type/import resolution,
including SqlClient.SqlCommand with Imports System.Data. This fixes a declaration
association inconsistency; it does not establish execution or procedure contracts.
Reuse existing current source-scan output if it contains this capability; otherwise run only
the previously authorized focused source scan with the documented command and
known local paths. Do not rebuild/execute the application, access a database,
alter private source, commit, export patches or upload private output.

Inspect the Database Parameter Candidates section of report.md and its fact IDs
in facts.ndjson/index.sqlite. Use source spans to review parameter constructions
at the already selected caller. Names/expressions are hashed by default. Type
syntax is a candidate; directions remain unknown. Inspect commandAssociation:
syntax-local-straight-line-candidate links a fresh local declaration and consecutive
parameter additions to an execution span; it is not a verified command instance
or procedure binding. Other shapes remain unresolved. Count these states separately.
The association now allows a single Try/Catch/Finally/Using statement list,
bounded constructor arguments and simple parameter-value/transaction assignments.
It never crosses block boundaries. Report commandAssociationReason for every
unresolved candidate; distinguish unsupported association from successful parameter
extraction. Do not claim that an unavailable execution line proves the check ran.
Count commandReceiverAssociation and commandExecutionEvidence separately. A
syntax-local-declaration-candidate is receiver evidence at the parameter Add site;
later complex value expressions can leave execution unresolved without erasing it.
The legacy commandAssociation field is the combined forward-check result, not the
receiver-only count. Do not report zero receiver associations solely from that field.
Also count laterExecutionSyntax separately. same-receiver-text-call-observed records
a later call location within the bounded same-block window even across expressions
with unknown effects. Read laterExecutionSyntaxReason and its location fields;
never interpret this lexical observation as instance continuity, reachability or
procedure verification. The stronger commandExecutionEvidence can remain unresolved.
Inspect parameterValueObservationCount and the indexed expression hashes/kinds/source
locations locally. These observations match receiver text and exact literal index,
not runtime parameter identity; repeated assignments remain separate observations.
Inspect commandTextOriginEvidence: constructor-first-argument-syntax-only records
only a hash/kind/location, not resolved SQL text or overload proof. Use the report's
pending review checklist; do not mark contracts verified from these fields. Return
only approved aggregate counts/statuses, never expression text or private hashes.
Before any scan, confirm the saved project configuration and resolved source root
locally against the user's intended project. Do not infer the project from prior
debugging context or scan a different registered repository. Named
arguments, other providers, ambiguous imports and cross-method wrapper propagation
are not solved by this increment. Do not rewrite application code to fit the
extractor. Preserve source/binary historical identity boundaries.

Record constructor candidates separately from human-reviewed parameter mappings
and schema contracts. Match them to the selected slice only with supporting
local evidence; do not join merely by parameter name or nearby line. If a known
source construction is missed, return its generic syntax shape and status only,
not private source, names, hashes or patches. This provides a bounded next fixture
case for the public development machine.

Return human-approved counts and generic statuses: candidates retained, selected
caller candidates, unresolved associations, unsupported shapes and pending schema
checks. Keep all detailed reports and source inspection local.
