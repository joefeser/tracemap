# Public Demo Before/After Fixtures

These fixtures are small synthetic C# projects used by `scripts/demo-public.sh`.
They exist only to produce deterministic static before/after evidence for the
public demo. The `after` variant adds a public API route and changes the SQL
query shape for a generic orders example.

The fixtures do not claim runtime behavior, deployment reachability, schema
validity, compatibility, package impact, or release approval.
