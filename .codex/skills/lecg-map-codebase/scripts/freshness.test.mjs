import test from 'node:test';
import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { dashboardState } from './freshness.mjs';
import { inventoryFreshness, root } from './query-index.mjs';

test('dashboard cannot establish compiler health, and another workspace is rejected', () => {
  assert.equal(dashboardState({ active_project: { path: 'C:/unrelated' } }), null);
  assert.equal(dashboardState({}), null);
  const result = dashboardState({ active_project: { path: root }, languages: ['csharp'], jetbrains_mode: false });
  assert.equal(result.state, 'reachable');
  assert.equal(result.symbol_resolution_verified, false);
  assert.equal(result.csharp_configured, true);
});

test('missing input hashes and forged revision flags cannot establish freshness', () => {
  const missing = inventoryFreshness({ source_inputs_match_revision: true, repository_revision: 'unknown' });
  assert.equal(missing.inventory_admissible, false);
  assert.equal(missing.inputs_match_working_tree, false);
  assert.equal(missing.input_set_matches, false);
});

test('normal freshness exits zero, strict mode fails for inadmissible inventory', () => {
  const script = fileURLToPath(new URL('./freshness.mjs', import.meta.url));
  for (const args of [[], ['--strict']]) {
    const result = spawnSync(process.execPath, [script, ...args], { encoding: 'utf8' });
    const status = JSON.parse(result.stdout);
    assert.equal(result.status, args.length && !status.index.inventory_admissible ? 1 : 0);
    assert.ok(['reachable', 'degraded', 'unavailable'].includes(status.serena.state));
    assert.equal(status.rendered_docs.status, 'ASSUMED_STALE');
    assert.ok(Buffer.byteLength(result.stdout) < 4096);
    assert.equal(Object.hasOwn(status, 'call_spine'), false);
  }
});
