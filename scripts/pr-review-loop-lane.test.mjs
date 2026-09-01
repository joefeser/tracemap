import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import test from 'node:test'

const lane = readFileSync(new URL('../.agent-control/lanes/pr-review-loop.yaml', import.meta.url), 'utf8')
const runbook = readFileSync(new URL('../docs/PR_REVIEW_LOOP.md', import.meta.url), 'utf8')

function blockAfter(pattern, indentation) {
  const match = lane.match(pattern)
  assert.ok(match, `missing lane block: ${pattern}`)
  const start = match.index + match[0].length
  const lines = lane.slice(start).split('\n')
  const body = []
  for (const line of lines) {
    if (line.trim() && line.length - line.trimStart().length < indentation) break
    body.push(line)
  }
  return body.join('\n')
}

const quorum = blockAfter(/^      trustedCodeReview:\s*$/m, 8)
const qodo = blockAfter(/^    qodo:\s*$/m, 6)
const codex = blockAfter(/^    codex:\s*$/m, 6)
const claudeLocal = blockAfter(/^    claude-local:\s*$/m, 6)
const localReviewFallback = blockAfter(/^  localReviewFallback:\s*$/m, 4)

function boundedCurrentHeadRecoveryEligible({ codexCurrent, qodoReturnedOnce, qodoRequestCountZero }) {
  const fastQuorum = /minimumReturned:\s*1\b/.test(quorum)
    && /preferAllReturned:\s*false\b/.test(quorum)
  const qodoOnePass = /requirement:\s*required\b/.test(qodo)
    && /waitUntilReturnedBeforeProcessing:\s*true\b/.test(qodo)
    && /requestAllowed:\s*explicit_only\b/.test(qodo)
    && /requestRetryCeiling:\s*0\b/.test(qodo)
  const codexExactHeadRequired = /requirement:\s*required\b/.test(codex)
    && /waitUntilReturnedBeforeProcessing:\s*true\b/.test(codex)
    && /requestAllowed:\s*policy\b/.test(codex)
    && /requestRetryCeiling:\s*2\b/.test(codex)
  return fastQuorum && qodoOnePass && codexExactHeadRequired
    && codexCurrent && qodoReturnedOnce && qodoRequestCountZero
}

test('TraceMap admits stable ACK v0.4.4 through v0.5.x with required review capabilities', () => {
  assert.match(lane, /requiredVersion:\s*">=0\.4\.4 <0\.6\.0"/)
  assert.match(lane, /- reviewQuorum/)
  assert.match(lane, /- requiredReviewerBatching/)
  assert.equal(boundedCurrentHeadRecoveryEligible({
    codexCurrent: true,
    qodoReturnedOnce: true,
    qodoRequestCountZero: true,
  }), true)
})

test('stale Codex plus stale Qodo cannot satisfy the consumer lane contract', () => {
  assert.equal(boundedCurrentHeadRecoveryEligible({
    codexCurrent: false,
    qodoReturnedOnce: true,
    qodoRequestCountZero: true,
  }), false)
  assert.equal(boundedCurrentHeadRecoveryEligible({
    codexCurrent: true,
    qodoReturnedOnce: false,
    qodoRequestCountZero: true,
  }), false)
  assert.equal(boundedCurrentHeadRecoveryEligible({
    codexCurrent: true,
    qodoReturnedOnce: true,
    qodoRequestCountZero: false,
  }), false)
})

test('TraceMap authorizes only the bounded exact-head Opus fallback contract', () => {
  assert.match(lane, /- externalReviewReceipts/)
  assert.match(lane, /- boundedLocalReviewFallback/)
  assert.match(claudeLocal, /enabled:\s*true\b/)
  assert.match(claudeLocal, /provider:\s*claude-code\b/)
  assert.match(claudeLocal, /authority:\s*owner_authorized_receipt\b/)
  assert.match(claudeLocal, /requirement:\s*try\b/)
  assert.match(claudeLocal, /- trustedCodeReview\b/)
  assert.match(claudeLocal, /- joefeser\b/)
  assert.match(claudeLocal, /model:\s*claude-opus-4-8\b/)
  assert.match(claudeLocal, /modelFamily:\s*claude-opus-4\.8\b/)
  assert.match(claudeLocal, /timeoutSeconds:\s*1800\b/)
  assert.match(claudeLocal, /maxBudgetUsd:\s*4\b/)
  assert.match(claudeLocal, /allowDirty:\s*false\b/)
  assert.match(localReviewFallback, /enabled:\s*true\b/)
  assert.match(localReviewFallback, /trigger:\s*fresh_review_fix_cycle_ceiling\b/)
  assert.match(localReviewFallback, /reviewer:\s*claude-local\b/)
  assert.match(localReviewFallback, /maxAttempts:\s*2\b/)
  assert.match(localReviewFallback, /maxFixCycles:\s*2\b/)
  assert.match(localReviewFallback, /postTerminalComment:\s*true\b/)
  assert.match(runbook, /FRESH_REVIEW_FIX_CYCLE_CEILING_REACHED/)
  assert.match(runbook, /owner_authorized_receipt/)
  assert.match(runbook, /aggregate\s+authorized spend is at most \$8/)
  assert.match(runbook, /A PR cannot\s+authorize its own fallback from head-only configuration/)
  assert.match(runbook, /--owner-authorized-local-review/)
  assert.match(runbook, /node "\$ACK_ROOT\/dist\/cli\.js" pr-loop/)
})
