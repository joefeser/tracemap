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
