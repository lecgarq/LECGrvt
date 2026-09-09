import test from 'node:test';
import assert from 'node:assert/strict';
import { checkAttempts } from './reconcile-dedicated.mjs';

const plan = { operation: 'api.set:Example.Id', models: ['a.rvt', 'b.rvt'] };
const refused = model => ({ operation: plan.operation, model, model_sha256: model, status: 'rejected-with-reason', cleanup_verified: true });
const passed = model => ({ ...refused(model), status: 'validated', setter_attempted: true, rollback_verified: true,
  commit_status: 'Committed', before: 1, desired: 2, after_commit: 2 });

test('a dedicated case completes at first success or exhausts its frozen model order', () => {
  checkAttempts(plan, [passed('a.rvt')]);
  checkAttempts(plan, [refused('a.rvt'), passed('b.rvt')]);
  checkAttempts(plan, [refused('a.rvt'), refused('b.rvt')]);
  for (const receipts of [[], [refused('a.rvt')], [passed('b.rvt')], [passed('a.rvt'), refused('b.rvt')],
    [refused('a.rvt'), { ...passed('b.rvt'), classification_only: true }],
    [refused('a.rvt'), { ...passed('b.rvt'), cleanup_verified: false }]])
    assert.throws(() => checkAttempts(plan, receipts));
});
