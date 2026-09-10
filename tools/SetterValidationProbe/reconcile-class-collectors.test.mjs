import test from 'node:test';
import assert from 'node:assert/strict';
import { promoteValidated } from './reconcile-class-collectors.mjs';

const receipt = { status: 'validated', model_sha256: 'MODEL-A', target_count: 3,
  classification_only: false, cleanup_verified: true, infrastructure_failure: false,
  setter_attempted: true, rollback_verified: true, commit_status: 'Committed',
  before: -1, desired: 42, after_commit: 42 };

test('promotes the proven prior same-value model exactly once', () => {
  const before = { operation: 'api.set:X', state: 'same_value_only', passed_models: 0,
    context_failures: 0, unsupported_models: 9, same_value_models: 3 };
  assert.deepEqual(promoteValidated(before, receipt), { ...before, state: 'changed_value_tested',
    passed_models: 1, same_value_models: 2 });
});

test('does not double-count a model already backed by a validating receipt', () => {
  const before = { operation: 'api.set:X', state: 'changed_value_tested', passed_models: 1,
    context_failures: 0, unsupported_models: 0, same_value_models: 11 };
  assert.deepEqual(promoteValidated(before, receipt, new Set(['MODEL-A'])), before);
});

test('refuses ambiguous aggregate history', () => {
  const before = { operation: 'api.set:X', state: 'same_value_only', passed_models: 0,
    context_failures: 1, unsupported_models: 0, same_value_models: 11 };
  assert.throws(() => promoteValidated(before, receipt), /cannot identify/);
});
