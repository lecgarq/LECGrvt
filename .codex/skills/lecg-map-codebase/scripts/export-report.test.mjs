import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync, readdirSync, unlinkSync } from 'node:fs';
import { createHash } from 'node:crypto';
import { dirname, resolve } from 'node:path';
import { execFileSync, spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { root, readInventory, inventoryFreshness, selectEntries } from './query-index.mjs';
import { exportUnvalidatedSetters, countStates } from './export-report.mjs';

const script = fileURLToPath(new URL('./export-report.mjs', import.meta.url));
const directory = resolve(root, 'docs/review');

test('out-of-contract setters remain selected and explicitly counted', () => {
  const entries = ['same_value_only', 'missing_fixture', 'context_rejected', 'out_of_contract',
    'changed_value_tested'].map((state, i) => ({ operation: `api.set:A.P${i}`, state,
      passed_models: state === 'changed_value_tested' ? 1 : 0,
      context_failures: 0, unsupported_models: 0, same_value_models: 0 }));
  const selected = selectEntries({}, 'validation', 'unvalidated-setters', () => ({ entries }));
  assert.equal(selected.length, 4);
  assert.deepEqual(countStates(selected), { context_rejected: 1, missing_fixture: 1,
    out_of_contract: 1, same_value_only: 1 });
  assert.equal(Object.keys(selected.find(e => e.state === 'out_of_contract')).length, 6);
});

test('report rejects stale inputs and unsupported CLI arguments without writing', () => {
  const before = readdirSync(directory);
  assert.throws(() => exportUnvalidatedSetters({ schema_version: 1,
    input_sha256: { 'RevitCopilot/Agent/Knowledge/revit2026-validation.json': 'wrong' } }), /STALE/);
  assert.throws(() => exportUnvalidatedSetters({ schema_version: 1 }), /missing/);
  for (const args of [[], ['--raw'], ['unvalidated-setters', '../elsewhere.json'], ['counts']]) {
    const result = spawnSync(process.execPath, [script, ...args], { encoding: 'utf8' });
    assert.equal(result.status, 1);
    assert.equal(result.stdout, '');
    assert.ok(Buffer.byteLength(result.stderr) < 1024);
  }
  assert.deepEqual(readdirSync(directory), before);
});

test('report exports the complete selected ledger to unique files with bounded receipts', t => {
  const index = readInventory();
  if (!inventoryFreshness(index).inventory_admissible) {
    t.skip('Live export integration requires CURRENT inventory; stale rejection is tested separately.');
    return;
  }
  const paths = [];
  try {
    for (let i = 0; i < 2; i++) {
      const output = execFileSync(process.execPath, [script, 'unvalidated-setters'],
        { cwd: process.env.TEMP, encoding: 'utf8' });
      const receipt = JSON.parse(output);
      assert.equal(dirname(receipt.path), directory);
      assert.match(receipt.path, /unvalidated-setters-[\da-f-]+\.json$/);
      paths.push(receipt.path);
      assert.ok(Buffer.byteLength(output) <= 1024);
      const bytes = readFileSync(receipt.path);
      const report = JSON.parse(bytes);
      assert.equal(receipt.sha256, createHash('sha256').update(bytes).digest('hex'));
      assert.equal(receipt.bytes, bytes.length);
      assert.equal(receipt.total, report.entries.length);
      assert.deepEqual(receipt.counts_by_state, countStates(report.entries));
      assert.deepEqual(report.counts_by_state, receipt.counts_by_state);
      assert.equal(report.inventory_admissible, true);
      assert.deepEqual(report.entries, selectEntries(index, 'validation', 'unvalidated-setters'));
    }
    assert.notEqual(paths[0], paths[1]);
  } finally {
    // Only files created by this test, verified inside the fixed report directory.
    for (const path of paths) unlinkSync(path);
  }
});
