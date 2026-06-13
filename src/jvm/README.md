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
