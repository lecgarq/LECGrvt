import test from 'node:test';
import assert from 'node:assert/strict';
import { mkdirSync, mkdtempSync, writeFileSync, readFileSync } from 'node:fs';
import { join, resolve, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';
import { spawnSync } from 'node:child_process';

test('compiled C# exporter preserves out_of_contract, denominator, and rejects contradictory evidence', () => {
  const root = resolve(dirname(fileURLToPath(import.meta.url)), '../..');
  const scratch = join(root, 'docs/review/tmp');
  mkdirSync(scratch, { recursive: true });
  const directory = mkdtempSync(join(scratch, 'setter-ledger-test-'));
  const dll = join(root, 'RevitCopilot/Research/bin/x64/Debug/net10.0/KnowledgeLab.dll');
  const api = Array.from({ length: 2216 }, (_, i) => ({ operation: `api.${i < 805 ? 'set' : 'get'}:Synthetic.P${i}`,
    kind: i < 805 ? 'change' : 'read' }));
  const operations = ['out_of_contract', 'roundtrip_only', 'context_rejected', 'missing_fixture', 'passed']
    .map((status, i) => ({ operation: api[i].operation, model: 'synthetic-fixture', status, verification: 'Changed-value synthetic test ONLY' }));
  const input = join(directory, 'synthetic-input.json');
  const output = join(directory, 'synthetic-ledger.json');
  const source = { status: 'completed', error: null, installed_catalog: { api }, operations };
  const execute = () => spawnSync('dotnet', [dll, 'export-validation', '--benchmark', input, '--destination', output,
    '--output', directory], { encoding: 'utf8' });
  writeFileSync(input, JSON.stringify(source));
  const success = execute();
  assert.equal(success.status, 0, success.stderr);
  const exported = JSON.parse(readFileSync(output));
  assert.equal(exported.entries.length, 2216);
  assert.equal(exported.entries.filter(e => e.operation.startsWith('api.set:')).length, 805);
  assert.equal(exported.entries.filter(e => e.state === 'changed_value_tested').length, 1);
  assert.deepEqual(exported.entries.find(e => e.operation === api[0].operation), {
    operation: api[0].operation, state: 'out_of_contract', passed_models: 0,
    context_failures: 0, unsupported_models: 0, same_value_models: 0 });
  operations.push({ operation: api[0].operation, model: 'synthetic-fixture', status: 'passed', verification: 'Changed-value synthetic ONLY' });
  writeFileSync(input, JSON.stringify(source));
  const rejected = execute();
  assert.notEqual(rejected.status, 0);
  assert.match(rejected.stderr, /Contradictory/);
  assert.deepEqual(JSON.parse(readFileSync(output)), exported, 'Rejected input must not overwrite the previous export.');
});
