# Evidence Graph Vault Export Tasks

## Implementation Tasks

Recommended first PR boundary: implement the core combined-index plus
paths/reverse-report MVP, source claim catalog, deterministic Markdown/JSON, and
safety validation. Portfolio, release-review, and evidence-pack readers should
land in a later PR unless their compatible schemas are confirmed during
implementation.

- [ ] 1. Define schema, storage, and command boundary. Requirements: 1, 2, 8, 9.
  - [ ] Define `evidence-graph-vault-export.v1` graph JSON schema.
  - [ ] Define `graph.json` `contentHash` behavior and canonical JSON writer rules.
  - [ ] Define generated Markdown frontmatter sentinel and content hash behavior.
  - [ ] Choose first implementation shape: `tracemap vault export` or script fallback.
  - [ ] Document the migration path if script fallback is used.
  - [ ] Define supported input kinds, compatibility version fields, and required schema fields.
  - [ ] Define generated output layout and collision behavior for missing files, valid generated files, stale generated files, and non-generated user files.
  - [ ] Define exact stable ID hash inputs, context strings, truncation length, and collision gap behavior for nodes, edges, gaps, limitations, files, links, tags, frontmatter, and arrays.
  - [ ] Define canonical frontmatter key ordering and `--format markdown|json|markdown,json` behavior.
  - [ ] Define the `vault-export.*.v1` rule namespace for exporter-created gaps, limitations, and validation findings.

- [ ] 2. Implement input readers or adapters. Requirements: 1, 3, 4.
  - [ ] Read combined indexes read-only and project safe source/fact/edge evidence.
  - [ ] Read paths and reverse JSON reports using documented schema fields.
  - [ ] Defer or implement portfolio report JSON only after compatible schema fields are confirmed.
  - [ ] Defer or implement release-review report JSON only after compatible schema fields are confirmed.
  - [ ] Defer evidence-pack JSON until schema and claim-level metadata are locked enough for public/demo promotion.
  - [ ] Emit schema gaps for unknown, missing, incompatible, truncated, or partial inputs.
  - [ ] Preserve rule IDs, evidence tiers, coverage labels, commit SHA, extractor versions, supporting fact IDs, supporting edge IDs, and limitations where safe.

- [ ] 3. Implement graph projection. Requirements: 3, 4, 6.
  - [ ] Project source, endpoint, surface, package, SQL/query, WCF, Remoting, WebForms, legacy, symbol, rule, gap, limitation, and report nodes.
  - [ ] Project call/create/value-flow/surface/path/reverse/release-review/portfolio relationships as evidence edges.
  - [ ] Preserve lower-tier and reduced-coverage classifications without promotion.
  - [ ] Emit duplicate or ambiguous identity gaps instead of choosing arbitrary winners.
  - [ ] Omit speculative edges for runtime DI, reflection, serializer mapping, dynamic dispatch, branch feasibility, event firing, mutation semantics, and collection contents.
  - [ ] Keep symbol nodes optional or gated when symbol identity is unsafe or unstable.

- [ ] 4. Implement claim-level filtering. Requirements: 1, 6, 7.
  - [ ] Assign claim levels to inputs, nodes, edges, gaps, limitations, and export summary.
  - [ ] Default mixed/private/local exports to `hidden`.
  - [ ] Implement a reviewed source claim catalog or compatible evidence-pack metadata input for promotion to `demo-safe` or `public-safe`.
  - [ ] Match claim catalog entries by stable source identity and emit gaps for unmatched or ambiguous entries.
  - [ ] Implement `--minimum-claim-level demo-safe` and `--minimum-claim-level public-safe` as explicit output filters.
  - [ ] Recompute top-level classification from included evidence.
  - [ ] Emit sanitized hidden-evidence omission gaps when filtering removes important relationships.
  - [ ] Fail when public-safe filtering leaves no visible evidence.
  - [ ] Ensure filtering never mutates input artifacts.

- [ ] 5. Implement Markdown vault rendering. Requirements: 2, 5, 8.
  - [ ] Generate `README.md`, `index.md`, and node notes from the graph model.
  - [ ] Generate deterministic relative Markdown links using `[text](relative/path.md)` and optional Obsidian-compatible backlinks.
  - [ ] Generate safe frontmatter and tags from closed vocabularies or stable IDs.
  - [ ] Ensure generated frontmatter is first in the file and parseable by standard YAML frontmatter readers.
  - [ ] Escape Markdown table cells and link labels.
  - [ ] Include rule IDs, evidence tiers, coverage labels, supporting IDs, gaps, and limitations near every evidence claim.
  - [ ] Generate sentinels for every generated Markdown file.
  - [ ] Validate stale or hand-edited generated notes.
  - [ ] Refuse to overwrite non-generated user notes.

- [ ] 6. Implement redaction and safety validation. Requirements: 7, 8.
  - [ ] Scan every Markdown file and every JSON string leaf for local paths, home fragments, raw remotes, raw SQL, snippets, config values, connection strings, endpoints, credentials, secrets, tokens, analyzer diagnostics, stack traces, private names, and unsafe Markdown.
  - [ ] Scan every `graph.json` string leaf, including IDs, labels, rule IDs, tags, limitation text, diagnostics, and relationship metadata.
  - [ ] Validate IDs, tags, frontmatter, links, rule IDs, limitation text, and diagnostics as ordinary strings.
  - [ ] Return sanitized diagnostics with category plus JSON pointer or Markdown section/line.
  - [ ] Use context-separated deterministic hashes only for allowed safe inputs.
  - [ ] Omit or category-label secret-like, credential-like, low-entropy, and enumerable private values rather than hashing them.
  - [ ] Ensure `./scripts/check-private-paths.sh` passes without machine-specific allowlists.

- [ ] 7. Add fixtures and tests. Requirements: 1, 2, 3, 4, 5, 6, 7, 8, 10.
  - [ ] Add focused fixtures or generated in-test artifacts for combined index, paths/reverse report, and gap-heavy reduced coverage cases.
  - [ ] Test deterministic `graph.json` byte stability.
  - [ ] Test `graph.json` content hash validation catches stale or hand-edited manifests.
  - [ ] Test deterministic Markdown byte stability.
  - [ ] Test byte stability across different output directories and checkout roots using checked-in golden hashes instead of tracked generated vault output.
  - [ ] Test frontmatter key ordering is stable across reruns.
  - [ ] Test stale generated sentinel failure.
  - [ ] Test invalid generated frontmatter/hash failure.
  - [ ] Test generated Markdown frontmatter parseability.
  - [ ] Test non-generated user note collision failure.
  - [ ] Test hidden evidence filtering, public-safe success, and no-visible-graph-node failure.
  - [ ] Test source claim catalog promotion by stable identity and rejection of display-name-only promotion.
  - [ ] Test lower-tier/reduced-coverage evidence is not promoted.
  - [ ] Test duplicate identity emits gaps.
  - [ ] Test incompatible input schema emits gaps or sanitized failure.
  - [ ] Test unsafe planted values in Markdown, frontmatter, tags, links, JSON leaves, IDs, and diagnostics.
  - [ ] Test Obsidian-friendly links remain valid relative Markdown.
  - [ ] Test public/demo output contains no raw local paths, raw remotes, SQL/config values, snippets, secrets, or private names.
  - [ ] Test `--dry-run` writes no files.
  - [ ] Test `--format markdown` suppresses `graph.json`, `--format json` suppresses Markdown notes, and `--format markdown,json` writes both with consistent hashes.
  - [ ] Test `--force` only overwrites generated files after validation.
  - [ ] Test `--force` does not bypass claim-level, redaction, private-path, stale generated file, or schema gates.
  - [ ] Test explicit `--date` is required for public/demo dates and no wall-clock timestamps, ISO 8601 timestamps, or filesystem timestamp strings appear.
  - [ ] Test every exporter-created gap and limitation node carries a documented `vault-export.*.v1` rule ID.
  - [ ] Test public-safe and demo-safe filtering fail when real nodes are removed and only gap nodes or summary counts remain.

- [ ] 8. Document workflow and limitations. Requirements: 5, 6, 9.
  - [ ] Document command or script usage with placeholders only.
  - [ ] Document input support and schema compatibility behavior.
  - [ ] Document claim levels, filtering, and omission gaps.
  - [ ] Document Obsidian compatibility as optional local Markdown behavior.
  - [ ] Document that exported graph edges are static evidence, not runtime traces.
  - [ ] Document site relationship as future consumption only.
  - [ ] Document validation commands and troubleshooting.

- [ ] 9. Validate implementation. Requirements: 8, 10.
  - [ ] Update this spec's `implementation-state.md` with branch, scope decisions, validation, and follow-ups.
  - [ ] Run focused vault export tests.
  - [ ] Run stale generated output and redaction validation tests.
  - [ ] Run `./scripts/check-private-paths.sh`.
  - [ ] Run `git diff --check`.
  - [ ] Run `dotnet build src/dotnet/TraceMap.sln` and `dotnet test src/dotnet/TraceMap.sln` if .NET code changes.
  - [ ] Run focused script tests and document deferred .NET tests if implementation is script-only.
  - [ ] Run or explicitly defer relevant pinned smoke checks from `docs/VALIDATION.md`.

## Deferred Follow-Ups

- Hosted HTML viewer or site integration.
- Generated screenshots or visual layouts.
- Obsidian plugin-specific metadata beyond plain Markdown links/tags.
- Runtime telemetry overlays.
- Ownership/service catalog/vulnerability/license overlays.
- Syncing user-authored notes back to TraceMap.
