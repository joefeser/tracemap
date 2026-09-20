# Historical Web Forms / VB Investigation Branches — 2026-09-19

Status: historical research inventory; not an implementation plan

Two clean local investigation worktrees are intentionally retained because
their tips are not contained in `dev`. Their machine-local paths are omitted
from this shareable record:

| Branch | Tip | Useful retained research |
| --- | --- | --- |
| `codex/vb-webforms-battle-test` | `cc9c61c2` | Projectless VB receiver diagnostics, overload and inherited-field experiments, multi-language identity requirements, battle-test runbook, and private-safe aggregate validation patterns. |
| `codex/vb-webforms-receiver-bridge` | `cf9515e1` | Focused regression comparison tooling, receiver-gap hardening experiments, and terminal-delta diagnostics associated with closed PR #753. |

Do not merge either branch wholesale. Both branches contain broad, overlapping
changes based on older `dev` states. Current Web Forms terminal reachability is
defined by PR #770 at `046d3c41`.

Material worth carrying forward must be handled as a new, reviewable change:

1. reproduce the behavior with a public or synthetic fixture;
2. identify the exact rule, evidence tier, identity boundary, and limitation;
3. compare against current `dev`, including terminal inventory and path-detail
   truncation independently;
4. copy only the minimized fixture, diagnostic, or documented design decision;
5. run current focused and full validation; and
6. leave private paths, source identities, and receipts out of shareable
   artifacts.

The branches are research inputs for projectless receiver gaps and the compiled
.NET fixture matrix. They are not evidence that current `dev` lacks the merged
terminal-reachability behavior.
