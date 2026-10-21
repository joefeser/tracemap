# Accept the first retained H1 HTTP report

Use the existing private baseline inventory on the work machine. Validate the
new `webforms-review requery-handler --surface http-client` capability on
`codex/http-evidence-regressions`. This is a first HTTP report, not a comparison
against an earlier HTTP report. The accepted database reports remain untouched.

Budget: 15 minutes, one CLI build and one HTTP requery. No scan, combine,
preflight, application execution, network request, database access, source edits,
checkpoint rewriting or new diagnostic helper. Stop on missing inputs, mismatches
or unsupported behavior; do not improvise another workflow.

If this machine has an unpushed implementation commit, preserve it. Do not merge,
rebase, reset or push it as part of acceptance. Ask the operator to prepare a clean
separate checkout/worktree of the maintainer-supplied remote revision, then run
this prompt there using the original inventory's artifact references. The tool
checkout and retained evidence directory do not need to be the same directory.

1. Record tool branch, SHA and dirty state. Preserve unrelated untracked files.
   Stop if tracked changes affect the build. Read the inventory and verify the
   retained index hash against its receipt. Keep private references local.
2. Resolve H1's actual HTTP entry handler from its recorded event-binding fact,
   caller edge and HTTP-operation fact. Verify exact symbol joins in the same
   index. Do NOT reuse the database Save handler merely because its query receipt
   supplies the known bundle and limits. If the CLI's handler-name selector cannot
   uniquely select H1, stop with AMBIGUOUS ROOT; do not substitute another handler.
3. Build `src/dotnet/TraceMap.Cli/TraceMap.Cli.csproj` once using
   `dotnet build src/dotnet/TraceMap.Cli/TraceMap.Cli.csproj --nologo -c Debug`.
   Record exit and warnings. Use that checkout's built CLI.
4. Run the following command shape with locally resolved values and the recorded
   accepted depth. The bundle is the completed report or recovery directory, not
   the SQLite filename. The destination must be new, outside Git and outside the
   retained run and all inputs. Other traversal limits come from the retained run
   configuration; verify they match the recorded limits, without increasing them.

   ```text
   dotnet <built-tracemap.dll> webforms-review requery-handler --run <retained-run> --bundle <retained-bundle> --handler <verified-H1-handler-name> --out <new-private-output> --surface http-client --max-depth <recorded-depth>
   ```

5. Verify the new receipt records HTTP scope, no database surface-name filter,
   the exact H1 root, expected limits, current generator SHA-256 and the verified
   combined-index SHA-256. Preserve the native bounded-input hash. Report context
   rejection as CONTEXT REJECTED; never bypass its guard.
6. Open the native grouped handoff and require at least one actual retained path
   from H1's handler to its expected HTTP operation. Verify ordered caller hops,
   supporting facts, rule IDs, tiers and locations against the original H1 facts.
   Check the event binding separately as the entry anchor: a handler-root query
   need not include the binding as a traversed call edge. A successful exit,
   receipt or `PathEnumerationPerformed=true` alone is NOT path acceptance.
7. Preserve coverage/provenance gaps and truncation. A stronger terminal must not
   upgrade weaker path evidence. If no path is returned, distinguish intact H1
   facts from a reporting/join/search gap; do not declare HTTP absent or unused.

Write one new private Markdown note with exact commands and evidence references.
Return only public tool SHA, build status, elapsed time, hash-verification result,
H1 root verified yes/no, path present yes/no, chain/variant counts, evidence tiers,
truncation, and FIRST HTTP BASELINE ACCEPTED / PATH NOT ESTABLISHED / AMBIGUOUS ROOT /
INPUT MISMATCH / CONTEXT REJECTED / TIME LIMIT REACHED. Keep private paths, hashes,
symbols, URLs, payloads and artifacts local. No uploads or commits from this run.
