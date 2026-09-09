import test from 'node:test';
import assert from 'node:assert/strict';
import { aggregate, receiptAttempt } from './reconcile-pilot.mjs';

test('retries do not add model counts and success is not erased by later failures', () => {
  const attempts = [{ model_sha256: 'A', state: 'same_value_only' }, { model_sha256: 'B', state: 'same_value_only' },
    { model_sha256: 'A', state: 'changed_value_tested' }, { model_sha256: 'A', state: 'context_rejected' }];
  const record = aggregate('api.set:A.P', attempts);
  assert.deepEqual(record, { operation: 'api.set:A.P', state: 'changed_value_tested', passed_models: 1,
    context_failures: 0, unsupported_models: 0, same_value_models: 1 });
  assert.deepEqual(aggregate('api.set:A.P', [...attempts, ...attempts]), record);
});

test('out_of_contract is visible, retains the record, and cannot coexist with a validation', () => {
  const rows = [{ model_sha256: 'A', state: 'out_of_contract' }];
  const record = aggregate('api.set:A.P', rows);
  assert.equal(record.state, 'out_of_contract');
  assert.equal(Object.keys(record).length, 6);
  assert.equal(record.passed_models, 0);
  for (const sha of ['A', 'B']) assert.throws(() => aggregate('api.set:A.P', [...rows,
    { model_sha256: sha, state: 'changed_value_tested' }]), /Contradictory/);
});

test('no receipt or test-runner success substitutes for a committed and restored changed value', () => {
  const good = { status: 'validated', setter_attempted: true, commit_status: 'Committed', before: 1,
    desired: 2, after_commit: 2, rollback_verified: true, cleanup_verified: true, model_sha256: 'A' };
  assert.equal(receiptAttempt(good).state, 'changed_value_tested');
  for (const patch of [{ cleanup_verified: false }, { infrastructure_failure: true }, { rollback_verified: false },
    { commit_status: 'RolledBack' }, { after_commit: 1 }, { desired: 3 }, { setter_attempted: false }, { status: 'NUnit passed' }])
    assert.throws(() => receiptAttempt({ ...good, ...patch }));
  assert.equal(receiptAttempt({ status: 'rejected-with-reason', cleanup_verified: true,
    setter_attempted: false, model_sha256: 'A' }).state, 'context_rejected');
});
