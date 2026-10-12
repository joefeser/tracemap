# Legacy framework compatibility (#861)

These public synthetic libraries are compiled and statically scanned. Their methods
are never executed. Framework API availability is established from the selected
Microsoft.NETFramework.ReferenceAssemblies 1.0.3 metadata, then checked by actual
C# and VB compilation, semantic extraction, emitted IL, retained SQLite queries,
grouped handoffs and HTML. This is not an execution or runtime compatibility test.
Each framework directory has an OVERLOADS.md with the exact declaring assembly
identity and parameter/return signature for every compiled case.

| Selected target / reference set | WebClient | WebRequest / HttpWebRequest | HttpClient | JSON HTTP extensions |
|---|---|---|---|---|
| .NET Framework 2.0 | Tested: 51 overloads | Tested: 3 / 3 | Unavailable in selected framework references | Unavailable in selected framework references |
| .NET Framework 3.5 | Tested: 51 overloads | Tested: 3 / 3 | Unavailable in selected framework references | Unavailable in selected framework references |
| .NET Framework 4.0 | Tested: 51 overloads | Tested: 3 / 3 | Unavailable in selected framework references | Unavailable in selected framework references |
| .NET Framework 4.5 | Tested: 79 overloads | Tested: 4 / 3 | Tested: 30 overloads | Unavailable in selected framework references |
| .NET Framework 4.8 | Tested: 79 overloads | Tested: 4 / 3 | Tested: 30 overloads | Unavailable in selected framework references |
| .NET 10 | Tested: 79 overloads | Tested: 4 / 3 | Tested: 44 overloads | Tested: 36 overloads |
| Other TFMs, profiles, package-added/backported HttpClient or JSON | Not yet validated | Not yet validated | Not yet validated | Not yet validated |

“Tested” requires both languages and all listed reference overloads; it does not
claim exhaustive discovery in an application. The 51 WebClient cases on 2.0–4.0
cover sync and event-based async, including explicit verbs and user tokens.
Task-based WebClient methods and GetResponseAsync are unavailable in those
selected references. The 79 cases on 4.5/4.8/10 add task-based forms. HttpClient
Send and PatchAsync convenience methods are not present in the 4.5/4.8 references;
SendAsync, String/Uri convenience forms and cancellation/completion overloads are
covered where present. No package is added to manufacture an absent API.

| Evidence mode (each listed framework) | C# | VB |
|---|---|---|
| Semantic, compiler-resolved trusted API | Tested per-overload Tier1 facts and retained operation/report projection | Same |
| Projectless/syntax fallback | Tested unresolved Tier3 name candidates; no framework/signature support claim | Tested absence of HTTP API classification; unsupported |
| Compiled-only + IL | Tested original assembly/signature/IL facts and scan report; HTTP operation classification unsupported | Same |

Native scans can retain unresolved syntax candidates alongside Tier1 semantic calls. These are separate evidence rows, not extra supported overloads; they must not be counted as additional semantic coverage.

Framework identity is kept separate from application target framework:

- Legacy WebClient/WebRequest declarations resolve to `System`, token
  `b77a5c561934e089`. Both net20 and net35 reference **System 2.0.0.0**; net40,
  net45 and net48 reference **System 4.0.0.0**. Neither version uniquely identifies
  the application's target framework.
- Framework HttpClient resolves to `System.Net.Http`, version 4.0.0.0, token
  `b03f5f7f11d50a3a` in the selected net45/net48 references. The trusted identity
  checks remain unchanged. Unsigned lookalikes with the exact legacy `System`
  name are rejected as well as modern lookalikes.
- net20/net35 emitted assemblies lack TargetFrameworkAttribute; compiled extraction
  keeps their TFM unknown. The selected project/reference TFM is recorded by this
  fixture, not inferred from the System or mscorlib reference. net40/net45/net48
  emit and retain their explicit .NETFramework TargetFrameworkAttribute.
- C# can encode the base-slot `HttpMessageInvoker.SendAsync` MemberRef for the
  HttpClient override on legacy targets. The IL assertion checks what was actually
  emitted; it does not rewrite that reference into HttpClient classification.
- The original .NET 10 controls cover fakes, inherited APIs, wrappers, dynamic
  destinations/verbs, content reads and HttpRequestMessage state. Legacy fixtures
  verify available signed signatures; they do not add interprocedural state tracking.

Admitted application evidence remains a separate prerequisite: inspect its project
TFM/configuration and retained TargetFrameworkAttribute, AssemblyRef and exact
MemberRef signatures independently. A missing TFM stays unknown. This public
fixture matrix must never be substituted for the application's recorded TFM or
admission receipt. Do not include private paths, types, endpoints or artifacts in
public validation receipts. Application-specific framework matching is not yet
validated until the selected admitted evidence is identified and inspected.

Local macOS compilation/static extraction is supported using the reference package;
Windows validation runs in `.github/workflows/outbound-http-compatibility.yml` and
must be reported with its actual run/head/result, never assumed from configuration.
Runtime behavior, private Windows application build parity, targeting profiles and
package-added APIs remain outside this receipt.

```sh
dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --filter 'FullyQualifiedName~OutboundHttpCoverageTests|FullyQualifiedName~OutboundHttpLegacyCoverageTests' -warnaserror
python3 scripts/validation/http-coverage-smoke.py --output /tmp/http-public-compatibility
```

To regenerate a legacy source/signature matrix, first restore its C# project, then
run `scripts/diagnostics/HttpReferenceMatrix` with the selected reference-directory,
TFM and fixture-directory. This generator reads metadata only. It emits source and
Markdown, not a new derived machine-readable artifact. Existing scan/report writers
retain their generator SHA-256, bounded-input SHA-256, privacy and provenance rules.
Absence never means unused; static evidence never proves runtime execution.
