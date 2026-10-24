# Prepare a bounded H4 lifecycle validation

You are working locally with the private application and its accepted TraceMap
evidence. Prepare the next H4 test. SQL work is deferred. This prompt authorizes
read-only preparation, not application execution or a new scan.

## What we are testing

H4 is the accepted HTTP path beginning in a WebForms control's
`CreateChildControls()` override. Existing evidence establishes a static path
from that override to an HTTP terminal. A separate Tier2Structural
`framework-lifecycle-candidate` describes the override relationship; it is not
a traversed call edge or proof that the actual page invokes the override.

The next question is narrow: under one approved page, identity, and application
state, does the real framework lifecycle enter this override, which guards or
cache conditions apply, and does execution reach the HTTP callsite? Actual
request transmission is a separate, optional test.

Do not implement a general WebForms framework model. Do not revisit SQL, claim
application-wide coverage, or upgrade static evidence tiers from runtime notes.

## Phase 1 — prepare now, without running the application

Spend at most 30 minutes on targeted local inspection. Read repository guidance
first. Use the accepted private inventory to resolve the H4 override and its
host page/control registration; do not guess the host from a method name.
If the inventory is ambiguous, report the exact missing selector and stop.

1. Record the current source revision and accepted receipt/index references in
   a private Markdown note. Preserve existing baselines byte-for-byte. Reuse
   recorded provenance; do not claim fresh verification unless performed with
   the committed verifier and the matching preserved binaries.
2. Inspect only the selected override, immediate hosting/wiring evidence, and
   relevant guards/cache logic. Distinguish facts from assumptions. Identify
   which test states can be selected without editing business data or config.
3. Identify an existing approved nonproduction environment and the installed
   compatible WebForms runtime/debugger. Modern .NET alone does not supply
   classic System.Web hosting. Do not install tools or build/run the application.
4. Prepare exact operator steps for observing natural page-driven entry,
   framework call stack, guard outcomes, cache hit/miss, and callsite reachability.
   Prefer existing debugger breakpoints and counters; do not modify source or
   create an ad-hoc diagnostic executable. If committed tooling is insufficient,
   return `CAPABILITY_MISSING` with the smallest public-safe requirement.
5. Identify side effects that page navigation could trigger before reaching the
   override, including database writes, email, and other outbound calls. This
   is a safety inventory, not SQL analysis. A breakpoint at the override does
   not make navigation safe.

Produce a runnable operator plan, not just a recommendation to add a breakpoint.
Keep actual paths, symbols, URLs, configuration, source revisions, and identities
in the private note. Do not copy credentials, cookies, headers, connection strings,
payloads, or source snippets into the note or return message.

## Phase 2 — explicit operator approval required

Stop after preparation unless the operator explicitly approves all of:

- The named isolated nonproduction environment and application build/source.
- The exact page action, test identity, and permitted state setup.
- Controls preventing unintended database, email, and external HTTP side effects.
- Whether only lifecycle/callsite observation is authorized, or a request to a
  named controlled test endpoint is also authorized.

This prompt is not that approval. Do not start a server, build the application,
launch a browser session against it, change configuration, clear shared caches,
or send requests while preparing. If isolation cannot be demonstrated, return
`BLOCKED_ENVIRONMENT`; do not silently fall back to production.

After explicit approval, use the prepared plan for only these bounded cases:

| Case | Required observation | Claim boundary |
| --- | --- | --- |
| Eligible fresh test session/cache miss | Natural host-page lifecycle entry, framework caller, guard outcomes, and whether the target callsite is reached | Callsite reached is not a request sent |
| Repeat in the same test session | Independently observe entry and cache behavior | Do not assume a cache hit prevents lifecycle entry |
| One safely selectable guard-negative state | Record entry, selected guard outcome, and whether execution stops before the callsite | Absence in this case does not prove global unreachability |
| Optional controlled endpoint case | Correlate one approved test request with the observed callsite and method | No claim about production endpoint success |

Do not force the override by calling it directly or invoking
`EnsureChildControls()` from a test driver and describe that as natural page
dispatch. A synthetic harness can test instrumentation, but cannot establish
that the actual application's hosting page invokes H4. If ordinary page use
does not reach the override, report the observed state and unresolved wiring;
do not force execution to obtain a passing result.

Use only minimal local observations: timestamps, test correlation IDs, boolean
guard/cache outcomes, and a private debugger call-stack reference. Avoid HAR,
packet dumps, response bodies, secrets, and real customer identifiers. Any
separately approved temporary instrumentation must be reversible and confined
to a disposable application copy. Do not commit or publish it automatically.

## Evidence and output

Keep runtime observations separate from native TraceMap facts and receipts.
Preserve truncation and all existing gaps. Do not rewrite the static graph,
claim exhaustive HTTP coverage, or infer SQL, migration, or runtime correctness.
Use a private Markdown note, not a new machine-readable artifact format.

Return only a sanitized matrix with:

- Preparation status: `PLAN_READY`, `BLOCKED_INPUT`, `BLOCKED_ENVIRONMENT`, or
  `CAPABILITY_MISSING`, with the smallest next action.
- Runtime execution: `NOT_AUTHORIZED`, `AUTHORIZED_NOT_RUN`, or `OBSERVED`.
- Evidence origin: actual application, synthetic-only, or not run.
- Natural framework entry, guard/cache behavior, callsite reached, and controlled
  request observed: separate results, each `YES`, `NO_IN_TEST_CASE`, or `NOT_TESTED`.
- Static Tier1 path and separate Tier2 lifecycle candidate: retained unchanged.
- Existing truncation/gaps: preserved; no exhaustive coverage claim.
- Exact approval or missing prerequisite needed next, without private values.

No Git mutations, publication, new scans, retained requeries, full test suites,
SQL work, or application execution are authorized by Phase 1. If an approval
question is needed, provide the completed preparation and ask one concise,
specific question naming the proposed bounded test in the private session.
