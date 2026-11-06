# Verify inline text and bulk-copy evidence once

Use the existing private workspace, reports and known sample methods. Keep all
private source, identifiers, SQL, hashes and detailed receipts outside public Git.
Read AGENTS.md. This is evidence reconciliation, not application conversion.

Public controls are `SqlWrapperRouteTests` and the independently authored
`samples/sql-database-matrix/wrapper-{website,library}` write fixtures. They pin:

- Incremental literal UPDATE through a simple separate compiled wrapper has a
  symbolic command-text composition with hashed literal operands.
- A custom-parameter ArrayList wrapper loop can retain the execution route while
  ordinary command binding reports `IlCommandTextBindingUnavailable`.
- Transactional DELETE/bulk-copy retains API calls and the destination setter's
  hashed operand. A bulk path endpoint alone does not prove destination binding,
  transaction identity, commit success, trigger effects or runtime execution.

This branch adds fixtures/tests/docs, not a scanner capability change. Do not
expect a new scan to fix an existing refusal automatically.

Perform one bounded review using existing evidence first:

1. Bind the two owner-identified methods to their exact binary/module/token and
   source observation. Do not choose roots merely by display-name substring.
2. For the inline UPDATE, inspect retained call operands and command-binding
   candidates/gaps. Follow the SQL argument through admitted wrapper bodies using
   the existing selected-root path reader (`scripts/sql-wrapper-report.py`) if
   the index supports it. Distinguish composition recovery from parameter binding.
3. For bulk-copy, locate the exact destination setter call and its linked
   `ManagedIlCallValuesObserved` fact via `ilCallFactId`. Inspect `argumentOrigins`
   for a constant-string hash. Match the owner's locally observed literal using
   its UTF-16LE SHA-256 and length (`str:<length>:<digest>`), without publishing it.
   Missing cleartext is expected and is not evidence of missing hashed extraction.
4. Inspect DELETE binding and transaction calls separately. Do not infer shared
   transaction identity or successful execution from nearby calls.

If the needed facts were not enabled in the retained scan, one fresh scan using
the saved setup and unchanged admitted inputs/limits is authorized. Preserve the
prior run. Do not rerun the wizard, widen scope, increase limits or retry a refusal.
No database/application execution, source edits, installations or deploys.

Stop after that review (and at most one scan). Produce a short sanitized table:
execution route; text composition; parameter binding; bulk destination operand;
bulk destination projection; transaction identity. For each give present,
explicit-gap or not-established, the rule/schema and local fact linkage. Correct
earlier claims when hashed evidence was overlooked. State any real gap precisely;
do not propose another scan as the default next step.
