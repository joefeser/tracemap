# Base44 Static Evidence Cross-Application Validation

Date: 2026-07-19

This receipt exercises the reusable TraceMap adapter against three immutable source authorities used by 88mph. It proves static extraction and source binding only. It does not replace the 88mph capability census, runtime, migration, requirements, deployment, or browser evidence.

| Application | Repository commit | Accepted source SHA-256 | Accepted tree SHA-256 | Static facts | Packet SHA-256 |
| --- | --- | --- | --- | ---: | --- |
| HarborMusic | `69c0716c1caa8d549bb247afa64e0c0d0e285666` | `345e80e65003c8731e92fb61c5ce326de6c2d0d5b030e69944e981086e8dfe41` | `a0b94f42a9511a80728675fbcd46bbc6c098fa0870f78f2711f67502c8e7e93d` | 1,648 | `0fdf240b64824c8009a6a949751bea0ece437e37ac6a9e181740a52dfb7fa0f5` |
| DigitalTwin-Fork | `664f44068546aa243c106066bf5d6ad9a6f9a313` | `014e3ebb55dbdb248dfa47b9b08af2771b2523c13c5548cb3517cd5827275c1f` | `e53b23f2e277070042e7cbe740efbdfabd90b7b337c4d3aeeea3bd7c0210c679` | 3,311 | `1e890b2beb6e19e841fc0f567cc88e07f2006b3f5a5c5403bbf8d224f1ece0d5` |
| ShopGenie-Fork | `b7343b0a235c29b0f6b718c4ae7b8ee7778d5712` | `9a12a8531d600b00e8766f24b9b8425058dd21cf8e4796b0884cd05c2217a321` | `ab867c8bcacfc853f4a3a484494971238e990b24141b294613efd11e5794af52` | 862 | `316832deffbe1a9b24d65d4beb84d26d5613a33af3cc598726d23b6b51767f9c` |

Harbor was re-resolved from `BigRiverMachine/HarborNetwork` `main`, which still pointed to the accepted #301 commit. DigitalTwin used the recorded historical #294 authority in a detached worktree because its upstream branch has moved. ShopGenie used the accepted #237 live-proof source commit from `joefeser/ShopGenie`; “ShopGenie-Fork” is the fixture/application label, not a current repository name. DigitalTwin's accepted-source SHA-256 is the deterministic `git archive --format=tar` digest computed from its exact recorded commit; its accepted normalized tree digest remains the #294 authority.

All three scans correctly report `Level3SyntaxAnalysis` with one project-loading gap because these Base44 exports do not supply a loadable TypeScript project. This is expected for the additive JS/JSX/TS/TSX syntax adapter. The packet preserves the gap, and consumers must not turn missing facts into clean absence.

## Fact coverage

- Harbor: 94 customer boundary rows (93 function entry surfaces plus one shared function-library source), 438 entity operations, 93 environment accesses, 110 function invocations, 30 HTTP targets, 93 SDK imports, and 697 SDK primitive calls.
- DigitalTwin-Fork: 133 function surfaces/customer boundaries, 1,197 entity operations, 48 environment accesses, 117 function invocations, 35 HTTP targets, 134 SDK imports, and 1,514 SDK primitive calls.
- ShopGenie-Fork: 428 entity operations, 1 SDK import, and 433 SDK primitive calls. Its accepted source is a frontend-only legacy export, so no backend function entry surfaces are present in that repository commit.

## Commands

```bash
cd src/typescript
npm run check
node dist/src/cli.js base44-evidence --repo <detached-authority> --out <output> \
  --accepted-source-sha256 <source> --accepted-tree-sha256 <tree> \
  --coverage-label <authority-label>
shasum -a 256 <output>/base44-evidence.json
```

Validation result: build passed; 32/32 TypeScript tests passed; JSON, Markdown, HTML, normal facts, SQLite, manifest, report, and log outputs were produced for every source. Controlled tests also prove fail-closed malformed SHA input, URL/secret redaction, JSX extraction, and explicit reduced-coverage diff behavior.
