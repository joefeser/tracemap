# Design

## Decision

Normalize the two SaveAsText line-break escapes at the quoted-scalar parsing
boundary. This gives all protected string properties the same in-memory text
that Access declared while preserving every other backslash sequence.

The existing SQL projector and UI binding composer then consume the normalized
value. Qualified wildcard sources reuse only fields already attached to their
resolved static dependency. No new field identities or output ordinals are
invented.

## Coverage

Control bindings may become complete when a normalized inline query resolves to
one dependency and the named control source resolves uniquely in that
dependency's catalog. The inline wildcard record-source binding remains partial
unless its complete ordered output projection is independently established.

## Privacy and non-claims

Raw properties and SQL remain in memory only. Standard facts retain hashes,
lengths, stable evidence identities, coverage, rule IDs, provenance, and
limitations. The result does not prove query execution, returned columns,
rendering, row values, correctness, or production use.
