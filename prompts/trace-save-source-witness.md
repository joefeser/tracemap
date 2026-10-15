# Trace one real Save operation — source inspection only

Ten-minute TOTAL wall-clock limit. Start the timer before inspection. Stop at
the deadline and return the last established boundary, not a guessed chain.

Goal: establish the actual source call chain from the exact Save handler used
in the latest retained-handler query to its first database operation. We need
one concrete source witness before changing the extractor again.

Use the private application's source at the commit recorded in the existing
scan. Verify that the inspected files match that commit. Preserve the current
checkout and any local edits; do not switch branches, reset, stash, or modify
files. If matching source cannot be verified, stop with that precise blocker.
Do not confuse the application's commit with the TraceMap tool commit.

Read the existing handler-query receipt and diagnostic graph to identify the
exact handler and the caller containing the two retained source operations
(execute-candidate and bulk-write-boundary). These are identity anchors, not
proof that the handler reaches that caller.

Inspect only the selected handler and the method bodies needed to follow its
calls. Use targeted searches to locate declarations and resolve overloads.
Trace direct calls using their actual receiver, argument and declaration
evidence. For interface, delegate, reflection or late-bound dispatch, require
an explicit source witness for the target; otherwise stop at that boundary.
Do not join methods merely because names, files or business descriptions match.

Determine whether the handler:

1. Calls the method containing the two retained database operations;
2. Reaches a different database operation; or
3. Stops at a specific statically unresolved boundary.

Compare the established source chain with the ALREADY RETAINED diagnostic graph.
Identify the first evidenced source call that has no corresponding retained
connection, if one exists. Preserve distinctions between absent call facts,
facts without graph edges, and edges excluded by traversal. If the existing
artifacts cannot distinguish these, say which evidence is unavailable rather
than guessing. Tier3 evidence alone is not a reason to reject a connection.
External compiled-IL admission gaps alone do not explain missing source facts.

Return only:

- Elapsed time and source-commit verification result.
- A sanitized handler-to-boundary chain using consistent anonymous method labels.
- Which of the three outcomes above is supported.
- The first missing retained connection, or an explicit statement that none was
  established; include the VB syntax shape and dispatch category.
- A minimal anonymized synthetic VB shape suitable for a regression test, if
  supported by the inspected source. Preserve the relevant syntax, receiver,
  overload and control-flow shape; do not copy private identifiers or literals.
- Confirmed findings versus unresolved hypotheses, and the exact stopping point.

Keep actual private symbols, file paths, line references, commits, source and
artifacts local. Maintain those local references so the finding can be checked;
return only the sanitized chain and synthetic shape here. Do not commit or
upload private material.

No build, tests, scan, combine, requery, context preparation, DLL search,
configuration change, application execution, SQL or HTTP. Do not alter source,
receipts, reference rosters, budgets or original reports. No unrelated issue
work and no follow-on investigation after the time limit.

Success is a source-backed explanation of this one operation's path or its
precise unresolved boundary. It is not runtime reachability, whole-application
completeness, a clean build, or proof that no database behavior exists.
