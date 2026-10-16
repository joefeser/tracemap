# Diagnose one complete integrated entry-path gap

Use the existing integrated scan and confirmed saved project configuration. Do
not rescan or switch projects. Confirm the scan revision, extractor versions and
source-root identity locally; if source and scan differ, report that limitation
before attributing a gap to the extractor.

Find one locally verified WebForms entry handler that reaches a reported database
or outbound HTTP operation. Inspect source locally to establish the actual source
call chain, then compare every hop with the retained method and call facts. This
is source evidence, not proof of runtime execution or a deployed URL. If no such
chain can be established, say so rather than inventing a connection.

Locate the first hop the integrated report cannot join. Determine whether the
cause is missing extraction, missing receiver/type information, ambiguous identity,
a report-join mismatch, or an explicit traversal limit. Do not guess or promote
syntax evidence to resolved evidence. Inspect the remaining hops on that same
chain for additional blockers so the public reproduction covers the whole path.

Return only an operator-approved, sanitized structural description suitable for
an independently authored synthetic fixture:

- Entry-binding mechanism.
- Method and receiver declaration shapes.
- Call syntax and argument count at each hop.
- Evidence field names and generic states present or missing, including rule IDs
  and evidence tiers where available.
- First failed join and its evidenced reason; label an undetermined cause unknown.
- Additional blockers visible along that same chain, including applicable limits.
- Expected entry-to-operation relationship and current report outcome.

Use invented identifiers and abstract structural shapes, not copied private code.
Keep private source, paths, names, SQL, values, hashes, fact identifiers and detailed
reports local. Do not modify application code, execute application methods, connect
to databases or HTTP endpoints, export patches, or modify, commit or push the public
repository. Keep historical compiled paths separate unless an admitted identity
bridge exists.

The objective is one reproducible entry -> handler -> backend -> operation chain,
not another parameter inspection. A faithful report of an unresolved chain does
not mean entry-to-operation connectivity has been delivered.
