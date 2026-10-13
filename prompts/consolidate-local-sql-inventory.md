# Consolidate the existing local SQL evidence

Use only the existing retained native path reports and the already verified
admitted application binary from the completed literal recovery. Do not start
another discovery round, rescan, build the application, execute application code,
connect to a database, change source files, commit, push or export private inputs.
Building this public diagnostic is permitted. Keep all private content local.

1. Confirm this checkout contains repeatable `--paths` support in
   `scripts/diagnostics/RecoverSqlCommandText/README.md`. If absent, stop with
   `capability-missing`; do not implement a replacement on this machine.
2. Reuse the exact native JSON files from the completed recovery, not summaries
   or grouped handoffs. Use the recorded admitted binary digest, not a fresh hash
   as a substitute for provenance. If a required input is missing or its identity
   cannot be reconciled, report that specific blocker; do not search broadly.
3. Build the public diagnostic and run it once, repeating `--paths` for each root:

```text
dotnet build scripts/diagnostics/RecoverSqlCommandText/RecoverSqlCommandText.csproj -warnaserror
dotnet scripts/diagnostics/RecoverSqlCommandText/bin/Debug/net10.0/RecoverSqlCommandText.dll --paths <root-1-native.json> --paths <root-2-native.json> --paths <root-3-native.json> --assembly <previously-verified-admitted.dll> --expected-sha256 <recorded-admitted-digest> --out <fresh-private-inventory.md> --allow-local-command-text
```

4. Verify every selected input appears, including empty roots. Verify each
   original truncation flag and reason remains unchanged. Constant matches do
   not clear cycle/frontier limits. Review per-input native provenance and
   coverage; do not upgrade historical evidence to current-build proof.
   If the human has already supplied an authorized local contract catalog in
   `sql-contract-catalog.v1` format, optionally append
   `--contract-catalog <that-local-file>` to the command. Do not query a database,
   infer parameter types or fabricate a catalog. If no catalog exists, continue
   without it and report `contract-input-missing` as the next concrete input needed.
   Catalog matches are operator-supplied candidates, not verified schema or
   connection bindings. Duplicate exact entries remain ambiguous. Keep its
   contents and digest local, just like recovered literals.
5. Review the local rows as migration leads. Repeated constants across roots are
   not unique procedure counts. Symbolic values stay unresolved. No schema match,
   procedure existence, parameter contract, connection, transaction or runtime use
   is established by this utility. Do not invent these fields or label the
   inventory complete. No private database/schema collection is requested here.

Return only human-approved aggregate counts: inputs processed, retained rows,
constant-match rows, symbolic/unavailable rows, failures, and per-root truncation
flags/reason categories. State whether current-build identity remains unestablished
and that schema contracts/migration decisions remain pending. Never return private
names, paths, SQL, literals, hashes, binary content or the inventory itself. The
actual inventory stays on this machine for the human migration review.
