# Claude prompt: classify the two local workspace callbacks

Read and follow
`.kiro/specs/webforms-bounded-report-memory/README.md` before doing any work.
The README is authoritative if it conflicts with this prompt.

Do not write a BRD or modify the application repository.

The post-fix TraceMap review is complete. It verified:

- `build-environment/0.5.0`;
- unknown diagnostic origins: 0;
- `LegacyWorkspacePrerequisitesUnresolved`: 0;
- exactly two genuine `WorkspaceDiagnostic` callback occurrences; and
- neither callback retained a safe `CS####` or `MSB####` identifier.

Your only task is to classify those two callbacks locally.

Build TraceMap in Debug mode. Set a breakpoint inside the
`MSBuildWorkspace.RegisterWorkspaceFailedHandler` callback in
`src/dotnet/TraceMap.Core/CSharpSemanticExtractor.cs`.

Start the same scan, but run only long enough to observe both workspace
callbacks. The complete scan does not need to finish.

Inspect `args.Diagnostic.Kind` and `args.Diagnostic.Message` in the debugger
only. Do not print, log, save, screenshot, paste, or commit either native
message. Do not put the messages in the response.

For each callback, return only:

- diagnostic kind;
- safe `MSB####` or `CS####` identifier, if present;
- one closed category from:
  - `sdk-resolution`;
  - `reference-assemblies`;
  - `web-application-targets`;
  - `imported-targets`;
  - `legacy-project-evaluation`;
  - `project-load`;
  - `solution-load`;
  - `other`;
- whether both callbacks normalize to the same category; and
- aggregate occurrence count.

If inspecting debugger variables would require printing or transmitting the raw
message, stop and provide the operator with the exact breakpoint and debugger
expressions instead.

Do not claim a root cause or recommend a TraceMap code change unless the
observed closed category proves that the existing classifier is missing a
bounded case. Do not push, open a pull request, or change application code.

