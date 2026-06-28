# Site TraceMap Tools Swift Evidence Safety Story Requirements

Status: not-started
Readiness: backlog
Public claim level: shipped

## Objective

Create a site story for Swift evidence safety: public-safe output boundaries,
rule IDs, evidence tiers, hashed sensitive identifiers, no raw secrets/local
paths by default, and explicit reduced-coverage gaps.

Proof anchor: PR #425, merge commit
`e8813daaf763e277e7c5d88a2c0b2ad0b570f25a`.

## Safe Public Claim

TraceMap tells readers what Swift evidence it can prove, what tier that evidence
has, and what it cannot prove.

## Requirements

- The story must emphasize no public conclusion without evidence.
- The copy must describe evidence safety without claiming TraceMap sanitizes all
  possible user-authored public content.
- The copy must avoid raw source snippets, raw SQL, secrets, local absolute
  paths, raw remotes, credentials, and stored values.
- The story must connect Swift evidence safety to the site-wide claim ledger and
  public claim levels.
- The story must make reduced coverage and unsupported Swift features visible.

## Acceptance Criteria

- Future public copy explains Swift evidence safety in plain language.
- The story links to site claim guardrails, limitations, or roadmap where useful.
- Site validation passes after implementation.
