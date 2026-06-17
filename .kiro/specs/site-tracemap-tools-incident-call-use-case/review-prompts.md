# Site TraceMap Tools Incident Call Use Case Review Prompts

Status: implemented
Readiness: ready-for-review
Public claim level: concept

Use these prompts when reviewing a future implementation of this spec.

1. Does `/incident-call/` render with `Public claim level: concept` and the
   shared principle, "No public conclusion without evidence"?
2. Does the page stay within static dependency evidence and avoid runtime,
   production traffic, endpoint performance, outage cause, APM replacement,
   release safety, and operational safety claims?
3. Do proof-path links to `/proof-paths/`, `/validation/`, `/docs/`,
   `/limitations/`, and `/demo/result/` exist or have documented gaps?
4. Are raw `facts.ndjson`, `index.sqlite`, `.tracemap` paths, analyzer logs,
   raw snippets, raw SQL, config values, secrets, local absolute paths, raw
   remotes, generated scan directories, and private sample identities absent
   from public output?
5. Do build, validation, route metadata, discovery metadata, sitemap coverage,
   and desktop/mobile sanity checks support the implementation before tasks are
   marked complete?
