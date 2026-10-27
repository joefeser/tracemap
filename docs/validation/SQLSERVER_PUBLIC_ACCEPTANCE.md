# Finite public SQL acceptance checkpoint

This package stacks on Field reads; it is not a promise to implement all SQL.
It supplies one local offline replay before another work-machine round trip.
The exact source/helper/parameter/contract/consumer links are reported separately.

## One command after a clean committed build

```sh
dotnet build src/dotnet/TraceMap.Cli/TraceMap.Cli.csproj -c Debug -warnaserror
python3 scripts/dnn-sql-validation/public_acceptance.py --fetch-public-dnn --output /tmp/sql-public-acceptance-new
python3 -m unittest discover -s scripts/validation -p test_sql_public_acceptance.py -v
```

Use a new external output directory. The runner performs fresh native parameter,
reader, helper and Field smokes sequentially, then compares **the same**
`WithParameter` execution fact through independent parameter and reader bindings
in C# and VB. Four pre-authored variants per language are good (match/match),
parameter type mismatch (mismatch/match), renamed result (match/mismatch), and
absent exact identity (unknown/unknown). This is eight pairs, not eight DNN routes.
The direct route needs no helper. Other helper routes prove consumer legs only;
their bindings cannot substitute for parameter bindings.

The retained matrices cover missing/renamed columns, unknown wrappers, ambiguous
identity and channel separation, without widening their previous contracts.
The command explicitly fetches the licensed pinned DNN complete-file slice,
does **not** build or execute DNN, and checks the existing exact partial scan.
There are no Docker/SSH/SQL/runtime calls. Ordinary NuGet restore and the explicit
public GitHub fetch may use the network. No private-input mode is provided.

`public-acceptance.json` is a local evidence packet with exact generator, input,
source, tool and child-receipt hashes. Native facts/reports keep their provenance.
Source/build/receipt integrity is not authentication or deployment correspondence.
The packet remains partial, with no aggregate clean verdict. Interrupted/failed
output is preserved; no successful packet is written. Retry at a new destination.

## Required legs and what is actually linked

| Leg | Same-execution synthetic route | Real DNN GetRoleGroups |
| --- | --- | --- |
| Caller/helper | direct method, no helper needed | source-inspected wrapper chain, not bound |
| Command/parameters | exact factId and parameter digest | external PetaPoco command unavailable |
| Contract identity | explicit four-part synthetic identity | owner/object qualifier unknown |
| Admitted contract | independent public declarations | original install file not admitted |
| Consumer | same factId, reader digest, Name access | CBO/IHydratable binding unavailable |
| Static/runtime link | NOT RUN in this offline packet | NOT RUN |

## Prioritized consolidated gaps

1. **Required before claiming this real DNN operation:** prove concrete provider
   command identity and wrapper forwarding; admit exact owner/qualifier and the
   original SQL contract; prove hydration-to-reader ownership. Current package
   tests these as four explicit blockers rather than silently normalizing names.
   The smallest first capability is an independently evidenced concrete reader
   binding across the external wrapper boundary. There is no authorization here
   to implement unrestricted PetaPoco or DNN runtime.
2. **Required before private SQL acceptance:** supply the selected operation's
   permitted schema/contract and explicit identity mapping once, together with
   source/receipt references. Missing schema remains a real missing input, not a
   request to rerun an unchanged source scan. Match only supported shapes; retain
   unsupported gaps. Use the consolidated prompt below.
3. **Required final integration gate:** parent completion, child divergence check,
   final full Debug `-warnaserror` and any explicitly coordinated live regression.
   This package must not contend with the parent's running suite/container.
4. **Optional, not automatic next phases:** arbitrary ORM, joins, dynamic SQL,
   multi-level helper inference, conversions/nullability, DataColumn overloads,
   later results, whole-site or migration readiness. None is silently added to
   the finite checkpoint merely because SQL has more features.

Runtime observations from the existing linked-provider harness remain separate;
they do not establish runtime behavior for these same-execution fixture methods.
Issues #908 and #870 remain open for their wider scope.

Later handoff: [validate-sql-evidence-consolidated.md](../../prompts/validate-sql-evidence-consolidated.md).
