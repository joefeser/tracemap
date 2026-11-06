# One verification of bounded string-array SQL composition

Read AGENTS.md. Use the owner's existing private setup and previously identified
inline-UPDATE method. Do not restart setup or change source/configuration.
Keep private source, SQL, identifiers, receipts and hashes outside public Git.

This dispatch verifies IL extractor 0.2.70 / evidence policy v71. The public
regressions include VB and C# compiler-generated String.Concat(String[]) shapes,
local arrays/aliases, null elements, nested concatenations and protected bodies.
Supported array contents are bounded to 16 arrays / 16 explicitly initialized
elements, tracked only within one method with no back edges. Escapes, incomplete
initialization, unsupported/repeated writes and conflicting joins refuse contents.
The change does not add parameter binding, general heap tracking, StringBuilder,
enumerable/object-array concatenation or shared transaction proof.

1. Confirm the clean tool checkout, build identity and actual extractor/policy.
   Preserve dirty work; never reset/clean. Run the public focused gate:

   dotnet test src/dotnet/tests/TraceMap.Tests -c Debug -warnaserror --filter "FullyQualifiedName~IlStringArrayValueTests|FullyQualifiedName~SqlWrapperRouteTests"

2. Perform exactly one fresh scan from the saved private setup, same compiled
   inputs, scope and existing resource guard/caps. Preserve all previous runs.
   No database/application execution, source changes, installs or automatic retry.
3. Bind the previously identified inline method by binary/module/token. Locate
   its exact framework String.Concat(String[]) call and linked
   ManagedIlCallValuesObserved fact using ilCallFactId. Inspect argumentOrigins:
   the array argument should carry Kind=string-array-elements with an ordered
   JSON list of hashed literals / symbolic argument slots / call results / null.
   Do not search cleartext table names to decide whether extraction succeeded.
4. Follow that call-result through the existing wrapper route. If command binding
   exists, use scripts/sql-wrapper-report.py on the new index for the exact root;
   expect symbolic-string-composition, not readable SQL. If binding is still
   unavailable, report the precise independent gap without treating the array
   snapshot as proof of command/parameter binding. Do not switch analysis routes
   silently or substitute an older scan's evidence.
5. Bulk-copy destination hash was already present; verify preservation only.
   Transaction identity, parameter attachment and runtime effects remain separate.

Stop after the single scan and bounded evidence read. Return a sanitized table:
tool identity; input equality; array snapshot present/refused; element count and
origin-kind counts (no private literals/digests); composed result propagation;
command binding; explicit remaining gap kinds; bulk operand preservation;
guard completion and prior-run preservation. Store exact private links locally.
No implementation, PR, conversion regeneration, budget increase or extra scan.
