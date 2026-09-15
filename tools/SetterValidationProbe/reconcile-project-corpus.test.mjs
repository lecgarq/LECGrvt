import test from 'node:test';
import assert from 'node:assert/strict';
import { randomUUID } from 'node:crypto';
import { appendProjectAttempt, promoteWithinCorpus } from './reconcile-project-corpus.mjs';

const receipt = (status, reason = null) => ({ status, reason, model_sha256: randomUUID(),
  classification_only: false, cleanup_verified: true, infrastructure_failure: false,
  setter_attempted: status !== 'rejected-with-reason', rollback_verified: status !== 'rejected-with-reason',
  commit_status: status === 'validated' ? 'Committed' : null,
  before: status === 'validated' ? 1 : null, desired: status === 'validated' ? 2 : null,
  after_commit: status === 'validated' ? 2 : null });

test('promotes a same-corpus validation without changing its 12-model total', () => {
  const before = { operation: 'api.set:X', state: 'same_value_only', passed_models: 0,
    context_failures: 11, unsupported_models: 0, same_value_models: 1 };
  assert.deepEqual(promoteWithinCorpus(before, receipt('validated')), {
    ...before, state: 'changed_value_tested', passed_models: 1, same_value_models: 0 });
});

test('maps project outcomes to exact ledger counters', () => {
  const before = { operation: 'api.set:X', state: 'same_value_only', passed_models: 0,
    context_failures: 0, unsupported_models: 0, same_value_models: 12 };
  let after = appendProjectAttempt(before, receipt('rejected-with-reason', 'no_existing_target_in_unmodified_sample'));
  after = appendProjectAttempt(after, receipt('rejected-with-reason', 'no_proven_valid_alternative'));
  after = appendProjectAttempt(after, receipt('rejected-with-reason', 'InvalidOperationException: context'));
  after = appendProjectAttempt(after, receipt('still-same-value', 'committed_but_unchanged'));
  assert.deepEqual(after, { ...before, context_failures: 1, unsupported_models: 2, same_value_models: 13 });
});

test('project validation adds a new model and promotes the state', () => {
  const before = { operation: 'api.set:X', state: 'same_value_only', passed_models: 0,
    context_failures: 0, unsupported_models: 10, same_value_models: 2 };
  assert.deepEqual(appendProjectAttempt(before, receipt('validated')), {
    ...before, state: 'changed_value_tested', passed_models: 1 });
});
