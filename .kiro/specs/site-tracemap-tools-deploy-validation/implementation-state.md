# Implementation State

Status: implemented
Last verified: 2026-06-15
Branch: codex/site-amplify-validation
Source of truth: working tree

## Summary

This spec wires the generated-site validation command into deployment and pull
request automation. Amplify now runs `npm run validate`, which builds `site/dist`
and validates the generated output before publishing. GitHub Actions now runs a
site validation workflow on site-related pull requests and `main` pushes.

The deployment output remains static files under `site/dist`. No backend,
runtime service, database, auth, crawler, or dependency was added.

## Validation

- `npm test`
- `npm run validate`
- `ruby -e 'require "yaml"; YAML.load_file("../amplify.yml"); YAML.load_file("../.github/workflows/site-validation.yml"); puts "YAML OK"'`

## Follow-ups

- Consider adding branch protection for the `Site validation` workflow after it
  has run cleanly on a few site PRs.
