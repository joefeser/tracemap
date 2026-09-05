# Base44 Entity Payload Evidence Validation

Date: 2026-09-05

This receipt validates TraceMap issue #713 against the canonical ShopGenie
source. It is independent static evidence only. It does not create database
schema authority, prove runtime behavior, admit an SDK, or declare ShopGenie
compatible.

## Bound identities

- Source repository: `BigRiverMachine/ShopGenie`
- Source ref: exact `origin/main`
- Source commit: `6753cfa6264b04c0d7dbfcde942689ee32d2c06b`
- Accepted source/tree SHA-256:
  `a5a313b180ef0cdfd2e837779f251cc754978fcd474b6a32646c133b058b6e24`
- TraceMap implementation commit:
  `e656f19a7905fc07f8af6657526ee34f86bf23ea`
- Extractor identity: `base44-evidence@base44-evidence/0.2.0`
- Evidence packet SHA-256:
  `99433b83c0aef7dfdf9ac3f705b2e61462a2ff2e58e03851e9663f06fbab4bdf`

The packet was generated into a disposable local output directory and was not
committed because standard scan artifacts are local-only.

## Results

| Evidence | Count |
| --- | ---: |
| Existing Base44 entity-operation facts | 1,368 |
| Linked entity payload/query shape facts | 1,368 |
| Mutation payload facts | 537 |
| Complete mutation payload facts | 402 |
| Partial mutation payload facts | 33 |
| Unresolved mutation payload facts | 102 |
| Query shape facts | 831 |
| Unique statically observed payload field names | 111 |
| Decimal-number payload field observations | 84 |
| Numeric-coercion payload field observations | 22 |
| ToolingItem decimal `job_cost` observations | 3 |

Every operation fact has one distinct matching `operationEvidenceId` in a
payload or query fact. This binds the new evidence to the existing TraceMap
operation census without replacing it.

The result independently demonstrates incompatible rigid-required-field
assumptions. Among complete executable-source create shapes:

- one ToolingItem call contains `cost` without `job_cost`;
- three ToolingItem calls contain `job_cost` without `cost`;
- ten of fourteen MaterialItem calls omit `vendor`; and
- twelve of fourteen MaterialItem calls omit `qty`.

These observations are callsite facts, not a declaration that any field is
globally optional. Partial and unresolved sibling callsites preserve their own
typed gaps.

## Controlled acceptance checks

The focused TypeScript suite proves:

- field presence classes for unconditional, conditional, spread-derived, and
  dynamic-computed properties;
- integer, decimal, numeric-expression, numeric-coercion, literal, call,
  property, and binding expression classes without storing values;
- bounded intrafile object binding, spread, post-initialization assignment, and
  lexical-shadow handling;
- source line spans and snippet hashes for every emitted field, including
  fields derived from declarations and mutations away from the SDK callsite;
- conservative handling of logical fallback branches, direct and compound
  assignments, array spreads, and nested unexecuted function bodies;
- redaction of string-literal element-access segments from property origins;
- deterministic fact identities for an unchanged executable source snapshot;
- zero Base44 fact delta after adding a false `database-schema.md` document;
- an exact added/removed payload-shape delta after changing executable payload
  code; and
- absence of seeded private literal values from the packet.

## Commands

The required local command matrix from `docs/VALIDATION.md` was run from the
repository root:

```bash
dotnet build src/dotnet/TraceMap.sln
dotnet test src/dotnet/TraceMap.sln
npm run check --prefix src/typescript
JAVA_HOME=/opt/homebrew/opt/openjdk@21/libexec/openjdk.jdk/Contents/Home gradle -p src/jvm test
JAVA_HOME=/opt/homebrew/opt/openjdk@21/libexec/openjdk.jdk/Contents/Home gradle -p src/jvm installDist
python3 -m venv /tmp/tracemap-python-venv
/tmp/tracemap-python-venv/bin/python -m pip install -e "src/python[dev]"
/tmp/tracemap-python-venv/bin/python -m pytest src/python/tests
PYTHON_BIN=/tmp/tracemap-python-venv/bin/python ./scripts/smoke-python-endpoints.sh
python3 scripts/test_validate_adapter_artifacts.py
./scripts/check-private-paths.sh
git diff --check
```

The applicable pinned public TypeScript smoke checks were also run at the exact
commits listed in `docs/VALIDATION.md`:

```bash
TRACEMAP_SKIP_BUILD=1 \
TRACEMAP_OSS_SMOKE_REPOS=scip-typescript,axios-npm-lock \
  scripts/smoke-open-source-repos.sh \
  /tmp/tracemap-oss-cache \
  /tmp/tracemap-oss-smoke-pr717-v2
```

The canonical private replay was then run separately:

```bash
node src/typescript/dist/src/cli.js base44-evidence \
  --repo <canonical-shopgenie-origin-main-checkout> \
  --out <disposable-output> \
  --accepted-source-sha256 a5a313b180ef0cdfd2e837779f251cc754978fcd474b6a32646c133b058b6e24 \
  --accepted-tree-sha256 a5a313b180ef0cdfd2e837779f251cc754978fcd474b6a32646c133b058b6e24 \
  --coverage-label canonical-shopgenie-origin-main-syntax \
  --no-semantic
python3 scripts/validate-adapter-artifacts.py <disposable-output>
./scripts/check-private-paths.sh
git diff --check
```

Results: the .NET solution built and 1,732/1,732 tests passed; 51/51 TypeScript
tests passed; JVM tests and distribution installation passed; 64/64 Python
tests and the Python endpoint smoke passed; 7/7 artifact-validator tests passed;
the private-path guard and diff check passed. The pinned `scip-typescript` scan
emitted 14,060 facts at commit
`891eb4293709a6a587bf4468dfa1b45a85182fd9`; the pinned `axios-npm-lock` scan
emitted 11,186 facts at commit
`84a9f3b9a4f3244b8c8e818f557d64c7b964fb25`. Both completed with their expected
explicit reduced-coverage labels. The canonical ShopGenie adapter artifacts
passed the shared artifact validator, and all 2,800 emitted payload/query field
observations carried a contributing line span and 64-hex snippet hash.

The public demo, combined-path smoke, .NET binlog smoke, Swift smokes, and
non-TypeScript OSS repositories were not run because this patch changes only
the Base44 TypeScript extractor and its additive string-valued fact properties;
it does not change shared combine/report/path behavior or another language
adapter. The .NET, JVM, and Python owning suites nevertheless ran through the
required cross-language matrix above. No coverage check applicable to this
changed adapter was deferred, so no follow-up issue is required.

## Remaining boundary

This is 1,368/1,368 shape coverage for TraceMap's current source-proven static
entity-operation facts, not whole-app compatibility. The hardened independent
reverse-engineer currently expands additional closed dynamic selector
candidates. The 88mph reconciliation stage must compare both inventories,
preserve every discrepancy as a blocker, and must not use either tool's missing
row as clean absence. The 33 partial and 102 unresolved payload facts also
remain explicit analysis gaps until stronger executable-source evidence closes
them.
