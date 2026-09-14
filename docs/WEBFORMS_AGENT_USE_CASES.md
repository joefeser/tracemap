# Web Forms Evidence Use Cases

TraceMap evidence can help a human reviewer or a separate authorized agent
understand a legacy Web Forms application without asking that agent to begin by
reading hundreds of source files. The evidence remains deterministic static
analysis; any agent summary is a downstream interpretation and must preserve
the evidence boundaries.

## Good uses

### Portfolio triage

Use the application workbench and alias-only outlier report to find pages with
high static activity, evidence ceilings, unresolved handlers, incomplete
chains, framework-specific controls, or unusually sparse evidence. The result
is a review queue, not an effort estimate.

### Technology-family discovery

Use compiler-resolved declaring type and assembly metadata to distinguish
application calls, framework calls, supported Telerik evidence, other
third-party calls, and unresolved calls. A family label describes retained
static evidence; it does not prove runtime receiver type or dispatch.

### Conversion assessment

Ask what capabilities a page appears to require in a target architecture:
event handling, state, validation, data access, navigation, server controls,
client behavior, or external dependencies. Require the answer to cite page
aliases and retained evidence, distinguish facts from inferences, and list what
must still be checked in source or at runtime.

### Evidence-gap review

Aggregate typed gaps before inspecting individual pages. A gap repeated on
most pages is more likely a missing extractor or coverage-label problem than
hundreds of independent application defects. Preserve that uncertainty until a
rule-backed extractor or authorized review resolves it.

### Human-review handoff

Use the WITS overlay to record reviewer verdicts, migration dispositions,
capability labels, and comments without mutating scanner evidence. Decisions
remain a separately validated overlay.

## Bad uses

Do not ask an agent to:

- declare a page safe to migrate from static evidence alone;
- invent business rules or runtime behavior;
- treat call projections as runtime call counts;
- treat an unresolved handler as dead code;
- silently fill evidence gaps from naming conventions;
- infer source identity from aliases, counts, or hashes; or
- approve work, releases, or production changes.

## Prompt patterns

Effective prompts ask for a bounded deliverable and an evidence ledger. Useful
requests include:

- “Group the aliased pages into review cohorts using only the supplied static
  evidence. Explain each grouping and list its limitations.”
- “Identify which pages have compiler-resolved Telerik evidence and which only
  have unresolved or structural hints. Do not infer a Telerik dependency from
  names alone.”
- “For each selected page, separate retained facts, reasoned hypotheses, and
  owner questions. Cite the page alias and evidence category for every fact.”
- “Find systemic extraction gaps that repeat across the corpus. Recommend
  extractor work separately from application remediation.”
- “Draft a conversion discovery checklist. Do not estimate effort until the
  listed runtime, data-contract, state, and ownership questions are answered.”

For a ready-to-paste work-machine prompt and commands, see [Web Forms agent
handoff](WEBFORMS_AGENT_HANDOFF.md).

