# Inspect the exact Save framework boundary

Five-minute TOTAL wall-clock limit. This is static inspection of one existing
framework method, not another application scan or missing-nested-call diagnosis.

Established evidence: M3's Tier1 CallEdge targets M4 exactly in the same source
index, and that edge is already in the diagnostic graph. M4's graph node uses
property-access evidence as its representative; this does not make its method
declaration or call connection absent. The two recognized source DB operations
belong to an unrelated M5 chain. Do not try to connect them to Save.

Goal: follow M4's call through the inherited framework receiver to the first
recognized database API, or report the exact unresolved static boundary.

1. Read the existing local source witness and the existing retained call facts
   for M4's framework invocation. Establish the exact receiver type, declaring
   type, method signature and assembly identity from compiler/metadata evidence.
   A method named ExecProc_Scalar alone is not sufficient identification.
2. Use ONLY the framework DLL already staged in the approved compilation
   context, or its already-authorized matching source. Check the existing
   recorded SHA-256 against that file before inspecting it. Do not search for
   substitute DLLs, change the roster, or claim original-build equivalence.
3. Inspect that exact method body using an already-installed static metadata/IL
   reader or decompiler. Never invoke the method, load the application for
   execution, connect to SQL, or execute an assembly to inspect it. If no static
   reader is available, stop and name the missing tool; do not install one.
4. Follow only direct statically identified calls necessary to reach the first
   database API, with a maximum of four method bodies including the initial
   framework method. Resolve overloads from exact metadata signatures. For
   virtual/interface/delegate/reflection dispatch, retain the declared target
   and stop unless an explicit static receiver witness establishes the target.
5. For a recognized API, record its exact declaring assembly/type/signature,
   call instruction and method-body evidence locally. Distinguish direct API
   invocation from constructing a command or setting CommandText. Observe any
   CommandType assignment if present; do not infer it from a wrapper name.
   Do not publish private SQL, strings, paths, hashes or identifiers.
6. Compare ONLY those exact method identities with the existing combined index
   and diagnostic graph. Report whether the framework invocation, framework
   body, outgoing call and DB terminal are each retained, absent, or excluded
   by an explicit admission reason. Do not regenerate artifacts or bypass an
   admission gate. If an artifact cannot answer, label it unknown.

Return a compact sanitized chain (M4 -> F1 -> F2 -> API, as supported), an
evidence table for each hop (source/IL/retained fact/graph), the first missing
layer and its recorded reason, and elapsed time. Keep exact private evidence
local. Include a minimal PUBLIC-SAFE synthetic shape that preserves the actual
dispatch and call structure, without proprietary method bodies or literals.

Possible outcomes must stay distinct:
- A standard API call is established but excluded from retained graph evidence.
- A standard API call is established and retained; terminal projection or path
  selection needs investigation against its exact fact identity.
- The wrapper stops at unresolved dispatch, missing body/reference or the bound.
- The inspected body contains no DB API in the bounded chain; this is not proof
  that the application does not use a database.

No build, tests, scan, combine, handler requery, SDK changes, source edits,
context preparation, new downloads, assembly-provenance promotion, broad DLL
inventory, application execution, HTTP, or SQL execution. No follow-on work
after the bound. Do not reopen the disproven M3-to-M4 connectivity hypothesis.
