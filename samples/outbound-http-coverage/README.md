# Bounded outbound API coverage (#861)

Build-only public-safe C# and VB net10.0 libraries. Never invoke a fixture method:
every API overload contains synthetic/default arguments, some intentionally unusable
at runtime. No external HTTP/file/DB operation is performed by build or scan.

[OVERLOADS.md](OVERLOADS.md) enumerates 166 available public overloads, each compiled
in both languages. It includes String/Uri, cancellation/completion, JSON type/options/
context/type-info, explicit verb, user-token event async and Task variants. The
projects suppress only SYSLIB0014 because obsolete WebClient/WebRequest APIs are
intentional evidence inputs. Controls cover inherited framework members, wrappers,
dynamic destinations/verbs, request variables, content-only reads and same-named fakes.

| Input mode | C# | VB | Meaning |
|---|---|---|---|
| Resolved semantic framework API | All 166 fixture overloads | All 166 fixture overloads | Tier1 callsite/signature evidence, never execution |
| Projectless / syntax fallback | Expanded exact name set, unresolved Tier3 candidates | No HTTP API classification | No framework/overload parity claim; fakes can be candidates |
| Compiled-only + IL body evidence | Retained original IL calls | Retained original IL calls | No HttpCallDetected/report operation classification |
| Inline trusted HttpRequestMessage | Standard HttpMethod property and constant URI hash | Same | Request state held in variables is not followed |
| Dynamic URI / request variable | Call retained; destination unavailable | Same | No interprocedural/request-state reconstruction |
| Explicit constant WebClient verb | Bounded standard verb retained | Same | Custom/dynamic verb UNKNOWN; no body retained |
| WebRequest response | Call retained; verb/destination UNKNOWN | Same | Mutable request state not followed |
| WebClient file/relative/dynamic address | API call retained; protocol unproven | Same | Recognition does not prove HTTP protocol |
| User wrapper / overridden method | Framework call inside resolved body only | Same | Wrapper call is not itself a framework operation |
| Fake API / content ReadFromJsonAsync | No semantic outbound call | Same | Name-only fallback may retain an unresolved candidate |
| Legacy Framework 2.0 / 3.5 / 4.0 / 4.5 / 4.8 | Separate reference-built fixtures | Same | See [legacy compatibility matrix](LEGACY-COMPATIBILITY.md); no cross-framework overload parity inference |

The existing retained-query inventory is not full-index discovery. Reports state
mode limitations even when zero HTTP surfaces are retained. Absence never means
unused. Static evidence never proves runtime execution. Hosts, bodies, headers,
credentials and responses are not captured. C# destinations are hash-only; VB
preserves its existing normalized-path/hash policy. Public export privacy projection
remains unchanged. Generator/bounded-input hashes remain owned by existing report
writers; there is no new machine-readable matrix artifact.

```
dotnet build samples/outbound-http-coverage/csharp/HttpMatrix.CSharp.csproj -warnaserror
dotnet build samples/outbound-http-coverage/vb/HttpMatrix.VB.vbproj -warnaserror
dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --filter FullyQualifiedName~OutboundHttpCoverageTests -warnaserror
dotnet run --project src/dotnet/TraceMap.Cli -- scan --repo samples/outbound-http-coverage/csharp --out /tmp/http-csharp
dotnet run --project src/dotnet/TraceMap.Cli -- scan --repo samples/outbound-http-coverage/vb --out /tmp/http-vb
```

Legacy TFMs are representative test targets, not confirmed application targets.
Application-specific compatibility is pending work-machine validation and does not
block this branch. Use the [offline read-only inspection tool](../../scripts/diagnostics/InspectHttpFramework/README.md)
and its sanitized manual return template; keep application artifacts/local reports
on the work machine.
