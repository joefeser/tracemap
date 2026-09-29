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
