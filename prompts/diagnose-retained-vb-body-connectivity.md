# Diagnose one retained VB body connection without rescanning

Fast-forward the clean `codex/evidence-admission-hardening` tool checkout, preserving
unrelated work. Record tool HEAD separately from the private application commit.
Use the installed .NET 10 SDK; patch updates are not a failure. Build and run:

```text
dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --nologo --filter "FullyQualifiedName~WebFormsModernizationPacketTests|FullyQualifiedName~CombinedDependencyPathTests|FullyQualifiedName~ExplicitVisualBasicContextTests" --blame-hang-timeout 2m
```

This patch fixes diagnostic classification, not database connectivity. CallEdge
and MethodInvoked facts are body observations even without outgoing graph edges.
`nextEvidenceKind=semantic-call-resolution` requests work; it is NOT a successful
semantic-resolution status. Declaration-without-body does not identify its cause.

Use the existing verified run-002 retained index, approved context and application
source. Do not scan, combine again, execute application code, change references,
extend source membership, increase budgets, or overwrite any receipt/report.

1. Select one existing Save-handler chain by its exact retained identity. Use the
   existing `webforms-review requery-handler` command with `--view method-graph`
   and a new output directory, following its current help and required run/bundle
   arguments. This rebuilds only a bounded report over retained evidence.
2. At the first blocked callee, inspect the exact MethodDeclared fact and its
   SourceIndexId, TargetSymbol, memberIdentity, evidence file/span and body span.
   Check the source locator against the approved context roster. Verify source
   bytes against its recorded hash before inspecting the method body locally.
3. Query retained MethodInvoked, CallEdge, ArgumentPassed, ObjectCreated and
   database-operation facts in that exact method span. Compare their SourceSymbol
   and source-index identity to the declaration and graph node. Keep these four
   outcomes distinct: source not in context; no call-shaped body source; source
   invocation present but no fact; fact present but graph identity/edge missing.
   A source invocation is not itself proof that a compiler binding should exist.
4. If there are relevant compiler diagnostics, correlate their specific code and
   source span to the failed site. Do not attribute every missing edge to the
   presence of a scan-level CompilerDiagnostic. Do not assume an IL bridge is
   required when a source-to-source route might suffice.
5. Separately locate the previously missing GoToBidLines declaration in the
   authorized source. Report whether it is absent, outside the approved roster,
   generated, inherited/external, or present but not extracted. Do not guess.
6. Preserve the previous accepted results: 24 registration candidates and two
   restored selected-page inline behaviors. Report existing database-operation
   inventory separately from selected-handler terminal reachability. Do not add
   different fact-family counts together or describe candidates as runtime use.

Return a compact, sanitized diagnosis: exact tool HEAD, tests, one blocked-step
classification, whether source/fact/edge exists, supporting rule and tier, relevant
diagnostic code if established, and a minimal public-safe synthetic VB shape that
reproduces the failure. Keep source text, identifiers, paths, private hashes,
assemblies, context and raw diagnostics local. No private artifacts are to be
committed. If the retained data cannot establish a cause, state precisely what is
missing rather than requesting another broad scan or asserting a repair.
