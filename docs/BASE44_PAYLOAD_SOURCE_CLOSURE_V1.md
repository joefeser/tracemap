# Base44 Payload Source Closure v1

Extractor `base44-evidence/0.11.0` adds four narrow source-flow proofs to the
existing payload-shape v2 descriptor. These proofs reduce false gaps without
changing the descriptor schema or treating runtime-open fields as statically
complete.

## React dependency termination

A mutation-hook binding referenced in the dependency array of a source-proven
React `useCallback`, `useEffect`, `useLayoutEffect`, or `useMemo` call is a
terminating identity reference. It is not a payload mutation or escape. The
proof requires an exact runtime import from `react`, the dependency array in
the second argument, and an unshadowed imported binding. Local functions with
the same name do not qualify.

Other uses of a hook binding remain typed escapes. Computed members, extracted
methods, unknown calls, assignments, and arbitrary arrays are not termination
contexts.

## Finite computed-key domains

A computed object key may become a finite set of named fields only when every
reference to the owning local callback is accounted for and every call supplies
a statically finite string value. Supported values are string literals,
conditional expressions whose two branches are finite, and parameters whose
own finite local callers can be followed recursively.

The callback may be wrapped only by source-proven React `useCallback` and
Lodash `debounce` imports. Wrapper aliases, imported names, and local call
bindings must be unshadowed. A missing, spread, runtime-open, escaped, or
recursive argument leaves the original `dynamic-computed-property` gap. When
more than one name is possible, each field is conditional.

## Closed React object state

The extractor may follow the first element of a two-element `useState` binding
when all of these conditions hold:

- `useState` is an unshadowed runtime import from `react`;
- the initial value is independently analyzable;
- every setter reference is a direct one-argument call or a proven React
  dependency reference;
- every direct replacement is independently analyzable; and
- a functional updater has one plain parameter, returns one object, and uses
  that parameter exactly once as an object spread without another escape.

All possible state shapes are unioned. A field is unconditional only when it
is unconditional in every modeled state. Conflicting outer kinds and any
unresolved setter flow remain blockers. A function merely named `useState`
does not establish state authority.

## Mutually exclusive branches

A reference in the opposite arm of the same `if/else` or conditional
expression cannot execute before the payload call in the selected arm. Such a
reference does not create an alias-escape gap for that callsite. Sequential
references, conditions, loops, switch flow, missing `else` branches, and calls
outside the correlated arms retain their existing conservative behavior.

## Nonclaims

These are same-source executable-syntax proofs. They do not cross component or
module callback boundaries, infer a field from its name, model arbitrary
higher-order functions, or treat an unused mutation callback as a valid
payload. Cross-component form payloads, ref-mediated callback chains, dynamic
keys whose full caller graph is not represented in the source file, and hooks
without a `.mutate*` callsite remain typed blockers.

An outer object with a finite mutation call graph but runtime-open child fields
retains `runtime-deferred-object-fields` and the exact Docker obligation
`entity-open-object-fields:docker-write-readback-cleanup`. This is not static
completeness.
