# Codex handoff — repo state, invariants, and standing work (2026-11-03)

## Purpose and editing rules

The owner is between reviews for a period; agents operate within the
autonomy boundaries below. This document is the current-state layer ON
TOP OF `AGENTS.md` — read AGENTS.md first; it remains the primary
contract. The owner may edit this file at any time;
treat the latest `dev` version as the truth. Everything an arriving
agent needs to work safely is either here or pointed to from here.

Sanitized vocabulary used throughout (the discipline is mandatory, see
invariant 1): "the framework assembly," "the custom parameter
structure," "the root execute helper," "the two lifecycle roots," "the
owner's work machine." Never private names, SQL, binaries, or paths.

## Board state at handoff

### Merged (dev tip `b468af7db` at writing)

- **PR #922** — the whole evidence arc: the bounded walk completing both
  roots, the origin contract, the measured two-attempt conversion, five
  review rounds (18 findings fixed, 3 dispositioned), the purge of
  consumed prompts and the privacy scrub.
- **PR #929** — census-driven keying dedup (fork markers key by carried
  provenance; literal-pair folds by digest) and the 256-path / 16-site
  capacity. The four-input public mirror completes again (217
  alternatives) and the private-shape fixture completes with candidates.
  Tool `il-body-evidence/0.2.68` / policy `v69`.

### Open

- **PR #930** — test temp-directory sweep (a 196 GiB leak on the
  owner's Mac; also cleans the remote gate runner). MERGEABLE after a
  rebase onto dev; awaiting the owner's merge button. Merges are
  owner-only unless instructed otherwise.

### Branches on origin

| Branch | Purpose |
| --- | --- |
| `dev`, `main` | integration; promotion to `main` is owner-only |
| `codex/sql-postmerge-verify` | carried the v44 verification prompt (consumed — report received) |
| `pending-docs-to-verify` | the origin-purge manifest (tip SHAs of deleted branches) |
| `fix/test-temp-sweep` | PR #930 |
| the preserved base44 stash branch | #925 only — #923/#924's branches are deleted (the issues carry the evidence) |

### Issue queue

- **#923 / #924 — SUPERSEDED** (branches deleted; evidence in the
  issue comments). Close when convenient.
- **#925 — salvage AUTHORIZED** (the owner's decision is recorded in
  the resolution doc): port three behaviors + tests per
  `docs/context/BASE44-PRESERVED-BRANCH-RESOLUTION-2026-11-02.md` and
  open the PR; do NOT cherry-pick the one superseded hunk. The
  delete-branch/close-issue TAIL step runs only AFTER the owner merges
  that PR — branch deletion stays owner-gated.
- **#926 / #927 / #928 — dispositioned corners**, each with its bounded
  fix written in the issue: covariant array stores; `brtrue` on a boxed
  Boolean tests the reference; digest-mismatch folding under
  `Option Compare Text`. Assigned to Sol per the owner.
- **#931 — ROOT-B `lifecycle-work-limit`** (from v44-001): the
  DIAGNOSTIC half is DONE — PR #933 shipped the work-stop census,
  per-root budget ledgers, and canonical root ordering; see
  `docs/validation/SQL_LIFECYCLE_BUDGET_DIAGNOSTICS.md`. What remains
  is the owner's decision: the production work-budget raise itself
  (2M → 4M, with a memory check first — v44 peaked ~991 MiB of the
  2048 MiB cap at 2M) and any runner-isolation change. #931 stays open
  for that decision.

## Verification state (v44-001, the latest private scan)

- **ROOT-A completed: 216 alternatives with entry candidates — this is
  the conversion's input.** ROOT-B hit the work fence (#931). The
  candidates are MORE honest than the earlier generation's (v42: 96
  alternatives missing the per-string-input emptiness dimension; the
  old comparison bug silently skipped it).
- **Sanitized-file vintage note:** the sanitized conversion POC on the
  owner's work machine is labeled ROOT-B and cites the v42 scan — its
  fork inventory (6 decisions, no emptiness forks) predates the
  comparison fixes. The structure and origin-kind vocabulary remain
  valid; for CURRENT fork counts regenerate the annotated input from
  ROOT-A's v44-001 output (216 alternatives, emptiness decisions keyed
  per input). Expected delta when regenerating: more decisions per
  root, never new origin kinds.
- No further private scans without an owner-dispatched prompt. B2
  findings (surprises) go to the issue list, never a private fix loop.

## The conversion contract status

- `docs/validation/SQL_LIFECYCLE_ORIGIN_CONTRACT.md` is the standing
  contract: the origin table (exact / input / opaque-initializer /
  computed-string / computed-int / dbnull / unbound / connection-state
  / exceptional-path / allocation) decides generate-vs-TODO per value.
- Attempts 1–2 are complete with a zero-diff owner review (recorded in
  the capability map). The next conversion runs against the completed
  root's candidates under the same protocol: never guess — every
  unbound value is a named TODO carrying its recorded assumption;
  forks stay real branches; success is one diff review.
- Owner's attempt budget: 1–2 per method family, then stop and document.

## Autonomy boundaries

### Safe autonomously

- #926/#927/#928 bounded fixes (as written in the issues): C#-only
  fixtures, pin-first, focused lane + full remote gate, PR to `dev`
  (leave merging to the owner).
- #925 salvage implementation + PR exactly as its resolution doc
  specifies (the deletion tail step awaits the owner's merge).
- The diagnostic half of #931 (census on work-limit).
- upgrade-authority work per its own queue (`ua` = PRs #903 + #905;
  its bug reports arrive from downstream consumers).
- Docs, issue triage, test-infra.

### Owner-only

- Merging PRs; dev→main promotion; deleting preserved branches.
- Private scans (work machine) — only via owner-dispatched prompts.
- Budget raises of any kind (paths, sites, work, memory caps) — census
  decisions per the v30 precedent.

## Hard invariants (each one enforced, or learned the hard way)

1. **Privacy.** No private identifiers in repo artifacts — sanitized
   vocabulary only (top of this doc). The Private Path Guard scans the
   whole tree on push; a pre-commit clean-gate additionally refuses one
   employer-adjacent branch-name literal in tracked files (reference
   those branches by issue number + tip SHA, never by name). Sanitized
   scan reports reach the owner's chat, never the repo.
2. **Fixtures.** Public synthetic only — never private code, names,
   SQL, or binaries. New lifecycle fixtures are **C#-only**: the VB
   sample assemblies sit at the shared native body-budget edge, and ANY
   VB fixture growth silently drops tail bodies and breaks unrelated
   pinned tests (this fired twice).
3. **Versions bump with the batch.** `ScannerVersions.IlBodyEvidenceExtractor`
   and `PolicyVersion` move IN the batch commit. They once silently
   stayed three versions behind the docs; the commit pin is otherwise
   the only anchor.
4. **Pin-first, and pins must bite.** Reproduce a stop with a public
   fixture BEFORE admitting anything. A pin asserts outcome shapes —
   terminations, fork counts, decision receipts — not just green
   completion. Verify the pin bites: disable the fix, watch it fail.
5. **No silent folds.** The twelve review-round findings were all one
   class: a concrete fold picking a side without a gap. When in doubt,
   refuse by name; the refusal IS the deliverable.
6. **AGENTS.md core**: no conclusion without evidence, no rule without
   documented limitations, failed build is not a clean repo.

## Validation protocol (exact)

### Focused lane (local, ~6 min)

```
cd src/dotnet && dotnet test tests/TraceMap.Tests -c Debug \
  -p:TreatWarningsAsErrors=true --nologo \
  --filter "FullyQualifiedName~IlSql|FullyQualifiedName~ScanDiagnosticProgress|FullyQualifiedName~Report"
```

### Remote full gate (amd-5950x, WSL Ubuntu-24.04, ~13 min)

**Push the branch first** — the gate clones from GitHub, then fetches a
bundle over the top; an unpushed branch is exit 128.

```
git bundle create /tmp/gate.bundle HEAD
scp /tmp/gate.bundle amd-5950x:gate.bundle
scp gate.sh amd-5950x:gate.sh
ssh amd-5950x "wsl -d Ubuntu-24.04 -- bash <windows-home-wsl-path>/gate.sh"
```

`scp` targets the runner's Windows profile home (its OpenSSH default),
which WSL sees under its `/mnt/c` mount — substitute the runner's
WSL-visible home path for `<windows-home-wsl-path>`; never spell the
real path in tracked files (the Private Path Guard matches the path
substring even inside placeholders — this doc's first push failed it).

`gate.sh` (recreate if missing; set BRANCH to the branch under test;
requires a CLEAN working tree — the bundle carries committed work only):

```bash
set -e
cd "$HOME"
rm -rf tracemap-gate
git clone -q --branch "$BRANCH" --single-branch https://github.com/joefeser/tracemap tracemap-gate
cd tracemap-gate
git fetch -q <windows-home-wsl-path>/gate.bundle HEAD
git checkout -q FETCH_HEAD
echo "HEAD: $(git rev-parse --short HEAD)"
~/.dotnet/dotnet build src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj -c Debug -warnaserror --nologo > $HOME/gate.log 2>&1
~/.dotnet/dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --no-build -c Debug -warnaserror --logger "console;verbosity=normal" >> $HOME/gate.log 2>&1
echo "exit=$?" >> $HOME/gate.log
```

Then read the tail of `$HOME/gate.log` for counts — **exit zero alone
proves nothing** (count the passes). Log to `$HOME`, not `/tmp`: WSL's
/tmp is ext4-on-vdisk, not tmpfs. The runner is single-user: serialize
gates — concurrent runs clobber the shared checkout and log. Until PR
#930 merges, sweep the runner's temp after each gate; after #930 the
test suite sweeps itself.

### Compile-only smoke (~2 min)

```
python3 scripts/validation/sql-compiled-wrapper-smoke.py \
  --cli src/dotnet/TraceMap.Cli/bin/Debug/net10.0/tracemap.dll --out <fresh-dir>
```

## Known gotchas (each one cost real time)

- IDE/Edit-tool changes never stage themselves: `git status` after every
  `reset --soft` or squash — a squash after a soft reset silently drops
  unstaged edits (this bit PR #922's map fix).
- `perl -0pi script.pl file` silently no-ops; inline `perl -0pi -e`
  works. `\Q..\E` quotemetas the dot, so `branch.s` cannot match
  `branch's`. Scripted edits anchor-assert (count==1) + grep-verify.
- GitHub's mergeable/commit-count cache lags ~15 s after a force-push —
  poll before concluding a push failed.
- Branches cut from a long-lived checkout carry its history (a rebase
  once replayed a month of old commits). Always cut fresh off
  `origin/dev`; cherry-pick the commit you actually want.
- Fresh Linux/macOS builds promote analyzer warnings invisible on
  cached builds (xUnit2013/2031, CS1717). Test classes must be public;
  `Assert.Single(coll.Where(...))` fails xUnit2031 — use the filtering
  overload.
- Pipes through `wsl --` are eaten by cmd.exe: stage scripts via scp
  and run by path (that is why gate.sh exists).
- Identity encoding: `ldstr` literals arrive from body evidence as
  `str:len:sha256` digests; `ldtoken` operands carry `tok:type:0x…:`
  prefixes; cast operands carry `t:`. Compare bare scoped-type tails.
- Metadata tokens are per-module — the same token in two assemblies is
  two different methods. Do not conflate them across reports.
- Fork budget arithmetic: fork counts DOUBLE per activated binary
  dimension; a census family keyed `site/…` instead of a provenance
  means under-keying (independent values sharing one decision) — fix
  the key, don't raise the budget (collapse before raise, v26 lesson).

## Pointers (all on dev)

- `docs/validation/SQL_LIFECYCLE_CAPABILITY_MAP.md` — the per-batch
  public record of every admission, refusal, and boundary.
- `docs/validation/SQL_LIFECYCLE_ORIGIN_CONTRACT.md` — the conversion
  contract.
- `.kiro/specs/sql-compiled-wrapper-evidence/implementation-state.md` —
  gates, versions, oddities, the full batch trail.
- `docs/context/BASE44-PRESERVED-BRANCH-RESOLUTION-2026-11-02.md` —
  the #925 salvage prompt.
- Consumed working prompts are deliberately absent from the tree
  (purged with #922); their substance lives in the docs above, and the
  manifest-with-SHAs pattern is the audit compromise.
