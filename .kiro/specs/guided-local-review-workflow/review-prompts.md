# Guided Local Review Workflow Review Prompts

## Spec Review

Review `.kiro/specs/guided-local-review-workflow/` for issue #666.

Verify:

1. Distribution is not selected before reproducible no-checkout package smoke
   evidence exists.
2. The workflow reuses existing scanner, Web Forms packet, and explorer
   producers rather than copying evidence semantics.
3. `scan-execution-receipt.v1` remains operational diagnostic evidence and is
   not promoted into `CodeFact` conclusions.
4. Repository, commit, scan, snapshot, schema, claim-level, and input-hash
   conflicts fail closed.
5. Output collision, symlink/reparse, staging, publication, and cleanup
   boundaries cannot delete or overwrite user content.
6. Portable results cannot contain local absolute paths, remotes, source,
   snippets, SQL, config values, URLs, credentials, exception text, or private
   infrastructure names.
7. Partial and failed analysis remain visible and failed build is never called
   clean.
8. #667 remains the owner of Web Forms packet explorer compatibility.
9. Windows, macOS, and Linux claims require matching smoke evidence.
10. No LLM, embedding, vector, hosted, upload, or hidden telemetry capability
    enters TraceMap core.

Return only concrete contradictions, missing acceptance criteria, unsafe
authority expansion, deterministic identity defects, or test gaps. Classify
findings P1/P2/P3 and cite the exact file/section.

## Implementation Review

Review the #666 implementation against this spec and current repository state.

Prioritize:

- package/install/run/remove behavior outside the checkout;
- version schema truthfulness and provenance;
- exact reuse of standalone producer services;
- source/output mutation safety;
- pre/post artifact hash continuity;
- deterministic IDs and ordering;
- typed failures, last safe state, cleanup, and next action;
- public/private value suppression;
- standalone-versus-guided artifact parity;
- cross-platform claims backed by execution evidence.

Do not request new extraction, runtime claims, hosted services, restore by
default, or Web Forms explorer parsing owned by #667.
