# Collect source, compiled publication and offline SQL definitions

Start with a disposable synthetic run. This creates two tiny Git repositories,
compiles their managed assemblies, prepares the native correspondence receipts,
and adds authored SQL definitions. It never connects to SQL Server or executes
SQL. The managed fixture is not an IIS/Windows ASP.NET publication test.

From macOS **zsh**, prepare the launcher once, then run the trial using a fresh
output folder. These are single-line shell commands:

```sh
pwsh -NoProfile -File /Users/josephfeser/src/gh-joe/tracemap/scripts/tm.ps1 -Build version
pwsh -NoProfile -File /Users/josephfeser/src/gh-joe/tracemap/scripts/tests/Test-WorkspaceLauncher.ps1 -WithSql -OutputRoot "$HOME/tracemap-reviews/sql-trial-01"
```

Open `~/tracemap-reviews/sql-trial-01/workspace/runs/first/START-HERE.md`.
The script also collects a separate `web-only` run to exercise repository filtering.
Existing folders are preserved; choose another name when repeating the trial.

Expected SQL inventory: one table and three procedures. `ReadItems` declares two
parameters and a first result with `Label, Id` in that order. The filtered and
dynamic procedures retain unknown result shapes. All SQL evidence remains partial.
The source/compiled report and the SQL report are linked from the same start page.

## Use your own offline definitions

First verify the [source workflow](WORKSPACE_GETTING_STARTED.md) and, when needed,
the [compiled publication workflow](WORKSPACE_COMPILED_GETTING_STARTED.md).
Keep their configuration and explicit receipt preparation. SQL is independently
admitted; no source method is automatically matched to a stored procedure name.

Use a schema-only, bounded **definition set**, not a database backup or a dump
containing rows. The initial limits are 32 `.sql` files, 256 KiB per file, 1 MiB
total, 512 objects and 4,096 references. Each file also has lexical token and
complexity limits. UTF-8 input, exact hashes and explicit two-part object names
such as `dbo.ReadItems` are required. Over-limit or unsupported top-level scripts
are refused; the collector does not silently truncate or execute them.

Create a local manifest, for example `configuration/sql-definitions.local.json`:

```json
{
  "version": "sqlserver-local-definitions-input/1",
  "visibility": "local-only",
  "contextRepository": "web",
  "contextCommitSha": "<the selected web repository Git HEAD>",
  "definitionSet": "review-schema-export",
  "server": "ExampleServer",
  "database": "ExampleDatabase",
  "files": [
    { "path": "schema.sql", "sha256": "<SHA-256 of the exact schema.sql bytes>" }
  ]
}
```

The placeholders must be replaced. Obtain the commit with `git -C <web-root>
rev-parse HEAD`, and on macOS the file digest with `shasum -a 256 <schema.sql>`.
`contextRepository` is the workspace repository **ID**, not its absolute path.
The application commit provides collection context; it does **not** establish the
SQL export revision, which database was deployed, or correspondence with binaries.
Generated manifests may carry a `provenance` object with `generatorSha256` and
`boundedInputSha256`; the supplied trial records its script and bounded input.

To add SQL to a workspace, change its schema to `tracemap-workspace.v3` and add:

```json
"sqlEvidence": {
  "repositoryId": "web",
  "manifestPath": "configuration/sql-definitions.local.json",
  "sourceRoot": "../schema-export"
}
```

Paths resolve from the workspace configuration folder. The `.sql` paths inside
the manifest resolve from `sourceRoot`. Choose a dedicated SQL input directory;
do not set `sourceRoot` to the workspace configuration folder itself. V3 permits the existing `compiledReview`
section or source plus SQL alone. V1 remains source-only; V2 retains its compiled
contract. A repository filter must include the SQL context repository. Changed
hashes, stale context commits, duplicate definitions and overlapping output/input
folders refuse admission before a run starts.

Use the same `workspace verify` and `workspace collect` commands as the source
workflow. For SQL alone, the launcher also exposes:

```text
sqlserver-definitions verify --input <manifest> --source-root <definitions>
sqlserver-definitions collect --input <manifest> --source-root <definitions> --out <fresh-folder>
```

## Read the evidence

| Artifact | Purpose |
| --- | --- |
| `combined.sqlite` | Existing source and explicitly admitted compiled scan evidence |
| `sql/definitions.local.json` | SQL declarations, parameters, references, supported first results, gaps, rules, spans and provenance |
| `sql/sql-evidence.sqlite` | SQL sidecar tables: `sql_objects`, `sql_parameters`, `sql_result_shapes`, `sql_dependencies`, `sql_unknowns`, `sql_collection_metadata` |
| `sql/report.md` | SQL inventory and explicit limitations |
| `workspace-run.local.json` | Local run state, input/output hashes and links to each evidence channel |

The SQL index is **not** a raw scan index and is never passed to `combine`.
Its metadata binds the CLI generator bytes and canonical SQL export input; that
export separately binds Core generator bytes, parser bytes and hashed source files.
Input and output drift is checked before workspace completion. Failed runs are
retained with a failed state; they do not receive a completed start page.

Parameters are declared types and directions, not a full SQL comparison contract.
Dependencies are syntax references to admitted declarations, not execution or
reachability. Only a narrow single-SELECT result subset is modeled. Filters,
joins, branches, dynamic SQL, multiple results and unqualified/external resolution
remain unknown. View/function definitions are inventoried but their result shapes
are not modeled. Unapplied schema mutations invalidate complete table/result claims.

`USE` must match the manifest database. `ANSI_NULLS ON` and
`QUOTED_IDENTIFIER ON` headers are accepted as declarations, with runtime gaps;
OFF prevents complete shape claims. SQLCMD substitutions/directives are refused.
Top-level inserts, execution (including extended-property calls), schema creation
and other unsupported setup statements are refused. Migration ordering is not
inferred; duplicate/case-only definitions are rejected. DNN install templates use the separate [explicit definition selection contract](SQL_DEFINITION_SELECTION.md);
qualifiers are never silently replaced. The original definition-set contract above stays unchanged.

Names, paths and fingerprints remain **local-only**. Bodies, default expressions
and literal values are not copied into output. There is no public/shareable
projection and no change to the existing public-synthetic SQL admission routes.

## Test layers

Authored unit fixtures specify expected parameter directions, column order,
line spans, references and refusals before comparing extracted output. CLI tests
read the SQLite sidecar back and check literal/default omission. Workspace tests
exercise context/scope failures and drift after publication. The synthetic
PowerShell trial exercises all three evidence channels and filtering.

The opt-in public corpus smoke is `scripts/tests/Test-SqlDefinitionCorpus.py`.
It fetches exact pinned public bytes and license files; no upstream build,
application, database or SQL is executed. DNN templates and WWI setup statements
must remain refusals on the original route, while explicit DNN selection and WWI procedure declarations produce partial
inventories with unknown complex result shapes. This is compatibility evidence,
not full DNN route binding or runtime acceptance.

For the actual bounded public DNN workspace trial and its SQL admission gaps, see
[DNN trial results](validation/DNN_WORKSPACE_TRIAL.md).

For the separate conditional C# wrapper-to-procedure inspection command, see
[Source-to-SQL candidates](SQL_SOURCE_BINDING.md). It retains dispatch/transport
gaps and does not automatically change workspace or semantic binding behavior.
