# Review selected Web Forms source against TraceMap evidence

This is a separately authorized, narrowly scoped source-assisted review. The
temporary selected-source directory contains `selection-manifest.json` and at
most twelve aliased source files. Read only those files and the receipted
TraceMap evidence directories supplied to this session.

## Boundaries

- Treat TraceMap evidence as the provenance and coverage baseline. Source may
  answer a recorded question but does not retroactively make a missing fact
  compiler-resolved.
- Do not search outside the supplied directories, run the application, edit
  files, connect to external systems, or recover identities hidden by aliases.
- Cite selected source by its `source-NNN.ext` alias and line span. Do not quote
  private paths, identifiers, URLs, SQL, configuration values, credentials, or
  more than the minimum source text needed to explain a shape.
- Describe `.ashx` artifacts as HTTP handlers unless retained contract evidence
  establishes a stronger classification.
- Distinguish a confirmed source construct from runtime behavior. Static source
  does not prove that a branch, handler, query, service, or client behavior runs.

## Deliverable

Return the complete Markdown assessment on stdout with these sections:

1. **Evidence and source receipt** — compatible provenance, evidence coverage,
   selected aliases, byte count, and any mismatch.
2. **Questions answered by selected source** — link each answer to the prior
   page/cohort question and cite both evidence and aliased source.
3. **Source behavior map** — server lifecycle, event handlers, generated HTML or
   JavaScript, data shaping, HTTP/service calls, and control state that are
   directly visible. Keep unavailable categories explicit.
4. **Extractor candidates** — deterministic patterns TraceMap could add, with
   the exact syntax shape and false-positive boundary. Do not propose name-only
   classification.
5. **Remaining unknowns** — runtime, contract, ownership, external dependency,
   dynamic-dispatch, and truncated-path questions still not established.

Do not rewrite the whole application assessment, estimate migration effort,
declare conversion safe, or recommend production changes. End with the single
smallest additional source or evidence selection that would answer the most
important remaining question. Do not return only a summary or plan-file path.
