# Requirements

Add a public-synthetic, offline SQL Server DDL parameter origin after #910.
Admit explicit hash-bound SQL files and separately supplied logical repo/revision/
server/database. Support schema-qualified CREATE, ALTER and CREATE OR ALTER
PROCEDURE only, without migration ordering. Preserve rule/tier/parser/extractor,
file/line provenance and explicit unsupported facets. Reject malformed, ambiguous,
unresolved, unavailable or over-bound inputs. Reuse exact source bindings and
parameter comparison; preserve JSON origin and its 52-case 8/12/32 matrix.
Every derived artifact binds exact generator and public bounded-input SHA-256.
Always partial, no aggregate clean verdict, result shapes or runtime claims.
Commit and push on the current branch; no PR, agents, merge, SQL, containers,
remote hosts, private inputs, deployment or paid reviews.
