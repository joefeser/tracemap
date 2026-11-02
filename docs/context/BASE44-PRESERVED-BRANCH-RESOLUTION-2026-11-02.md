# Base44 preserved-branch resolution (#923 / #924 / #925) — decisions and salvage prompt

Date: 2026-11-02
Owner decision (Joe Feser, 2026-11-02): delete the two superseded branches; salvage the stash via a normal PR.
Evidence comments: [#923](https://github.com/joefeser/tracemap/issues/923#issuecomment-6584578020), [#924](https://github.com/joefeser/tracemap/issues/924#issuecomment-6584578555), [#925](https://github.com/joefeser/tracemap/issues/925#issuecomment-6584579021).
Research basis: branch tips verified against `origin/dev` (`d6186b1d`) on 2026-11-02; every claim below points at a file/line or a diff. Exact branch names appear in the linked issues; they are kept out of this file because the tree is subject to the public-clean gate.

## Decisions

| Issue | Branch tip | Verdict | Action |
| --- | --- | --- | --- |
| #923 | `c75ae8cb3` (2026-09-21) | SUPERSEDED — dev has both changes from earlier commits (`81e38cfd0`, `f65b90d5a`) and enforces more (packet validation `Base44EvidencePacket.ts:1007-1023`) | Deleted 2026-11-02 |
| #924 | `982dbccfa` (2026-09-11) | SUPERSEDED — schema hunk byte-identical to dev's `81e38cfd0`; dev test even has the branch's extra schema-const assertion (`Base44Evidence.test.ts:1784-1788`); branch base predates `gapSchemaVersion`/`gaps` schema work, so porting would regress | Deleted 2026-11-02 |
| #925 | `f3394e781` (2026-10-12, `codex/preserved-base44-stash-20261012`, stash preserve of uncommitted dev work) | STILL NEEDED (partial) — 3 of 4 behavior groups never landed on dev | KEEP until the salvage PR below lands; then delete |

## PROMPT — salvage task for the next tracemap session (resolves #925)

Goal: port the still-missing behaviors from the preserved stash `codex/preserved-base44-stash-20261012` (`f3394e781`, base `29d5fc56`) onto current dev as a reviewed PR. Do NOT cherry-pick the stash — one of its hunks is superseded and will conflict.

Steps:

1. Branch off current `origin/dev`.
2. Extract the stash's own diff for reference: `git diff 29d5fc56..f3394e781` (read-only; the branch stays on origin until this PR lands).
3. Port these hunks, adapted to current dev (all paths relative to repo root):
   - `src/typescript/src/extractors/Base44EvidenceExtractor.ts`
     - Add `"logUserInApp"` to `primitiveRoots` (dev currently ends at `"Analytics", "AppLogs"`, ~lines 12-16). Without it, a bare `base44.logUserInApp(...)` emits NO fact: `addSdkCall` early-returns when no chain element is a primitive root.
     - Compute `projectHasBase44Authority` at the top of `extractBase44Facts` (frontend package authority version OR any SDK authority roots OR any function-entry/base44 path), thread it through `visit`, and widen the `Base44HttpTarget` gate (~line 142) so a bare `fetch(...)` in SDK-free files is recorded (dynamic, `Tier4Unknown`) when the project has Base44 authority. Ordinary non-Base44 repos must stay unclassified.
     - Extend `isFunctionEntry` (~line 3188) to also match flat `functions/<name>.{js,jsx,ts,tsx}`; change the `functionName` fallback (~line 3197) to basename-minus-extension; emit `Base44CustomerBoundary` for function entries (dev's boundary gate ~line 74 currently only knows `base44/(functions|entities)/...`).
   - `docs/BASE44_STATIC_EVIDENCE_ADAPTER.md`
     - Port the stash's two paragraphs: flat-handler recognition (nested `functions/` files are NOT promoted by path alone; `handlerKind` stays `unknown` without `Deno.serve`) and the `logUserInApp`/bare-fetch obligations paragraph (HTTP-target fact = obligation to resolve, not proof of a provider).
   - `src/typescript/tests/Base44Evidence.test.ts`
     - Port both stash tests: "records a bound direct logUserInApp call and a standalone frontend fetch" and "recognizes top-level functions/<name>.ts handlers without promoting nested helpers" (asserts `functions/helpers/format.ts` gets NO surface fact).
4. Do NOT port the stash's `ensureSelectorEvidenceAuthorityFacts` hunk (the UI-semantics required-set addition and the call reorder). Dev's `6c7c2c9c3` (2026-10-15) already landed that in refined, commented form (`Base44EvidenceExtractor.ts:3104-3114`, tested in `Base44UiInputSemantics.test.ts:363-364`). Cherry-picking the stash wholesale will conflict there and would regress the comment/refinement.
5. Dev-side compatibility note: the new fact shapes need no packet-validation changes — `Base44EvidencePacket.ts:308-317` categorizes `Base44FunctionSurface` → `function`, `Base44HttpTarget` → `http-integration`, `Base44CustomerBoundary` by `surfaceKind` generically.
6. Verify:
   - `cd src/typescript && npx vitest run tests/Base44Evidence.test.ts tests/Base44UiInputSemantics.test.ts` — both ported tests pass, no existing assertions break.
   - `npx vitest run` (full suite) before opening the PR.
7. PR to `dev` referencing #925. After merge, delete `codex/preserved-base44-stash-20261012` and close #925.

Decision caveat to carry into the PR description: flat `functions/<name>.ts` recognition widens function-surface recall beyond the `base44/functions/` layout. Nothing on dev records a decision to reject it (no doc/spec/triage trace), but it was abandoned uncommitted work — the PR description should state the widening explicitly so reviewer sign-off is informed.

## Optional follow-up (residual from #923, non-blocking)

Dev's `docs/BASE44_SDK_CALLSITE_IDENTITY_V1.md` documents the evidence kinds (`:36-37`) and manifest↔lock agreement (`:45-51`) but not the standalone `Base44SdkImport` authority facts emitted by `packageAuthorityFacts()` (`Base44EvidenceExtractor.ts:3566`), and no test asserts those facts' `requestedPackage`/`importKind` directly (coverage is via `sdkIdentityJson` evidence assertions at `Base44Evidence.test.ts:1597-1601` and the validation-rejection test at `:1737`). If desired: port the one-paragraph doc addition and the 15-line fact-level assertion from branch tip `c75ae8cb3` as a small PR. Cosmetic only — the runtime contract is already enforced.
