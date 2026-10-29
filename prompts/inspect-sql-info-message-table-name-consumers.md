# Work-machine InfoMessage table-name consumers — read only

This is the next inspection after inspect-sql-connection-constructor-delegate.md.
Use its existing private report and the existing native receipt. Do not repeat
either prior inspection or scan. No implementation is authorized by this prompt.

## Question

The inspected connection wrapper constructs an instance SqlInfoMessageEventHandler,
stores it in a private field, and subscribes it to SqlConnection.InfoMessage in
Open(). Its callback parses message text and appends strings to a private ArrayList.
We need to establish whether that list affects the DataSet returned by the selected
SQL roots, especially table names, mappings, table order or result selection.
Event timing, callback purity and runtime success remain unknown.

## Scope and bounds

Read AGENTS.md. Preserve dirty files, existing inputs and outputs. Use existing
source or local IL/decompiler tools read only. No edits, builds, tests, native scans,
application/SQL execution, package installation or subagents. Do not add framework
assemblies as compiled scanner inputs. If inspection is unavailable, say so and stop.

Identify the callback's private ArrayList field using its exact declaring type and
field token in the binary bound by SHA in the existing receipt. Private names stay
local; call it TABLE-NAME-LIST in the returned report. Do not confuse this field
with the separate ArrayList containing SQL parameter objects.

Use Find All References/Analyze on the field, or search the existing IL dump for
its exact declaring-type/field reference. Include ldfld, ldflda and stfld; follow
local aliases and immediate calls receiving the list. Inspect initialization,
reset/clear, append, enumeration/indexer reads and escape sites. Search references
in existing admitted inputs, but do not interpret unrelated bodies as root calls.

Inspect at most 8 relevant bodies and 2048 total IL instructions. If exceeded,
return the partial findings and exact unresolved scope; do not restart or widen.
Follow consumers to an observable sink or unresolved call within that bound.
Record evidence for whether each consumer is actually reachable from a selected
root, using the existing forward call chain; do not infer reachability from a
field reference alone. Callback invocation is an external event possibility, not
a proven direct root call.

## One sanitized report, at most 35 lines, then STOP

- Existing receipt/tool SHA, binary alias, field token/type and owner alias.
- Reference inventory: method aliases/tokens, read/write/address/escape role,
  inspected or truncated, and root reachability evidence or unknown.
- For each read, final sink: DataTable.TableName, DataSet.DataSetName,
  DataAdapter.TableMappings, table add/remove/reorder, index/name selection,
  return value, logging-only, unrelated sink or unknown. Do not choose a category
  without tracing the value to that sink.
- If tables are renamed/mapped: identify ordinal versus name lookup, loop/order
  relation to the list, list-length mismatch/fallback behavior, and whether this
  occurs before or after Fill and before returning the same DataSet allocation.
  Report unknown allocation identity explicitly instead of assuming a match.
- State list reset/reuse scope and whether multiple executions can share its
  contents. Distinguish observed instructions from runtime/event behavior.
- Correct the earlier callback size ambiguity: report IL code size in BYTES and
  decoded INSTRUCTION COUNT separately, plus exception-region count. The prior
  report called 263 both bytes and instructions; do not silently pick one.
- End with observed dependency, unresolved effects and a minimal synthetic fixture
  description in prose. No private code, IL dump, literal strings, SQL or paths.

Keep full receipts and private material local. Return short text only; no email,
code transfer or more photographs requested. Do not classify the event as harmless
or skip the constructor/callback merely because direct command writes were absent.
