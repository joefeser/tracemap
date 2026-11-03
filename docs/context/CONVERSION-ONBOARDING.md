# Conversion onboarding — what the agent sets up and what everyone does

Plain-language guide for the agent helping the owner convert the
application's data-access methods. Read `AGENTS.md` and the Codex
handoff (`CODEX-HANDOFF-2026-11-03.md`) for repo rules; this doc is
only the conversion workflow.

## The goal

Each data-access method in the application gets converted using the
evidence from the lifecycle scanner. The scanner proves exactly what
each method does to build its SQL call; the conversion turns that proof
into annotated converted code. Every value the scanner could not prove
becomes a labeled TODO. Nobody guesses, ever — that rule is what makes
each conversion reviewable in one pass instead of weeks.

## Who does what

- **The agent (you)**: set up and run each per-method scan; draft each
  conversion from the scan's candidate facts; compile-check your work;
  hand the owner one file plus a TODO list.
- **The owner (Joe)**: reviews the diff against the original source,
  decides yes/fix/stop, merges things. Only the owner dispatches
  private scans and merges PRs.

## The pipeline — one method, four steps

### Step 1 — Scan the method

The evidence for one method is produced by ONE scan of just that method.
Setup per method (copy the previous run's scan script on the work
machine as the template — it is the proven shape):

- the method selector: its DLL + metadata token (the handoff index
  on the work machine lists the two already-done ones; new methods
  get new tokens)
- a fresh output directory per run (never reuse)
- the same flags as v45/v46, including `--il-max-work 2000000` and the
  bounded-scan guard (120 s / 2048 MiB)

Known cost: one method ≈ 1.28M work units, about 40 seconds. Run once,
stop. If a scan stops with `lifecycle-work-limit`: check whether
anything else shared that run's budget — each method gets its own run.

### Step 2 — Convert from the candidate facts

The rules live in `docs/validation/SQL_LIFECYCLE_ORIGIN_CONTRACT.md`
(public, on dev). The short version:

- Every value in the scan output carries an origin label. The label
  decides: generate the value, or mark `TODO(lifecycle-origin: <kind>):
  <what was not proven>`.
- String-parameter guards are REAL two-branch code (null or empty →
  DBNull; otherwise the value) — the scan carries both branches.
- Forks stay forks: the converted code keeps the branch; it never
  picks a side.
- The wrapper (the parameter structure, the execute helper, Open/Close)
  stays as-is — this phase converts methods, not the wrapper.

### Step 3 — Compile-check

Compile the converted file against the existing wrapper DLL (vbc) as
part of the conversion run. Zero errors. The deliverable never lands
uncompiled.

### Step 4 — The owner reviews one diff

Structure matches the original, both guard branches present, TODOs sit
where a human would ask a question. One review. One fix cycle if it
names defects. A second stall = document and move to the next method —
the evidence is sufficient for what it labels; the rest is human work.

## What already exists (don't redo)

- **Two methods already have complete evidence** — 216 alternatives
  each (v45 and v46 scans; the handoff index maps them). They need
  only steps 2–4.
- **One conversion draft exists on the work machine** (the most
  recent converted-method file, 12 TODOs, never compiled — the
  handoff index names it). It is a head start, not a requirement:
  compile-check it first if you use it; discard it and convert fresh
  if it fights you.

## Rules that never bend

- Never guess a value the scan didn't prove — TODO with the recorded
  assumption.
- Nothing private (method names, SQL, paths, binaries) enters any
  repository. Scan outputs and conversion drafts live on the work
  machine only.
- 1–2 conversion attempts per method, then stop and document.
- Surprises in scans go to the issue list, never a private fix loop.
