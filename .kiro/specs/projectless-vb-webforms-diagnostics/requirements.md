# Requirements

1. Preserve a public synthetic projectless VB Web Forms fixture without
   creating a project or solution file.
2. Prove whether direct calls inside resolved projectless VB handlers are
   retained separately from handlers containing only UI state changes.
3. Report terminal-free event-chain gaps according to the bounded traversal
   observation: truncated, downstream observed without a supported terminal,
   or no backend evidence observed.
4. Keep classifications evidence-relative and document that they do not prove
   runtime execution or absence.
5. Every retained VB syntax call edge must carry an explicit coverage label so
   packet admission does not create one provenance gap per valid call.
