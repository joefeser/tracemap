# SQL Validation Plan Template Design

The checked-in files are parser-valid examples rather than token-substitution
templates. Operators copy them outside the repository and replace the synthetic
identity, time, target, and identifier values before use. This lets tests pass
the exact files through the production parser without encouraging real target
details in source control.

The plans stay separate because a source observation and archive-target
observation have different owners, contexts, and failure meanings. The packet
provides an ordered responsibility matrix and preserves v1's four unsupported
assertions as `not-run` rather than inventing evidence.

No new runtime code or probe SQL is required.
