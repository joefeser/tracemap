# Finite corpus and acceptance matrix (authored before extraction)

Each language compiles a caller DLL referencing a separate wrapper DLL against
signed .NET Framework 4.8 reference metadata. Compilation is not execution.

| Case in both languages | Parameter leg | Fill/return leg | Consumer leg |
| --- | --- | --- | --- |
| Direct | fresh SqlParameter(name,type,size), exact Add attachment | fresh DataSet filled and returned | local column read |
| Forwarded | wrapper SqlParameter argument attached, caller constructor separately evidenced | same | local read; cross-method substitution must be explicit |
| FieldObject | custom object constructor stores fields, wrapper reads fields into SqlParameter | same | local read; field forwarding initially blocked |
| PropertyObject | custom object constructor/setter/getter forwarding | same | local read; property flow initially blocked |
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
