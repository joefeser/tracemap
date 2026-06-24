# Legacy Data Model ORM Mapping Completion Review Prompts

Use the repo-local Kiro wrapper:

```bash
node scripts/kiro-review.mjs --phase legacy-data-model-orm-mapping-completion --kind spec --model claude-opus-4.8 --fresh --timeout-ms 600000 --save-review-text
node scripts/kiro-review.mjs --phase legacy-data-model-orm-mapping-completion --kind spec --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text
```

Reviewer focus:

- Does the spec stay deterministic/static and avoid runtime ORM/database claims?
- Are unsupported descriptor and mapping-shape gaps explicit enough?
- Are safe normalized descriptors and hash-only handling sufficient?
- Is the first implementation slice crisp, or does it accidentally include all
  downstream report/export work?
- Are generated-code linkage boundaries clear when MSBuild/Roslyn fails?
- Are public-safe fixture and validation expectations concrete?
- Are raw SQL/config/secrets/local paths/remotes/private labels forbidden in all
  relevant outputs?

After patches, run one bounded re-review:

```bash
node scripts/kiro-review.mjs --phase legacy-data-model-orm-mapping-completion --kind re-review --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text
```
