# Design

Use an intentionally projectless Web Site fixture with `CodeFile`, inline
jQuery, server controls, code-behind, and an `App_Code` helper. Scan it through
the normal engine and build the Web Forms modernization packet.

Do not synthesize build metadata. The scanner's syntax fallback is the source
of evidence when compiler loading is unavailable.

Keep `eventChains[].classification` compatible. Refine only generated packet
gap classifications by mapping the already-retained traversal stop state to a
specific diagnostic. Every generated gap remains tied to the packet rule,
commit SHA, and supporting fact IDs.
