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

function onePassQodoEligible({ codexCurrent, qodoPriorClean, qodoCountersZero }) {
  const fastQuorum = /minimumReturned:\s*1\b/.test(quorum)
    && /preferAllReturned:\s*false\b/.test(quorum)
  const qodoOnePass = /requirement:\s*required\b/.test(qodo)
    && /waitUntilReturnedBeforeProcessing:\s*false\b/.test(qodo)
    && /requestAllowed:\s*explicit_only\b/.test(qodo)
  const codexExactHeadRequired = /requirement:\s*required\b/.test(codex)
    && /waitUntilReturnedBeforeProcessing:\s*true\b/.test(codex)
    && /requestAllowed:\s*policy\b/.test(codex)
  return fastQuorum && qodoOnePass && codexExactHeadRequired
    && codexCurrent && qodoPriorClean && qodoCountersZero
}

test('TraceMap selects ACK PR #281 one-pass Qodo policy without weakening Codex', () => {
  assert.match(lane, /requiredVersion:\s*">=0\.2\.0"/)
  assert.match(lane, /- reviewQuorum/)
  assert.match(lane, /- requiredReviewerBatching/)
  assert.equal(onePassQodoEligible({ codexCurrent: true, qodoPriorClean: true, qodoCountersZero: true }), true)
})

test('stale Codex plus stale Qodo cannot satisfy the consumer lane contract', () => {
  assert.equal(onePassQodoEligible({ codexCurrent: false, qodoPriorClean: true, qodoCountersZero: true }), false)
  assert.equal(onePassQodoEligible({ codexCurrent: true, qodoPriorClean: true, qodoCountersZero: false }), false)
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
})
