import assert from 'node:assert/strict'
import { readFileSync, writeFileSync, mkdtempSync, mkdirSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { fileURLToPath, pathToFileURL } from 'node:url'
import { execFileSync } from 'node:child_process'
import test from 'node:test'

// Explicit pinned installation; this test never installs software or calls GitHub.
assert.ok(process.env.ACK_ROOT, 'Set ACK_ROOT to the verified ACK v0.5.5 release directory')
const cli = join(process.env.ACK_ROOT, 'dist/cli.js')
const version = JSON.parse(execFileSync(process.execPath, [cli, 'version', '--json'], { encoding: 'utf8' }))
assert.equal(version.version, '0.5.5')
assert.equal(version.gitSha, 'ab39833')
assert.equal(version.distFresh, true)
const { loadPacket } = await import(pathToFileURL(join(process.env.ACK_ROOT, 'dist/commands/pr-review-loop.js')))
const repoRoot = fileURLToPath(new URL('../', import.meta.url))
const lanePath = '.agent-control/lanes/pr-review-loop.yaml'
const laneText = readFileSync(join(repoRoot, lanePath), 'utf8')
const options = { cwd: repoRoot, laneConfig: lanePath, repo: 'joefeser/tracemap', pr: '1', base: 'dev' }
// The real loader derives these claims from Git and the canonical path. Never inject them.
const loadedPacket = await loadPacket(options)
assert.equal(loadedPacket.configSource.laneConfig.status, 'loaded')
assert.equal(loadedPacket.configSource.laneConfig.repoLocal, true)
assert.equal(loadedPacket.configSource.laneConfig.committed, true)
assert.equal(loadedPacket.hostedReviewers.baz.authority, 'trusted_exact_head')

for (const invalid of ['missing', 'altered', 'external', 'untracked', 'overlay']) {
  test(`real ACK loader rejects ${invalid} lane authority`, async () => {
    const root = mkdtempSync(join(tmpdir(), 'tracemap-ack-loader-'))
    try {
      const repo = join(root, 'repo')
      mkdirSync(join(repo, '.agent-control/lanes'), { recursive: true })
      const git = (...args) => execFileSync('git', ['-C', repo, ...args], { encoding: 'utf8', stdio: 'pipe' })
      git('init')
      writeFileSync(join(repo, lanePath), laneText)
      git('add', lanePath)
      git('-c', 'user.name=Synthetic Test', '-c', 'user.email=test@example.invalid', '-c', 'commit.gpgsign=false', 'commit', '-m', 'Synthetic committed lane')
      const input = { ...options, cwd: repo }
      let error = /exact committed repo-local lane/
      if (invalid === 'missing') {
        rmSync(join(repo, lanePath))
        error = /Lane config not found/
      } else if (invalid === 'altered') {
        writeFileSync(join(repo, lanePath), laneText + '\n# Uncommitted modification\n')
      } else if (invalid === 'external') {
        const external = join(root, 'external.yaml')
        writeFileSync(external, laneText)
        input.laneConfig = external
      } else if (invalid === 'untracked') {
        writeFileSync(join(repo, 'untracked.yaml'), laneText)
        input.laneConfig = 'untracked.yaml'
      } else {
        const overlay = join(root, 'packet.json')
        // Even copied valid source claims cannot authorize an overlay.
        writeFileSync(overlay, JSON.stringify({ configSource: loadedPacket.configSource }))
        input.packet = overlay
        error = /cannot be combined with packet or stdin overlays/
      }
      await assert.rejects(() => loadPacket(input), error)
    } finally { rmSync(root, { recursive: true, force: true }) }
  })
}
const head = '1111111111111111111111111111111111111111'

for (const baz of ['missing', 'stale', 'current']) {
  test(`real ACK consumer holds ${baz} Baz correctly and never requests retired Qodo`, () => {
    const root = mkdtempSync(join(tmpdir(), 'tracemap-ack-consumer-'))
    try {
      const packet = structuredClone(loadedPacket)
      packet.pr = 1
      packet.base = 'dev'
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
