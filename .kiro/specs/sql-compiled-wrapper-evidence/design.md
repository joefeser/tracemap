# Finite corpus and acceptance matrix (authored before extraction)

Each language compiles a caller DLL referencing a separate wrapper DLL against
signed .NET Framework 4.8 reference metadata. Compilation is not execution.

| Case in both languages | Parameter leg | Fill/return leg | Consumer leg |
| --- | --- | --- | --- |
| Direct | fresh SqlParameter(name,type,size), exact Add attachment | fresh DataSet filled and returned | local column read |
| Forwarded | wrapper SqlParameter argument attached, caller constructor separately evidenced | same | local read; cross-method substitution must be explicit |
| FieldObject | custom object constructor stores fields, wrapper reads fields into SqlParameter | same | local read; field forwarding initially blocked |
| PropertyObject | custom object constructor-backed property/getter forwarding | same | local read; property flow initially blocked |
| Mutated | constructor parameter then Size mutation | same | candidate must not retain stale size |
| Escaped | parameter passed to unknown helper before Add | same | unknown effects must prevent positive parameter shape |
| Branch | differing parameter size paths | same | unknown parameter shape unless exact convergence proved |
| MergeFresh | Direct wrapper result merged into freshly allocated destination field | incoming result is not destination | unknown merged schema/field alias |
| MergeExisting | Direct result merged into caller-provided destination | incoming result is not destination | unknown existing schema/schema union |
| MergeMutated | Direct result merged then destination column removed | incoming result is not destination | unknown mutation, never original-result match |

Ten entry roots per language, not ten successful end-to-end bindings. Every leg is
reported separately. Positive direct construction is a bounded configuration
candidate, not a SQL contract match, runtime parameter value or deployed route.
Source/binary correspondence and server/database/procedure identity are separate
owner-supplied inputs. SQL joins/general parsing are outside this package.

Implementation boundary: reuse independent Cecil/SRM IL agreement, existing call
value origins and command→endpoint binding. Add a sibling bounded observation only
where those primitives prove the relation. A field/object heap transfer engine is
not silently inferred from source; remaining blockers must be recorded explicitly.

Negative controls additionally cover wrong encoded assembly/member signature,
parameter alias/escape, duplicate attachments, unsupported control flow, multiple
executions/returns, body/work bounds and stale/malformed provenance. No candidate
is upgraded to Tier1 or substituted for the existing source parameter/reader/Fill
comparison bindings.

## Authorized object-path continuation denominator

Authored before running the new interpreter: Direct, Forwarded, FieldObject,
PropertyObject and MergeFresh must each yield one Tier3 static dataflow candidate
in each language (10 total). Mutated, Escaped, Branch, MergeExisting and
MergeMutated must each remain unknown (10 total). These are NOT SQL contract
matches. MergeFresh explicitly records distinct filled and consumer allocations
and a fresh-empty-schema-union-candidate relation, never original result identity.
The route follows only fully inspected unique nonvirtual bodies and refuses all
unrecognized calls, exceptional/conditional flow, object mutations and escapes.
Local aliases are usable only when every use is inspected; no uninspected alias
or preexisting field ownership is inferred. Scalar strings remain hashed.

## Table-flow continuation matrix (before extraction validation)

Preserve the ten original roots per language as their own denominator. Add a
separate TableFlow corpus: First and Second each allocate one property-backed
DataSet and two named tables, merge first-result tables from two distinct fetch
methods, then consume destination ordinal zero or one. Each must preserve the
selected execution, filled DataSet, source-table and destination-table identities.
No match or runtime ownership claim follows from this static candidate.
Conditional writes, unknown helpers, catch/finally, repeated Merge, existing
destination, out-of-range ordinals and duplicate table names must remain refused.
The separate matrix is 13 roots per language: First and Second positive;
Conditional, Mutated, UnknownHelper, RepeatedMerge, ClearMerge, Existing,
Exceptional, DuplicateName, OutOfRange, DynamicSlot and ThirdTable unknown.
The original matrix stays at ten roots per language. All candidates remain Tier3;
conditional mutation and exception-state computation are not implemented.
