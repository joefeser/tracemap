# C# HTTP entry inheritance boundaries

Public synthetic, compile-only .NET Framework 4.8 sample. Never host the page or
execute HTTP calls. The only URL uses the reserved example.invalid domain.

This fixture separates direct framework overrides, overrides through a source
base class, a directly declared getter, an inherited getter, and an empty page
event. Registration supports containment only, never an invented call.

Current limitations are intentional test expectations, not desired final support:

- The direct BaseWidget.OnLoad override retains a semantic framework witness.
  The source-intermediary DerivedWidget override does not.
- Ordinary C# method/property declarations in this fixture are syntax-tier;
  there is no semantic declaration to join into a lifecycle/property entry.
- Two containment relationships survive, while both markup property references
  remain gaps. The empty Page_Load does not gain a route to HTTP.

The test references this project for clean-checkout reference assembly restore.
If C# declaration support is implemented, update the expected inventory with
exact symbol, assembly and supporting-fact checks; do not simply delete the
negative assertions or turn containment into source calls.
