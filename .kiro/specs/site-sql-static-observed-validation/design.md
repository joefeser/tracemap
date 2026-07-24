# Site SQL Static/Observed Validation Design

Add `/sql/operator-handoff/validation/` as a sibling to the static proof packet.
The page uses the existing TraceMap site system and a three-lane model: static
repository evidence, validator-produced categorical observations, and human
decision authority. No new public asset or runtime behavior is required.

Validation includes a focused route validator, negative safety tests, discovery
and sitemap checks, full site build/test/validate, and desktop/mobile browser
sanity.
