# Implementation State

Status: implemented
Last verified: 2026-06-15
Branch: codex/site-nav-validation
Source of truth: working tree
Public claim level: hidden

## Summary

This spec adds a generated-site validation guard for top-navigation drift. The
validator now checks every generated HTML file for the canonical `top-nav`
links, ignoring page-local `aria-current` markers while comparing link text and
targets.

The generated blog layout now includes `Capabilities` and `Docs`, matching the
hand-authored pages after the capabilities matrix work.

## Validation

- `npm test`
- `npm run validate`
- Browser sanity check for `/blog/why-tracemap-exists/` at 390px width:
  generated blog navigation includes `Capabilities` and `Docs`, no horizontal
  overflow, no console errors.
