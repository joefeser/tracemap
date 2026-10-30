# Scan continuation suspended — whole-protocol takeover first

EXPECTED_IMPLEMENTATION_COMMIT: `9b841e50e9ec9e59c459678d2590842a2326ced6`

The latest implementation recognizes exact signed runtime connection State reads
as UNBOUND and records their connection/site. It does not make a state-dependent
lifecycle branch decidable. Re-running the private scan expecting entry candidates
from this patch is not authorized by this prompt and would repeat the micro-fix loop.

Use prompts/inspect-sql-lifecycle-whole-protocol.md for ONE read-only capability
map from existing output; use prompts/glm-sql-lifecycle-takeover.md for the full
state and next implementation assignment. No build or scan is needed for that map.

A future single validation prompt must be pinned to the whole-protocol public
implementation AFTER its synthetic capability-fit gate. It must retain --repo,
--compiled-only, exact verified DLLs and two inner-root selectors, existing
boundary flags, fresh output/progress paths, Windows job self-test and the
60-second / 2048-MiB job cap. It must stop after one scan and report partial
coverage/unknowns without increasing limits or retrying. Child exit zero and
artifact publication alone do not prove entry lineage or SQL acceptance.
