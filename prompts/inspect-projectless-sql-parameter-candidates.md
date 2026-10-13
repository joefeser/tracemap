# Inspect projectless SQL parameter candidates locally

Use the published scanner with vb-syntax/0.4.2 or later. This increment adds
DatabaseParameterCandidate facts for bounded explicit SqlParameter constructor
syntax, not automatic wrapper-to-procedure argument mapping. Reuse existing
current source-scan output if it contains this capability; otherwise run only
the previously authorized focused source scan with the documented command and
known local paths. Do not rebuild/execute the application, access a database,
alter private source, commit, export patches or upload private output.

Inspect the Database Parameter Candidates section of report.md and its fact IDs
in facts.ndjson/index.sqlite. Use source spans to review parameter constructions
at the already selected caller. Names/expressions are hashed by default. Type
syntax is a candidate; directions and command association remain unknown. Named
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
