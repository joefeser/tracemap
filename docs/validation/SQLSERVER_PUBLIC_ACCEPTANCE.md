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
3. **Final integration gate (completed below):** parent completion, child
   divergence check, full Debug `-warnaserror` and coordinated live regression.
   Future implementation changes require fresh relevant validation; this record
   does not authorize contention with another suite/container.
4. **Optional, not automatic next phases:** arbitrary ORM, joins, dynamic SQL,
   multi-level helper inference, conversions/nullability, DataColumn overloads,
   later results, whole-site or migration readiness. None is silently added to
   the finite checkpoint merely because SQL has more features.

Runtime observations from the existing linked-provider harness remain separate;
they do not establish runtime behavior for these same-execution fixture methods.
Issues #908 and #870 remain open for their wider scope.

Later handoff: [validate-sql-evidence-consolidated.md](../../prompts/validate-sql-evidence-consolidated.md).

## Checkpoint at implementation head 78e8f6a4a

Fresh public replay passed: parameter **8/12/32**, reader JSON+DDL **72/20/94**,
helper **84/30/116**, Field **78/36/42**, with **70 admission refusals**. Eight
same-execution pairs matched the independently authored matrix. DNN retained
6,031 facts and the three incidental nonquery candidates; its one selected reader
route remains explicitly blocked, not validated by the synthetic controls.

The composition exposed and fixed one genuine integration defect: a result-local
initializer was incorrectly classified as a parameter-state alias/escape. Only
the exact current invocation is exempted, before conversion unwrapping. C#
0.22.21 / VB 0.10.24; ten new direct regressions. **311 focused tests** and **102
Python guards** pass. Private-path and whitespace guards pass. Parent remained
unchanged at the recorded SHA throughout this checkpoint.

Native comparison repeats are byte-identical. The outer local packet binds actual
scan/log bytes, including run-specific metadata; it is not promised identical
across fresh runs and is not a public privacy-projected export.

Full Debug, other retained smokes, pinned OSS adapters and live regression remain
pending a coordinated final slot. No SQL/container/application method ran. This
checkpoint is not merge readiness, deployment proof or private acceptance.

## Final integration at 9d486e208

Normally merged parent final `035f4890d` (docs-only delta from the initial base).
An initial full attempt exposed a stale Razor extractor-version assertion; the
owned run was cancelled, the exact pin repaired, and 96 touched tests passed.
That failed/cancelled attempt remains recorded, not silently treated as success.

The fresh frozen full Debug `-warnaserror` suite passed **5,412 tests**, with
**3 expected skips and no failures** (29m 2s). The consolidated public replay,
all remaining retained SQL smokes, pinned C#/VB OSS checks, **102 Python guards**,
private-path and whitespace checks passed sequentially. All original SQL
denominators and refusal expectations were preserved.

The existing isolated linked-provider matrix also passed **34 cases (20/6/8)**
and **54 runtime checks**, after revalidating its established host/image/resource/
ownership safeguards. Its owned container was removed and the post-run inventory
was empty. No new infrastructure, DNN runtime or private input was used.
This regression does not establish runtime behavior for the eight offline pairs.

Source, HEAD and CLI/Core/Reporting bytes stayed frozen throughout. The generated
offline packet still labels its external full/live gates pending: it does not
itself verify those runs. The spec implementation-state records their separate
receipt paths and the frozen revision. Results documentation follows in a
docs-only commit. The real DNN blockers and private-schema prerequisite remain;
DataColumn and unrestricted ORM work are optional extensions, not new gates.
