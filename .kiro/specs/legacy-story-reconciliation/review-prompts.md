# Legacy Story Reconciliation Review Prompt

Review `.kiro/specs/legacy-story-reconciliation/` for merge readiness.

Focus on:

- whether the scope is a reconciliation cleanup rather than a new feature;
- whether the coexistence test is sufficient to catch WCF/Remoting/legacy-data
  merge regressions;
- whether stale task/status wording is cleaned without hiding real deferred
  follow-ups;
- whether the validation plan is appropriate.

Return blocking issues, important non-blocking issues, missing tests, and
whether this is ready to implement or merge.
