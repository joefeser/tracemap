# Native Web Forms public subprocess diagnostic

This is a public synthetic source-size benchmark, not private Windows,
ASP.NET compiler, complete-site or runtime SQL acceptance. The test generates
PE/IL explicitly and never executes those DLLs. Generator and bounded input
hashes are retained in the local `native-scale.receipt.json`; private absolute
configuration paths and their fingerprints are not reproduced here.

The final diagnostic and preflight tests passed 42/42 with no failures/skips.
Every declared page retained all four generated terminal branches by exact
symbol identity. Grouped JSON restored the retained paths; source/published
rosters and admitted run artifacts were unchanged across completed resume.

| Observed dimension | 32 pages | 256 pages |
| --- | ---: | ---: |
| Source bytes | 2,236,416 | 17,891,328 |
| Published bytes | 13,728 | 83,200 |
| Retained facts | 1,780 | 14,100 |
| Retained compiled paths | 128 | 1,024 |
| Evidence + run file bytes | 22,814,356 | 175,574,091 |
| Prepare elapsed ms | 821 | 14,382 |
| Prepare OS peak resident bytes | 100,859,904 | 124,026,880 |
| Preflight elapsed ms | 225 | 268 |
| Preflight OS peak resident bytes | 76,431,360 | 81,870,848 |
| Run elapsed ms | 6,826 | 189,785 |
| Run OS peak resident bytes | 225,771,520 | 439,500,800 |
| Completed resume elapsed ms | 488 | 844 |
| Completed resume OS peak resident bytes | 89,489,408 | 115,245,056 |
| Packet / compiled truncation | true / true | false / false |

Peak resident usage comes from macOS `/usr/bin/time -l` process rusage, not
sampled working-set values or allocated-byte counts. It excludes test fixture
generation. Disk figures are retained file bytes, not transient SQLite/sorter
disk peaks or physical allocated blocks. Source padding makes byte scaling exact;
it does not model arbitrary customer graph fan-out. Both cases have explicit
`partial-static-review` coverage and `ReducedCoverage` compiled reports.

The 32-page case retains cycle truncation gaps. The 256-page case retains
`ProjectlessPublishMemberWorkLimit` (`bounded-publish-member-join-work-exceeded`):
name-only source/compiled candidate matching crosses its 100,000 work bound,
so source member bridge edges are withheld. The larger case's false truncation
flag therefore does not establish complete coverage. This discontinuity and
the roughly 28x run-time growth are follow-up findings, not performance wins.
The work cap was not raised. Native path retention accepts an explicit 4,096
budget for this diagnostic; its default remains 256 and other limits are unchanged.

The retained local diagnostic directory is
`output/native-scale-32-256-final-20260928`. Existing proof directories and
PowerShell compatibility workflows were not deleted or replaced.

## Qualified member-index rerun

The final rebuilt member-index/memory/native-scale suite passed 54/54 with no
failures or skips. Safely framed qualified types now narrow comparison work;
global source/name and receipt-bound assembly counts remain intact for gap
classification. Opaque, nested, malformed and delimiter-bearing identities use
the historical fallback. The 100,000 cap is unchanged, and end-to-end tests prove
that sufficient same-type competitors still withhold the entire member pass.
Another public fixture retains 800 candidate edges for 400 same-name types;
duplicating one same-type method withholds its edges as ambiguous.

| Rerun observation | 32 pages | 256 pages |
| --- | ---: | ---: |
| Retained compiled paths | 128 | 1,024 |
| Evidence + run bytes | 22,796,063 | 177,531,917 |
| Run elapsed ms | 7,363 | 236,527 |
| Run OS peak resident bytes | 219,250,688 | 386,105,344 |
| Packet / compiled truncation | true / true | true / true |

The 256-page case retains 8,192 graph edges (previously 7,168), no
`ProjectlessPublishMemberWorkLimit`, and 256 cycle gaps. All four terminal
branches per page, the grouped handoff and immutable input/resume rosters pass.
Restored source member candidates do not become exact source/binary or runtime
identity. The observed memory peak is lower, but elapsed time is higher; this is
one observation on the same machine, not a throughput or statistical improvement
claim. Coverage remains partial and reduced. The broader representative graph,
transient disk and private Windows gates remain open.

The retained rerun directory is
`output/native-scale-qualified-member-index-20260928`, with exact generator/input
commitments and every phase metric in its local receipt. Earlier observations
and proof directories remain intact.
