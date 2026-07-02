# Swift Route-Flow Smoke Requirements

Status: implemented
Public claim level: demo after generated outputs are reviewed for publication.

## Context

Swift HTTP/API client facts now carry source-local declaration context. The
existing combined `route-flow` report can use that context to show a static
client-call path from an HTTP client evidence row to the containing Swift symbol
and terminal HTTP surface.

## Requirements

1. Add a checked-in smoke script that scans `samples/swift-http-api-client-surfaces`,
   combines the index, and runs `tracemap route-flow --client-call` against a
   Swift HTTP/API client path.
2. The smoke SHALL verify Markdown and JSON route-flow artifacts are written.
3. The smoke SHALL verify entry evidence, static flow rows, a method-symbol
   bridge, a terminal HTTP surface, touched files, and touched symbols.
4. The smoke SHALL verify the route-flow output cites Swift HTTP rule evidence.
5. The smoke SHALL verify route-flow limitations continue to describe static
   evidence rather than runtime execution proof.
6. The smoke SHALL fail if raw URLs, secrets, query tokens, or private values
   from the Swift fixture appear in generated route-flow outputs.
7. Documentation SHALL explain the command and frame the result as
   coverage-relative static evidence, not runtime reachability.

