# Shared HTTP entry with two terminals

Public synthetic VB/.NET Framework 4.8 compile-only fixture. Never host or execute
it: it contains outbound API calls and intentional recursion. All URLs use
example.invalid. No private application source or identifiers are included.

`QueryPanel.RefreshChanged` is bound to `RefreshToggle.CheckedChanged` and calls
`BeginQuery`, which constructs `ProfileQuery`. Its constructor calls
`RemoteCalls.FetchProfile`, which calls `FetchToken` and separately performs a
profile upload. The event handler also calls an intentionally recursive method.

The regression requires both HTTP terminals independently from the event entry,
exact locations/rules/tiers/supporting evidence, and retained cycle truncation.
A helper-root query must not claim the event entry. A depth-one query is incomplete
and does not retain both terminals. Grouped handoff round-trip and incomplete-enumeration
HTML wording are checked. HTTP verb inference is not assumed from reachability.

Run from the repository root:

```text
dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --filter FullyQualifiedName~Shared_event_entry_retains_two_independent_terminals_despite_cycle_truncation -warnaserror --nologo
```

The signed framework reference package is compile-time evidence only. Tests scan
the fixture and build static reports; they never execute its methods or send HTTP.
