# Tasks

## Public and work — highest priority

- [x] Add a language-neutral public quickstart.
- [x] Add public evidence-led use cases and prompt patterns.
- [x] Add a work-machine Claude Code handoff prompt and commands.
- [x] Add a single setup/config command with seven bounded settings.
- [x] Add a single resumable pipeline command and run receipt.
- [x] Add typed solution/path preflight errors and bounded candidate hints.
- [x] Constrain multi-project discovery to the three configured roots.
- [x] Make all-pages mode explicit and keep selected-page mode bounded.
- [x] Keep scan, packet, evidence docs, workbench, logs, config, and receipt in
  one review root with clear retention guidance.
- [x] Validate projectless, one-project, multi-project, C#, and VB.NET fixtures.
- [x] Recover a completed scan that exceeded the original 2 GiB receipt-hash
  limit without rescanning or losing original/current tool provenance.
- [x] Give C# semantic and syntax call facts explicit coverage labels so valid
  retained calls do not become packet provenance gaps.
- [ ] Consolidate the 450-line focused review reference after compatibility
  wrappers and recovery paths are pinned by tests.

## Private — after public/work stabilization

- [ ] Specify an alias-only `wits-ticket-plan.v1` dry-run schema.
- [ ] Define grouping, fan-out caps, approval gates, and idempotency receipts.
- [ ] Define the WITS -> HACP/Routeboard -> external tracker -> ACK contract.
- [ ] Obtain legal review before selecting a restrictive/commercial license.

## Release checks

- [x] Run focused PowerShell tests for each script slice.
- [x] Run the relevant .NET tests and pinned validation workflow.
- [x] Confirm every new machine-readable artifact records generator/input hashes.
- [x] Confirm public artifacts contain no private paths, symbols, source, scan
  identity, commit identity, or private-input fingerprints.
