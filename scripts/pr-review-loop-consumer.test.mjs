import assert from 'node:assert/strict'
import { readFileSync, writeFileSync, mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { createRequire } from 'node:module'
import { execFileSync } from 'node:child_process'
import test from 'node:test'

// Explicit pinned installation; this test never installs software or calls GitHub.
assert.ok(process.env.ACK_ROOT, 'Set ACK_ROOT to the verified ACK v0.5.5 release directory')
const cli = join(process.env.ACK_ROOT, 'dist/cli.js')
const version = JSON.parse(execFileSync(process.execPath, [cli, 'version', '--json'], { encoding: 'utf8' }))
assert.equal(version.version, '0.5.5')
assert.equal(version.gitSha, 'ab39833')
assert.equal(version.distFresh, true)
const require = createRequire(cli)
const { parse } = require('yaml')
const lane = parse(readFileSync(new URL('../.agent-control/lanes/pr-review-loop.yaml', import.meta.url), 'utf8'))
const head = '1111111111111111111111111111111111111111'

for (const baz of ['missing', 'stale', 'current']) {
  test(`real ACK consumer holds ${baz} Baz correctly and never requests retired Qodo`, () => {
    const root = mkdtempSync(join(tmpdir(), 'tracemap-ack-consumer-'))
    try {
      const packet = structuredClone(lane.prLoopPacket)
      packet.pr = 1
      packet.base = 'dev'
      packet.configSource = { laneConfig: { status: 'loaded', path: '.agent-control/lanes/pr-review-loop.yaml', repoLocal: true, committed: true, requiredCapabilities: lane.agentControl.requiredCapabilities }, packet: { source: 'cli' } }
      const fixture = {
        id: `tracemap-${baz}-baz`, name: `TraceMap ${baz} Baz`, clock: { start: '2026-10-09T00:00:00Z' },
        input: { packet, pr: { number: 1, baseRefName: 'dev', headRefName: 'codex/synthetic', headRefOid: head, mergeStateStatus: 'CLEAN', body: 'Synthetic consumer contract' }, files: ['src/dotnet/TraceMap.Core/Synthetic.cs'], checks: [{ name: 'unit', state: 'SUCCESS' }] },
        timeline: [{ at: '0s', type: 'review_clean', bot: 'codex', head, body: 'No findings.' },
          ...(baz === 'missing' ? [] : [{ at: '1s', type: 'advisory_review', reviewer: 'baz', head: baz === 'current' ? head : '2222222222222222222222222222222222222222', state: 'APPROVED' }])],
        checkpoints: [{ at: '2s' }]
      }
      const path = join(root, 'fixture.json')
      writeFileSync(path, JSON.stringify(fixture))
      const result = JSON.parse(execFileSync(process.execPath, [cli, 'pr-loop', 'simulate', '--fixture', path], { encoding: 'utf8', maxBuffer: 8 * 1024 * 1024 }))
      assert.equal(result.resultJson.canMerge, baz === 'current', JSON.stringify(result.resultJson))
      if (baz !== 'current') assert.equal(result.resultJson.evidence.hostedReviewBatch.patchAuthorized, false)
      assert.ok(!JSON.stringify(result.mutations).includes('@qodo'))
      assert.ok(!result.resultJson.evidence.requiredReviewBatch?.unsettledReviewers?.includes('qodo'))
    } finally { rmSync(root, { recursive: true, force: true }) }
  })
}
