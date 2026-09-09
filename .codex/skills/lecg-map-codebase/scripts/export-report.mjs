import { realpathSync, writeFileSync } from 'node:fs';
import { createHash, randomUUID } from 'node:crypto';
import { join, relative, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { root, readInventory, inventoryFreshness, selectEntries } from './query-index.mjs';

const recordsSource = 'RevitCopilot/Agent/Knowledge/revit2026-validation.json';

export const countStates = entries => Object.fromEntries(
  [...new Set(entries.map(e => e.state))].sort().map(state =>
    [state, entries.filter(e => e.state === state).length]));

// Explicit report action, never an implicit side effect of query-index.
export function exportUnvalidatedSetters(index = readInventory()) {
  if (index.schema_version !== 1 || !Object.hasOwn(index.input_sha256 ?? {}, recordsSource))
    throw new Error('Unsupported inventory or missing validation-ledger hash.');
  if (!inventoryFreshness(index).inventory_admissible)
    throw new Error('STALE inventory: refresh separately before exporting.');
  const entries = selectEntries(index, 'validation', 'unvalidated-setters');
  const counts_by_state = countStates(entries);
  const freshness = inventoryFreshness(index);
  if (!freshness.inventory_admissible)
    throw new Error('Inputs changed during export preparation; no report written.');
  const directory = resolve(root, 'docs/review');
  if (relative(realpathSync(root), realpathSync(directory)) !== join('docs', 'review'))
    throw new Error('Report directory must resolve to this repository docs/review directory.');
  const report = JSON.stringify({ report: 'unvalidated-setters', tier: 'inventory',
    exported_at: new Date().toISOString(), ...freshness, records_source: recordsSource,
    predicate: 'operation starts api.set: and state is not changed_value_tested',
    total: entries.length, counts_by_state, entries }, null, 2) + '\n';
  // ponytail: one fixed report; add another selector only for a demonstrated report need.
  const path = join(directory, `unvalidated-setters-${randomUUID()}.json`);
  writeFileSync(path, report, { encoding: 'utf8', flag: 'wx' });
  return { report: 'unvalidated-setters', tier: 'inventory', ...freshness,
    path, total: entries.length, counts_by_state, bytes: Buffer.byteLength(report),
    sha256: createHash('sha256').update(report).digest('hex') };
}

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  try {
    if (process.argv.length !== 3 || process.argv[2] !== 'unvalidated-setters')
      throw new Error('Use export-report.mjs unvalidated-setters. No paths, flags or other selectors.');
    process.stdout.write(JSON.stringify(exportUnvalidatedSetters()) + '\n');
  } catch (error) {
    process.stderr.write(JSON.stringify({ error: String(error.message).slice(0, 500) }) + '\n');
    process.exitCode = 1;
  }
}
