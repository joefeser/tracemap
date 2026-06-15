# Implementation State

Status: implemented
Last verified: 2026-06-15
Branch: codex/site-shared-chrome
Source of truth: working tree
Public claim level: hidden

## Summary

This spec adds a shared build-time header renderer for `tracemap.tools`. Static
HTML pages still live as plain source files, but generated `dist` output now
receives the canonical header and top navigation during the build. Generated
blog pages use the same renderer.

The validator now imports the canonical navigation data from the build script,
so generation and validation share one link list instead of duplicating it.

## Validation

- `npm test`
- `npm run validate`
- Browser sanity check for `/capabilities/` at 1280px,
  `/examples/scan-packet/` at 390px, and
  `/blog/why-tracemap-exists/` at 390px: canonical navigation renders,
  `aria-current` is correct for page/section matches, no horizontal overflow,
  no console errors.
