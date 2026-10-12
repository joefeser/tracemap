# Backend extraction inventory — first testable slice

Tracking: [#861](https://github.com/joefeser/tracemap/issues/861).
Goal: move human-selected database and outbound service functionality into a
.NET 10 API. This first slice does not generate or deploy API code.

## Try it

Build this branch's CLI and tests:

```sh
dotnet build src/dotnet/TraceMap.sln -warnaserror
dotnet test src/dotnet/TraceMap.sln --no-build --filter 'FullyQualifiedName~BackendExtractionSampleTests|FullyQualifiedName~GroupedCompiledPathHandoffTests|FullyQualifiedName~ConstructedGenericSampleTests'
```

Generate a **new** native Web Forms grouped report or retained-handler requery
using this tool build and your existing admitted inputs. Preserve the original
run/checkpoints and pinned tool. Do not replace a receipted generator in place
or edit old artifacts to add fields. Standard report/updated-reader admission
still applies; report any refusal rather than bypassing identity checks.

Open `compiled-paths.local.html`: the new **Backend extraction inventory** section
lists retained database and HTTP evidence. The companion
`compiled-paths.handoff.local.json` contains `backendExtraction` and its rule,
scope, coverage, truncation, retained-gap count and operation records.

Each record references the full node evidence, zero-based ordered variant
indexes, query-root node references and candidate-variant indexes. The rule/tier,
source/scan/commit, source or compiled location, command-binding fields and
supporting evidence remain in the existing node/edge/path records. Different
evidence records are not collapsed merely because labels match.

`found-not-connected-in-supplied-query` is not "unused". Query roots are not
automatically verified events. Rows do not count unique procedures or business
operations. Human decisions remain `undecided`; do not edit an admitted handoff
to approve a migration. A separate owner-decision receipt is future work.

Only database-api, sql-query and http-client surface records already retained
by the query are included. This is **not the independent full-index inventory**.
No new readable SQL, procedure definition, destination secret or HTTP payload is
captured. Command hashes are not SQL text. Unknown evidence remains unknown.

New constructed-generic edges include `limitations` in ordinary path JSON,
grouped JSON and method-graph JSON. Grouped HTML, method-graph HTML and displayed
Markdown paths explain that generic definitions are not specialized downstream.
Existing receipts and legacy handoffs remain unchanged/readable.

## Migration planning

Use `prompts/plan-backend-api-extraction.md` with explicitly authorized private
evidence. It is a new planning prompt; existing Claude diagnostic launchers are
not automatically changed to use it. It separates extracted facts, missing
contracts, human retain/retire decisions and proposed API operations. Automatic
launcher integration and implementation authority remain follow-on tasks.

## Synthetic inputs and validation

`samples/backend-extraction` is a build-only .NET 10 library: shared procedure
call, unconnected query, literal/dynamic HTTP targets and a text-only decoy.
Native scanner/report tests consume it without calling any method. Existing
compiler-built constructed-generic fixtures cover positive/negative resolution.

The initial discovery spike found no HTTP surface for `HttpClient.GetStringAsync`
in a semantic+IL scan. The positive HTTP fixture uses supported `GetAsync`.
Convenience API coverage and unsupported-family accounting are recorded in #861;
the missing surface is not a statement that the application makes no HTTP call.

Full-index discovery, private opt-in command/contract capture, entry-point
binding, owner-decision receipts and end-to-end API migration acceptance remain
open. No language extractor was changed by this slice; authentic Windows/private
site acceptance and the full repository suite are separate validation gates.
