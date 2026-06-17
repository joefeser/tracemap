# JVM Indexer Implementation State

Status: implemented-mvp

Branch/PR:

- Implemented across the JVM adapter feature work already merged into `dev`.

Scope Implemented:

- `src/jvm` Gradle package and `tracemap-jvm scan` CLI.
- Deterministic repo metadata, inventory, manifest, facts, SQLite, and report output.
- Maven, Gradle, package/dependency, config, and analysis-gap extraction.
- Java semantic extraction with syntax fallback.
- Kotlin syntax fallback and reduced coverage boundaries.
- JVM symbol identity, call edges, object creations, argument flows, local aliases, and symbol relationships where static evidence supports them.
- Spring/JAX-RS route facts, JDBC/JPA/SQL resource facts, Jackson serializer member facts, config-use facts, and sample contract deltas.
- Downstream `.NET` export/combine/reduce compatibility for JVM indexes.

Validation:

- JVM validation requires a Java runtime. If Java is missing, check Homebrew first and stop to ask only if Homebrew cannot provide the tool.
- Minimum local validation:
  - `cd src/jvm && ./gradlew test installDist`
  - `src/jvm/build/install/tracemap-jvm/bin/tracemap-jvm scan --repo samples/jvm-modern-sample --out <tmp>/jvm-modern-sample`
- JVM smoke expectations and pinned public sample repos are documented in `docs/VALIDATION.md`.

Open Follow-Ups:

- Kotlin semantic extraction.
- Bytecode/classpath-only dependency extraction.
- Additional JVM framework detectors and deeper ORM/query support.
- Derived parameter-forward edges beyond direct argument facts.
- Public OSS smoke automation beyond local/manual validation.
