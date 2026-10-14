# Migration regression corpus

Public, independently authored synthetic VB inputs. Static scan only: do not
execute methods, SQL or network requests. The missing external base is deliberate.
No private source, identifiers, paths, assemblies or outputs belong here.

Run `MigrationRegressionCorpusTests` and `IntegratedMigrationReportTests` from
the .NET test project. They scan the real checked-in files, not copied test strings.
The report must preserve source fact references, evidence tiers, unknowns and
deterministic ordering. An observed event chain is not a deployed HTTP route.

| Regression | Durable sample / acceptance |
| --- | --- |
| Typed Parameters.Add, five separate value observations | SqlShapes.vb / SaveCore; five parameters group under one operation |
| Nested Try, constructor arguments and transaction assignment | SaveCore; receiver candidate retained, strong execution remains unresolved |
| Relative SqlClient type spelling | SaveCore; declaration and parameter extraction agree |
| Complex value expressions | SaveCore; later execution observed lexically, effects not invented |
| String.Empty, += and &= text reconstruction | SaveCore; bounded hash candidate, no raw SQL in facts/report |
| Aliased SqlParameter and ordinary object creation | AliasConstructor; parameter candidate plus ordinary object/call facts retained |
| Reassignment and nonconstant text negatives | ReassignedCommand / UnknownText; no stale execution or constant-text proof |
| Bulk copy versus command parameters | BulkOnly; unconnected operation retained, no five-parameter leakage |
| Handles with unavailable external base | EventChains.vb / ExternalBaseWidget; complete helper/HTTP and wrapper/SQL candidate chains, ancestry unresolved |
| Direct UserControl versus desktop events | DirectWidget / DesktopEvents; event connectivity separate from framework assessment |
| Same-name, same-arity overload ambiguity | AmbiguousEvents.vb; both HTTP operation groups retained but neither guessed as the handler target |
| Markup page and control boundaries | ../migration-vertical-slice; existing real markup fixture and integrated tests |
| Ambiguous ownership, missing facts, unrelated names, caps | IntegratedMigrationReportTests fact mutations; refusal reasons preserved |
| Unrelated handler fan-out starving search | IntegratedMigrationReportTests; reverse search retains the relevant chain within budget |
| Provider/type alias and shadowing variants, blocks, expression limits | ProjectlessParameterCandidateTests; executable positive/negative matrix |
| SQL source/compiled providers and wrapper paths | ../sql-database-matrix plus SqlWrapperRouteTests; compiled/source provenance stays separate |

The corpus is not a claim of complete VB/ECMA coverage. Missing source edges,
external ancestry, schema contracts, runtime identity, deployment routes and
current-build correspondence remain separate checks. Historical truncation and
provenance protections remain covered by the grouped SQL report tests, not by
invented private artifacts in this source corpus.
