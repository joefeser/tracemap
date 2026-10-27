# Consolidated compiled-wrapper evidence handoff

Work on the public corpus in `samples/sql-compiled-wrapper-evidence`. Read AGENTS,
the spec under `.kiro/specs/sql-compiled-wrapper-evidence`, its implementation state,
`docs/validation/SQL_COMPILED_WRAPPER_EVIDENCE.md`, current IL command/value rules,
and source SQL parameter/DataSet/helper contracts before editing.

This branch has bounded static object-path evidence, not SQL-A acceptance. Its finite matrix has
20 entry cases, two local Tier3 SQL-shape candidates, 10 static object-path candidates and 10 unknowns.
Do not turn expected unknowns passing a smoke into a clean application verdict.
Do not request private inputs or a work-machine retry to fix missing capabilities.

First verify the exact branch head and existing validation record. Reproduce the
focused tests and compile-only native matrix. Preserve every existing source and
IL comparison contract, refusal, provenance field and budget. Never invoke fixture
methods, SQL providers, containers, a database or deployed application.

The bounded closed object protocol is implemented for this finite corpus. Verify
each leg without widening the claims:

1. Actual encoded custom constructor stores and corresponding field/getter loads,
   exact unique caller/callee/type identities, positional argument substitution,
   assignment spans as IL locations, and binary/provenance binding. No name-based
   wrapper exceptions or source-derived assumptions about a compiled body.
2. Forward a fresh parameter object and returned DataSet through the exact call
   instance; refuse alias/escape/mutation, recursion, virtual ambiguity, unsupported
   control flow and competing assembly/body candidates. Keep IL at Tier3.
3. Verify only the admitted fresh-empty Merge/field consumer relation. Incoming data is
   not destination data; preserve unknown existing destination schema, schema
   union, mutation and aliases. Never claim row/value/type/null/runtime behavior.
4. Keep contract comparison **unproven**: the compiled path is not a legacy source
   parameter/result binding. An independent admitted contract and explicit binding
   to the same operation/execution/consumer are still required before matches.

Expected positive roots in each language: Direct, Forwarded, FieldObject,
PropertyObject and MergeFresh. Mutated, Escaped, Branch, MergeExisting and
MergeMutated remain unknown. MergeFresh has distinct filled/consumer allocation
IDs and `fresh-empty-schema-union-candidate`, never original-result identity.

Separate remaining blockers: complex DDL bodies with locals, joins, computed
projections and ordering remain unsupported by existing admission. Explicit
server/database/schema/procedure identity and source/binary/deployed correspondence
remain owner inputs. A later finite schema-admission/comparison design needs its
own scope; do not implement a general parser or silently relabel manual schema as
independent synthetic declarations. Case-only names remain unknown. Do not request
another private run merely to rediscover these known capability gaps.

Use both existing C# and VB cases and their mutation/escape/branch/Merge controls.
Add independent negatives for wrong assembly/key, stale hashes, missing/ambiguous
bodies, changed constructor/store/load, limits and unsupported destinations before
claiming any newly supported case. Do not remove unsupported cases or change their
expectations merely to make the matrix green. Extend rule limitations and retain
exact generator and bounded-input hashes on every new derived artifact.

Run focused matrix/version/scope/bounds first, then one frozen full Debug suite with
`-warnaserror`, relevant retained offline and pinned checks sequentially. No duplicate
test infrastructure, full DNN runtime, general SQL parser, join inference or new ORM
support. Push only the bounded validated branch when authorized; no PR, merge, ACK
policy change or review spend. Final return: exact head, supported legs, still-missing
legs, unchanged denominators, gate failures/reruns, and the smallest next action.

Return one consolidated matrix: exact tool/binary head, every finite leg's
supported/unknown status, hashes, tiers/rules, contract origin, gaps and smallest
remaining capability/input. No private inputs or work-machine acceptance are
authorized by this local prompt. Owner schema/identity inputs and private fit are
separate from the passing public synthetic corpus.
