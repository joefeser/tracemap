# SQL lifecycle origin contract — what a converter may generate, and what it must not

This contract tells a conversion AI what the walk's entry candidates prove
about each value, so generated code carries exactly the proven structure
and marks exactly the unproven values. It is the acceptance bar for the
conversion attempt: **never guess — every unbound value becomes a named
TODO carrying the recorded assumption, traceable to its origin label.**

The walk proves structure only: which constructions, guards, and
assignments execute on normal-completion paths, with provenance for every
value. It never proves runtime values, execution, rows, or schema
ancestry. The origin table below is the full vocabulary.

## Origin table

| Origin | What the walk proves | The converter MAY generate | The converter MUST mark TODO |
| --- | --- | --- | --- |
| `exact` (literal from the IL) | the literal value itself | the literal, inline | nothing |
| `input:N` (root argument) | the slot's position and declared type only | a named parameter with that type, flowing through the proven structure | the runtime value it will carry |
| `opaque-initializer` (opt-in) | a conditional normal-return and tracked-heap noninterference assumption for the root's scalar initialization | the initializer's shape | which branch the runtime takes |
| `computed-string` (unbound string) | the shape of the computation (concatenation, slicing, trace strings) | the same computation in generated code | the runtime value it produces |
| `computed-int` (unbound integer) | the computation's shape (parses, lengths, array bounds) | the same computation | the runtime value it produces |
| `dbnull` | the `DBNull.Value` constant | `DBNull.Value` | nothing |
| `unbound` (sizes and other claimed-unknown values) | that the value is runtime-dependent — proven unbound, never guessed | a placeholder | the value, as a named TODO |
| `connection-state` (unbound State observation) | that the connection's runtime `State` was read and compared against a compiled constant — one bounded alternative per outcome; the value and its transitions are never observed | the guard as a real branch carrying per-alternative structure | which branch the runtime takes |
| `exceptional-path` (catch regions) | that the `Try`/`Catch`/`Finally` structure exists; uncaught exceptional paths terminate their alternative without executing catch continuations | the `Try`/`Finally` structure as proven | the catch body's contents — `ex.Message`/`ex.InnerException` are exception-variable-dependent unbound strings |
| `allocation:N` / `object-*` | the allocation's identity and provenance chain (which constructor, from which receiver) | the same construction and flow | runtime identity equivalence with the original |

## Rules for generated code

1. **Structure is evidence; values are labels.** Every control-flow shape
   the candidates show (guards, forks, loop bounds, call order) is
   generated as proven. Every value is generated from its origin row.
2. **Forks stay forks.** Where the walk produced state alternatives (per
   `stateControlPolicy`), the generated code keeps the guard as a real
   branch — it does not pick a side.
3. **Unbound values become named TODOs.** Format:
   `// TODO(lifecycle-origin: <kind>): <recorded assumption>` — the
   assumption restates what the origin row says is unproven. One TODO per
   unknown; never a silent default.
4. **No invented SQL.** Command text arrives `exact` or stays a TODO.
   Parameter names arrive exact (constructor-proven) or stay TODOs.
   Sizes arrive exact, `unbound`, or as a recorded assumption.
5. **No structure the walk does not show.** No added exception handling,
   no added disposal, no "helpful" refactors. The diff against the
   original is structure-preserving by construction.
6. **The standing limitations ride along.** Tier3 conditional lineage
   candidate; successful normal-completion paths only; no row proof; no
   schema ancestry; ArrayList support exact for single complete
   enumerations of at most 16 inspected custom objects.

## What this contract is not

It is not a claim that the converted code is correct — it is the contract
that makes the conversion **reviewable in one pass**: a reviewer checks
structure against the candidates and TODOs against the origin labels, and
nothing else. If a generated line needs information the origin table does
not carry, that line is wrong by definition.
