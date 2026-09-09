import test from 'node:test';
import assert from 'node:assert/strict';
import { execFileSync, spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { parseArgs, selectEntries, serializeBounded } from './query-index.mjs';

const script = fileURLToPath(new URL('./query-index.mjs', import.meta.url));
const fixture = {
  counts: { commands: 2 }, services: { Materials: ['a.cs', 'b.cs'] },
  commands: [{ name: 'AssignMaterialCommand' }], native_operations: [{ name: 'rename_elements' }],
  mcp_tools: [{ name: 'agent_apply' }],
  curated_recipes: [{ id: 'material-check', name: 'Check materials', description: 'Inspect appearance' }],
  api_evidence: { entries_by_state: { same_value_only: 2 } }
};

test('only the specified commands and argument counts are accepted', () => {
  for (const args of [[], ['--raw'], ['constructor'], ['counts', 'extra'], ['service'],
    ['operation', '--path'], ['operation', ' --raw'], ['recipes', 'a', 'b'], ['recipes', '\n'], ['service', ' '.repeat(3)]]) {
    assert.throws(() => parseArgs(args));
  }
  for (const args of [['freshness'], ['counts'], ['service', 'Materials'], ['command', 'C'],
    ['operation', 'O'], ['mcp-tool', 'M'], ['recipes'], ['recipes', 'material'], ['validation', 'read_invoked']]) {
    assert.equal(parseArgs(args).command, args[0]);
  }
});

test('selectors do not expose unrelated inventory sections', () => {
  assert.deepEqual(selectEntries(fixture, 'service', 'materials'), [
    { domain: 'Materials', path: 'a.cs' }, { domain: 'Materials', path: 'b.cs' }]);
  for (const [command, term] of [['command', 'assignmaterialcommand'], ['operation', 'rename_elements'],
    ['mcp-tool', 'agent_apply'], ['recipes', 'appearance'], ['counts']]) {
    assert.equal(selectEntries(fixture, command, term).length, 1);
  }
  assert.deepEqual(selectEntries(fixture, 'command', 'material'), []);
  assert.deepEqual(selectEntries(fixture, 'service', '*'), []);
  assert.throws(() => selectEntries(fixture, 'validation', 'unknown'));
  const rows = selectEntries(fixture, 'validation', 'same_value_only', () => ({ entries: [
    { operation: 'api.set:A.B', state: 'same_value_only' }, { operation: 'api.get:A.C', state: 'read_invoked' }
  ] }));
  assert.equal(rows.length, 1);
  assert.equal(rows[0].operation, 'api.set:A.B');
  assert.equal(selectEntries(fixture, 'validation', 'unvalidated-setters', () => ({ entries: [
    { operation: 'api.set:A.B', state: 'same_value_only' },
    { operation: 'api.set:A.C', state: 'changed_value_tested' },
    { operation: 'api.get:A.D', state: 'read_invoked' }
  ] })).length, 1);
});

test('entry and UTF-8 byte ceilings include metadata, truncation notice and newline', () => {
  const small = JSON.parse(serializeBounded({}, Array.from({ length: 198 }, (_, n) => ({ n }))));
  assert.equal(small.returned, 25);
  assert.equal(small.truncation, 'TRUNCATED: 25/198 entries — refine the query');
  const report = JSON.parse(serializeBounded({ export_command: 'explicit-report' }, Array(40).fill({ n: 1 })));
  assert.equal(report.total, 40);
  assert.match(report.truncation, /explicit report export/);
  for (const width of [20, 200, 5000]) {
    const output = serializeBounded({ source: 'inventory' }, Array(30).fill({ text: '界'.repeat(width) }));
    assert.ok(Buffer.byteLength(output) <= 4096);
    assert.ok(JSON.parse(output).returned <= 25);
    assert.equal(JSON.parse(output).total, 30);
  }
  assert.throws(() => serializeBounded({ text: 'x'.repeat(5000) }, []));
});

test('CLI uses the repository beside the script, not caller cwd, and labels stale data', () => {
  for (const args of [['freshness'], ['counts'], ['service', 'Materials'], ['command', 'AssignMaterialCommand'],
    ['operation', 'rename_elements'], ['mcp-tool', 'agent_apply'], ['recipes'], ['validation', 'same_value_only']]) {
    const output = execFileSync(process.execPath, [script, ...args], { cwd: process.env.TEMP, encoding: 'utf8' });
    const result = JSON.parse(output);
    assert.equal(result.command, args[0]);
    assert.ok(Buffer.byteLength(output) <= 4096);
    assert.ok(result.returned <= 25);
    if (result.repository_revision !== result.head || !result.source_inputs_match_revision
        || !result.inputs_match_working_tree) {
      assert.equal(result.status, 'STALE');
      assert.equal(result.inventory_admissible, false);
    }
  }
  const invalid = spawnSync(process.execPath, [script, 'counts', '--raw'], { encoding: 'utf8' });
  assert.equal(invalid.status, 1);
  assert.equal(invalid.stdout, '');
  assert.ok(Buffer.byteLength(invalid.stderr) < 1024);
});
