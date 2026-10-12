# SQL/database extraction matrix (#864)

Baseline: origin/dev `6cb7532528d687a2fc20ca1135db130b244549da` includes HTTP PR #863.
This is a pre-implementation inventory. Supported below means an existing bounded
extractor, not matrix acceptance. Public independently authored fixtures will
replace provisional entries with per-case validation receipts. No database methods
are executed. Framework assembly version never establishes application TFM.

| Language/mode | Existing support | Status and gaps |
|---|---|---|
| C# semantic | DbCommand/SqlClient reader/scalar/nonquery sync/async, ORM patterns | Partial: no XML/APM, adapter parity, IDbCommand or bulk matrix; provider identity checks differ from VB |
| VB semantic | DbCommand reader/scalar/nonquery sync/async, adapter Fill, command/parameter evidence | Partial: framework member parity excludes provider-only XML/APM; IDbCommand/bulk absent |
| C# failed/projectless syntax | name-only gaps and SQL construction patterns | Partial: gaps are not compiler-resolved operations |
| VB failed/projectless App_Code syntax | explicitly typed command operations and adapter Fill | Partial supported candidates: alias/shadow/other overload tests required |
| Compiled-only (both) | original ManagedIlCallObserved signatures; bounded command-binding flow | Partial: original IL is separate from source semantic classification; async/XML/APM gaps |
| SQLite/path/grouped HTML/JSON/inventory | DatabaseOperationCandidate surfaces and retained query operations | Partial: retained query denominator, not full-index/full-site discovery; metadata projection requires review |

## Reference denominator to enumerate before implementation

| Representative target | System.Data.SqlClient reference | Microsoft.Data.SqlClient selected package |
|---|---|---|
| net20/net35 | Microsoft.NETFramework.ReferenceAssemblies 1.0.3, System.Data 2.0 signed reference | Unavailable for selected modern pin |
| net40/net45/net452 | same package, System.Data 4.0 signed reference | Unavailable for selected modern pin |
| net472/net48 | same package, System.Data 4.0 signed reference | Public package 6.1.1 net462 reference, pending restore/enumeration |
| net10.0 | System.Data.SqlClient 4.9.0 public reference, pending restore/enumeration | Public package 6.1.1 net8.0 reference, pending restore/enumeration |

Each available type's public metadata overloads must be enumerated for command
ExecuteNonQuery/Scalar/Reader/XmlReader (sync, async/token, Begin/End), adapter
Fill/FillSchema/Update and separately labeled SqlBulkCopy WriteToServer forms.
DbCommand and IDbCommand are separate abstraction cases; factories prove only
encoded factory/abstract receiver boundaries. Open/Close are negative controls.

ODBC/OleDb, Npgsql, Oracle, SQLite/MySQL/DB2 and ORM/Dapper patterns are existing
partial or unavailable matrix coverage: no universal provider claim. This lane
must list each unvalidated boundary explicitly. Dynamic/late-bound dispatch,
factory concrete return inference and constructed generic body specialization
remain unproven. Mutable command text/type/parameters/transactions must never be
correlated across unrelated operations. Readable SQL/procedure capture remains
separately authorized opt-in private work; public outputs preserve availability
and fingerprints, never reconstruct text or infer procedure bodies.

## Implemented matrix (supersedes the provisional baseline above)

All entries below are bounded capabilities, with validation receipts recorded in
implementation-state.md. A supported cell is not universal provider coverage.

| Target | Public declared overloads per language | System provider | Microsoft provider | Task/cancellation APIs |
|---|---:|---|---|---|
| net20 | 46 | System.Data 2.0 | Unavailable with selected 6.1.1 pin | Unavailable in selected references |
| net35 | 46 | System.Data 2.0 | Unavailable with selected 6.1.1 pin | Unavailable in selected references |
| net40 | 46 | System.Data 4.0 | Unavailable with selected 6.1.1 pin | Unavailable in selected references |
| net45 | 70 | System.Data 4.0 | Unavailable with selected 6.1.1 pin | Supported where inventoried |
| net452 | 70 | System.Data 4.0 | Unavailable with selected 6.1.1 pin | Supported where inventoried |
| net472 | 112 | System.Data 4.0 | 6.1.1 ref/net462 | Supported where inventoried |
| net48 | 112 | System.Data 4.0 | 6.1.1 ref/net462 | Supported where inventoried |
| net10.0 | 112 | 4.9.0 lib/net8.0 (obsolete) | 6.1.1 ref/net8.0 | Supported where inventoried |

System.Data 2.0 token b77a5c561934e089 is shared by net20/net35, not an app TFM.
Classic System.Data 4.0 uses that token. Modern System.Data.Common and
System.Data.SqlClient use b03f5f7f11d50a3a; Microsoft.Data.SqlClient uses
23ec7fc2d6eaa4a5. Exact versions/signatures are in each OVERLOADS.md.
DbCommand/IDbCommand and DataAdapter/DbDataAdapter/IDataAdapter have their own
cases; SqlDataAdapter inherits the enumerated adapter members. Sync command,
XML and Begin/End overloads exist in each classic reference selection. Async
reader/scalar/nonquery/XML and bulk async/token overloads are inventoried only
where present; no nonexistent scalar Begin/End form is synthesized. net472/net48
and modern bulk providers include DbDataReader forms absent from earlier targets.

| Language / mode | Operation recognition | Reporting / state limits | Status |
|---|---|---|---|
| C# project semantic | Signed public command, XML/APM/async/token, adapter and bulk API declaration | Tier1 static call candidate; local straight-line state witnesses only | Supported bounded matrix |
| VB project semantic | Same API classifier and public declarations | Tier1; retains VB source spelling and separately encoded IL slots | Supported bounded matrix |
| C# failed-load/projectless | Explicit typed receiver, imports/aliases, inline constructors | Tier3 candidates; compiler/provider/overload/state binding unproven | Partial supported candidates |
| VB failed-load/projectless App_Code | Explicit parameters/locals/fields, imports/aliases, inline constructors | Tier3; real candidates and wrapper paths; no warning-only zero | Partial supported candidates |
| C#/VB compiled-only | Original IL observations plus signed API terminal candidates | Tier3 terminal; original signatures/provenance retained in IL facts; existing IL state binding narrower | Supported bounded API observations, partial state |
| SQLite, paths, grouped HTML/JSON, migration inventory | Rule/tier/callsite, optional bounded API/availability metadata, operation/bulk boundaries | Retained-query denominator and hashes/limits; no runtime/entry-point authority | Supported retained evidence; partial discovery |

Named controls verify literal/constant/dynamic text, Text/StoredProcedure,
parameter direction/type without values, transaction assignment, reuse/reassignment,
connection/factory and interface/abstract boundaries, aliases, fields, wrappers,
generic limits, shared/unconnected operations, same-named fakes, content-only
matches and Open/Close. Mutable state is conservative: bounded to preceding
straight-line statements on the same local/parameter; branch/unknown-call/reassignment
boundaries reset it. Field history, alias escape and cross-method state are unavailable.
Syntax generic receiver constraints and concrete factory inference are unavailable.
These limits are review obligations, not evidence that the application lacks a contract.

| Other provider/family | Matrix status | Existing evidence and remaining gap |
|---|---|---|
| ODBC / OleDb | Partial, unvalidated here | Some semantic base-slot and compiled legacy APIs; provider-specific overload/platform parity not enumerated in this lane |
| EF6 / EF Core / Dapper | Partial, outside ADO matrix | Existing C# patterns; no ORM universal SQL-generation or VB parity claim |
| Npgsql | Partial, unvalidated here | Existing C# patterns/base-slot evidence; provider-specific matrix absent |
| Oracle / SQLite / MySQL / DB2 / other | Unavailable matrix acceptance | Some preexisting patterns may remain; no positive inference from names/config/content |
| Dynamic/late binding/ambiguous dispatch | Unavailable resolved target | Gaps or explicit syntax candidates only; no guessed runtime operation |
| Procedure bodies, DB-side dependencies, readable contract capture | Unavailable by default | Requires separately authorized opt-in private inputs; hashes never supply text |
| Full-index discovery / verified UI/event entry binding | Partial parent #861 work | This lane preserves supplied retained query evidence; no full-site/unused claim |

Validation rejects missing/extra/duplicate cases and unmapped calls with named
expectations, without total offsets. Compiler fixture declarations and emitted IL
are checked independently against public inventory identities/signatures. Oracle
mutation guards cover validation machinery and CI producer/consumer paths. Native
CLI emits all required scan artifacts, repeats facts/report bytes, validates artifact
contracts and retains bounded report evidence. Windows CI must have an observed
exact-head result before being reported as accepted.

Modern fixture compiler/reference pin: SDK 10.0.302, Microsoft.NETCore.App.Ref
10.0.10. The native scan receipt retains exact generator and bounded input identity.

Compiled grouped terminal records also embed the original encoded API MemberRef,
raw-binary SHA-256, IL generator/bounded-input SHA-256 and bound/unbound provenance.
Signatures above 8,192 characters are unavailable with explicit truncation; the
API candidate and supporting fact references remain. Source state witnesses are
bounded to 256 statements / 1,024 operations per statement / 4,096 operations total;
work exhaustion explicitly leaves state unavailable. Parameter metadata is a
bounded local witness summary (including safe public enum type names), not a full
per-parameter/result contract or argument-value propagation. Connection constructor
and assignment controls retain evidence without database identities or credentials.
