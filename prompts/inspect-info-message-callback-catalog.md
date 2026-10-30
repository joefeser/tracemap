# Inventory every unsupported callback call from existing output — no scan

Read AGENTS.md. Inspect ONLY the existing compiled-only diagnostic artifacts and
retained public-tool metadata for the failing InfoMessage callback. Do not build,
scan, restore, install, execute private code or SQL, edit implementation, broaden
inputs, launch subagents or modify any existing output. Use local receipt paths.

The exact framework-2 delegate constructor is now admitted. The next refusal is
lifecycle-info-uninspected-call during callback inspection, at callvirt offset
100 (decimal). Both selected roots reach the same callback. The report contains
only a call identity hash; it does not identify the missing signature.

1. From the existing lifecycle rejection and retained body/call evidence, locate
   that exact callback using binary/body provenance. Alias private identifiers.
   Confirm offset, opcode, reference kind, and reader agreement; unknown if absent.
2. Return the FULL recorded TargetIdentity at the rejected call only if every
   identifier in it is public framework vocabulary. Preserve exact assembly name,
   version, culture, signing key, declaring type, member name, arity, calling
   convention, this flags, parameter identities and return identity. Do not
   normalize display strings or guess a signature from its hash. Recompute its
   UTF-8 SHA-256 locally and confirm match to the recorded rejection hash.
3. In the SAME inspected callback (at most 256 instructions and 32 call sites),
   inventory ALL distinct call/callvirt/newobj target identities against the
   actual current public IlSqlInfoMessageApis catalog. Include both admitted and
   unlisted calls. Enumerate all missing signatures now, rather than stopping at
   the first one. Preserve dispatch owner/signature differences. The catalog is
   source code, not a method-name allowlist. Framework-2 expansion is finite.
4. For unlisted public framework calls, return exact identities and offsets,
   declared argument/return types, reference kind, and retained StackShape if
   present. If private compiled callees occur, return aliases and categorical
   shape/retention only; do not return private names, code, paths or full receipts.
5. For each missing call state whether its immediate input/output originates from
   the event-message string, an XML reference, the private name list, sender, or
   another tracked reference, but only when retained metadata proves that fact.
   Mark unknown otherwise. Do not infer purity or no escape from a string return.

Return one bounded sanitized report (at most 50 lines), then STOP. Include tool
SHA, exact public signature/hash agreement for offset 100, count of total calls,
all unlisted public signatures/offsets, private-callee aliases if any, metadata
unknowns, and the complete list of remaining callback catalog gaps. If an existing
artifact lacks a required field, name the missing field and stop without a scan.
No photographs, private code, uploads or email are required. No acceptance claim.
