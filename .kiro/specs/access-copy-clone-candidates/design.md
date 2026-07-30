# Access Copy/Clone Static Candidate Design

## Decision

Add a read-side `tracemap-access copy-clone` command and
`AccessCopyCloneCandidateReporter`. It composes the source-neutral facts from
#550 through the flow model from #551. It does not add a second query extractor
or widen the v1 protected input vocabulary.

The persisted query contract exposes a query kind and undirected dependency
candidates. That is enough to identify conservative mutation shapes, but not
enough to prove source/target roles, column correspondence, generated-key
handling, or a clone. V1 therefore deliberately emits those limitations as
gaps.

## Candidate matrix

| Persisted query kind | Classification | Shape |
| --- | --- | --- |
| append | Candidate | bulk-append-shape |
| make-table | Candidate | table-creation-shape |
| update | NeedsReview | update-in-place-shape |
| bulk | NeedsReview | bulk-mutation-shape |
| compound | NeedsReview | compound-mutation-shape |

All other query kinds are out of the v1 candidate set. In particular, names
containing `Clone`, `Copy`, `Duplicate`, or `New` are never inspected.

## Composition

For each qualifying `AccessQueryDeclared` fact:

1. validate its opaque target stable key and closed query kind;
2. collect `AccessQueryDependencyCandidate` facts whose source is that query;
3. project each safe target as `dependency-role-unknown`;
4. build the bounded #551 flow report from the same fact set;
5. reference every flow path containing the exact query stable key;
6. aggregate stable provenance and safe limitation tokens; and
7. add rule-backed gaps for every unsupported conclusion.

This preserves reverse lookup from a candidate to its possible UI/startup
paths without turning adjacency into runtime reachability.

## Output

The command writes `access-copy-clone.md` and `access-copy-clone.json`
atomically under schema `tracemap.access-copy-clone-candidate.v1`.
Candidate, participant, and gap identities are hashes of already-safe evidence
identity. Raw property bags are never serialized.

## Provenance and tiers

Query declarations retain Tier2 structural evidence. Dependency and flow
evidence retain their upstream tiers. The candidate lists all supporting fact
IDs and adds `legacy.access.copy-clone-candidate.v1` to its rule set. Every
candidate also carries that rule, weakest tier, real commit SHA,
repository-relative primary span, and extractor/version as first-class
metadata. The report carries the same hashed repository identity and commit as
the flow input. Composition uncertainty is Tier4.

## Deferred richer evidence

Role-specific parsing of `INSERT ... SELECT`, bounded field mapping,
DAO/recordset mutation calls, generated-key handling, loop sequencing, and
macro action evidence need a separately approved evidence-contract version or
safe exporter mechanism. They are not inferred from current undirected
dependencies and are not reasons to widen Access COM.

## Privacy

Only closed categories, opaque stable IDs, portable safe relative spans, and
validated provenance are rendered. POSIX, Windows-drive, and UNC paths are
rejected on every host. The reporter ignores raw SQL/name/source properties
even if malicious synthetic facts contain them. Failure text exposed by the
CLI remains classification-only.
