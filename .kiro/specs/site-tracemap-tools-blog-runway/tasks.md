# Tasks

- [x] 1. Decide the initial blog structure.
  - Choose URL shape, for example `/blog/why-tracemap-exists/` and
    `/blog/building-tracemap-with-codex-kiro-qodo/`.
  - Decide whether blog pages stay plain static HTML or whether a tiny template
    step is needed before adding more repeated page chrome.

- [x] 2. Draft the origin story article.
  - Working title: `Why TraceMap Exists`.
  - Explain the problem of being asked to manually index unfamiliar systems for
    dependency and contract-impact review.
  - Keep the story professional: no employer, consultant, team, or individual
    blame.
  - Connect the story to deterministic evidence, coverage labels, and reviewer
    confidence.

- [ ] 3. Draft the build-process article.
  - Working title: `Building TraceMap With Codex, Kiro, and Qodo`.
  - Describe Codex worktrees, Kiro specs, implementation-state files, PR review
    loops, and Qodo feedback.
  - Credit Qodo as a useful PR review agent without implying formal partnership
    or endorsement.
  - Explain why the process matters for a deterministic analysis tool.

- [x] 4. Add blog pages and metadata.
  - Add canonical URLs, Open Graph metadata, and sitemap entries.
  - Add a blog index or link from the homepage/discovery pages if appropriate.

- [x] 5. Draft the manager value article.
  - Working title: `What TraceMap Solves for Engineering Teams`.
  - Explain manual indexing, review handoff, auditability, and coverage-aware
    decisions for a higher-level manager audience.
  - Keep the article grounded in static evidence and avoid claims about runtime
    proof, production usage, or release approval.

- [x] 6. Validate.
  - Run `npm run build` from `site/`.
  - Smoke the new URLs locally.
  - Check desktop and mobile browser layout.
