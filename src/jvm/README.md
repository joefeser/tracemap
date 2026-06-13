# TraceMap JVM Scanner

`tracemap-jvm` scans Java/Kotlin repositories into TraceMap-compatible artifacts:

```text
scan-manifest.json
facts.ndjson
index.sqlite
report.md
logs/analyzer.log
```

The scanner is deterministic and evidence-backed. It does not run Maven, Gradle, annotation processors, target application code, LLMs, embeddings, or network restore commands during scan.

## Build

```bash
JAVA_HOME=/opt/homebrew/opt/openjdk@21/libexec/openjdk.jdk/Contents/Home gradle -p src/jvm test
JAVA_HOME=/opt/homebrew/opt/openjdk@21/libexec/openjdk.jdk/Contents/Home gradle -p src/jvm installDist
```

## Scan

```bash
src/jvm/build/install/tracemap-jvm/bin/tracemap-jvm scan \
  --repo samples/jvm-modern-sample \
  --out .tracemap-jvm
```

Then reduce with the existing .NET reducer:

```bash
dotnet run --project src/dotnet/TraceMap.Cli -- reduce \
  --index .tracemap-jvm/index.sqlite \
  --contract-delta samples/contract-deltas/jvm-modern.order-status.json \
  --out .tracemap-jvm/impact-report.md
```

Minimum useful input is a Git checkout with a concrete commit SHA and at least one supported source/config/build file. Java semantic analysis uses JDK compiler APIs where possible; Kotlin MVP support is syntax fallback only.

## Validation

Run JVM unit/integration tests:

```bash
JAVA_HOME=/opt/homebrew/opt/openjdk@21/libexec/openjdk.jdk/Contents/Home gradle -p src/jvm test
```

Run the local JVM sample smoke:

```bash
JAVA_HOME=/opt/homebrew/opt/openjdk@21/libexec/openjdk.jdk/Contents/Home gradle -p src/jvm installDist
src/jvm/build/install/tracemap-jvm/bin/tracemap-jvm scan --repo samples/jvm-modern-sample --out /tmp/tracemap-jvm-modern
dotnet run --project src/dotnet/TraceMap.Cli -- reduce \
  --index /tmp/tracemap-jvm-modern/index.sqlite \
  --contract-delta samples/contract-deltas/jvm-modern.order-status.json \
  --out /tmp/tracemap-jvm-modern-impact.md
```

Expected high-signal sample checks:

- `Level1SemanticAnalysis` and `buildStatus = "Succeeded"`.
- one `HttpRouteBinding`: `GET /api/orders/{id}` to `com.example.orders.OrderController.getOrder`.
- populated `call_edges`, `object_creations`, and `argument_flows` tables.
- SQL/config/Jackson/JPA integration facts.
- reducer finding `DefiniteImpact` for `OrderResponse.status`.

Pinned public JVM smoke repos are documented in [Validation guide](../../docs/VALIDATION.md):

- `sourcegraph/scip-java` at `825463cb15d540d45c680593aad1f634330435cf`
- `spring-projects/spring-petclinic` at `a2c2ef994340d3970eb6db51247456a51bb161f8`
- `square/okio` at `cad7ff1057307142149b1a28dfcb49117e89b0d3`

Run all pinned public smoke fixtures:

```bash
scripts/smoke-open-source-repos.sh /tmp/tracemap-oss-cache /tmp/tracemap-oss-smoke
```
