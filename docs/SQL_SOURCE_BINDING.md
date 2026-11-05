# Inspect one conditional source-to-procedure candidate

`sqlserver-source-binding compare` connects a bounded set of C# wrapper syntax
observations to separately admitted selected SQL definitions. It produces a
**conditional profile candidate**, never a compiler-resolved terminal operation
or a runtime parameter binding. The initial profile is `dnn-reader-wrapper/1`.

The supported entry has one built-in `int` parameter and directly returns
`this.ExecuteReader("ProcedureName", parameter)`. Five explicit selectors name the
entry, provider, connection provider, helper and positional SQL formatter. Every
selected file is hashed; the CLI also checks its exact bytes against the declared
Git commit, including BOM bytes. A repository subdirectory is permitted as the
source root. Dirty-but-rehashed selected files are refused.

## Input manifest

```json
{
  "version": "sqlserver-source-binding-input/1",
  "visibility": "local-only",
  "repository": "dnn",
  "commitSha": "33b8fbe440ab665b8423599789db1f34d2a500fc",
  "profile": "dnn-reader-wrapper/1",
  "dispatchValuesOrigin": "review-scenario",
  "sqlInputSha256": "<SHA-256 of the exact SQL selection manifest bytes>",
  "template": {
    "databaseOwner": "dbo",
    "objectQualifier": "DnnTrial_",
    "valuesOrigin": "review-scenario"
  },
  "methods": [
    {
      "role": "entry",
      "path": "Data/DataProvider.cs",
      "sha256": "<SHA-256 of the exact source file>",
      "typeName": "DotNetNuke.Data.DataProvider",
      "methodName": "GetRoleGroups",
      "parameterTypes": ["int"]
    },
    {
      "role": "provider",
      "path": "Data/SqlDataProvider.cs",
      "sha256": "<file SHA-256>",
      "typeName": "DotNetNuke.Data.SqlDataProvider",
      "methodName": "ExecuteReader",
      "parameterTypes": ["string", "object[]"]
    },
    {
      "role": "connection",
      "path": "Data/SqlDatabaseConnectionProvider.cs",
      "sha256": "<file SHA-256>",
      "typeName": "DotNetNuke.Data.SqlDatabaseConnectionProvider",
      "methodName": "ExecuteReader",
      "parameterTypes": ["string", "CommandType", "int", "string", "object[]"]
    },
    {
      "role": "helper",
      "path": "Data/PetaPoco/PetaPocoHelper.cs",
      "sha256": "<file SHA-256>",
      "typeName": "DotNetNuke.Data.PetaPoco.PetaPocoHelper",
      "methodName": "ExecuteReader",
      "parameterTypes": ["string", "CommandType", "int", "string", "object[]"]
    },
    {
      "role": "formatter",
      "path": "Data/DataUtil.cs",
      "sha256": "<file SHA-256>",
      "typeName": "DotNetNuke.Data.DataUtil",
      "methodName": "GenerateExecuteStoredProcedureSql",
      "parameterTypes": ["string", "object[]"]
    }
  ]
}
```

Replace every hash placeholder. Source paths resolve under `--source-root`; for
this DNN slice that is its `DNN Platform/Library` folder. The SQL manifest and
originals use the [definition-selection contract](SQL_DEFINITION_SELECTION.md).
Repository/commit and all three template fields must match that admitted SQL
context exactly. `dispatchValuesOrigin` is `review-scenario` or `owner-supplied`;
either authenticates configuration or proves dispatch. Generated manifests can
carry the existing `provenance` generator/bounded-input hashes.

After preparing the launcher, run this as one shell command, substituting paths:

```text
pwsh -NoProfile -File scripts/tm.ps1 sqlserver-source-binding compare --input <binding.local.json> --source-root <CSharp-folder> --sql-input <selection.local.json> --sql-source-root <original-SQL-folder> --out <new-folder>
```

No source build, application invocation, database connection or SQL execution is
requested. Existing folders are preserved. Outputs must not overlap either input
root or contain either input manifest. Source/SQL bytes, the selected source Git
commit and committed blobs are rechecked inside staging before atomic publication.

## What the profile checks

| Role | Required bounded AST observation |
| --- | --- |
| Entry | One direct built-in int argument and literal procedure identifier; expected visible abstract ExecuteReader overloads; no transformed/conditional forwarding |
| Provider | Exact owner + qualifier + procedure composition; StoredProcedure intent; unchanged params array; observed configured-provider/fallback field shape |
| Connection | Direct static forwarding of all five arguments to the selected helper name |
| Helper | Conventional SqlClient provider constant; database construction; StoredProcedure-guarded formatter call; unchanged array to one external ExecuteReader invocation; bounded timeout/exception structure |
| Formatter | Recognized StringBuilder positional Exec loop; index-zero-based array iteration, primitive/non-IDataParameter path, AppendFormat order and separator/return shape |

Matching names alone do not create a candidate. Changed forwarding, visible
competing overloads, unsafe or missing selectors, ambiguous declarations, protocol
aliases/shadows and unsupported shapes remove the candidate or parameter row.
The initial source limits are 1 MiB/file, 4 MiB total, 200,000 AST nodes/file and
8,192 tokens/selected method; exactly five roles are required. C#14 syntax uses
default preprocessor symbols under the pinned Roslyn assembly. No project,
reference, semantic model or other partial declaration is loaded.

Under all recognized shapes and explicit assumptions, the literal procedure name
plus declared owner/qualifier selects exactly one admitted SQL procedure. With
one input formal, source argument ordinal zero is associated with SQL formal
ordinal zero. The SQL parameter name is read from that declaration; it is never
guessed by matching the C# variable name. Output-capable or multi-parameter
formals remain outside this mapping profile. Types, sizes, coercion, values,
OUTPUT/RETURN channels and result-column contracts are not compared.

## Read the result

| Artifact | Contents |
| --- | --- |
| `binding.local.json` | Source selectors, file/method hashes and spans; five observations; conditional target/ordinal row; gaps; embedded regenerated SQL evidence and parent digest |
| `source-sql-bindings.sqlite` | `binding_metadata`, `binding_observations`, `binding_parameters`, `binding_gaps` |
| `report.md` | Human-readable candidate and unresolved boundaries |

The Core report binds exact generator bytes, source parser/version, input hashes
and regenerated SQL evidence. The SQL evidence retains its own generator/parser,
original/selected hashes and settings. The SQLite header binds CLI generator
bytes and canonical Core report input, plus the successful source-commit check.
Names and fingerprints remain local-only. Raw source bodies, unrelated literals,
connection values and raw projected SQL are not emitted. No shareable projection
is provided; no original or retained index is modified.

`conditional-profile-candidate` and `conditional-positional-candidate` retain
Tier3 syntax evidence. Compiler symbols/overload lookup, other partial files,
preprocessor configuration, virtual/configured dispatch, owner/qualifier property
values, external PetaPoco transport, runtime invocation, caller cache/event entry,
hydration, types/coercion and result shapes remain explicit Tier4 gaps. In
particular, provider StoredProcedure intent becomes Exec text; the final
SqlCommand CommandType and actual ADO parameter binding are unavailable.

This route does not admit a DatabaseOperationCandidate terminal or substitute for
existing semantic parameter/result comparison bindings. It does not automatically
augment a workspace or claim a complete application path.
