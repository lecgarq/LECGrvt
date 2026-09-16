import { readFileSync, writeFileSync, readdirSync } from 'node:fs';
import { createHash } from 'node:crypto';
import { resolve, join, dirname, relative } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '../..');
const evidence = join(root, 'docs/review/setter-validation-gate');
const runName = process.argv[2];
const mode = process.argv[3] ?? 'setter';
if (!/^\d{8}T\d{6}$/.test(runName ?? '') || !['setter', 'accessor'].includes(mode))
  throw new Error('Use export-project-presence.mjs <run-name> [setter|accessor].');
const config = mode === 'accessor'
  ? { runKind: 'project-accessor-presence-runs', receiptRoot: 'ProjectAccessorPresence',
      manifest: 'project-accessor-presence-manifest.json', inventory: 'accessor-inventory.json',
      operations: 2216, types: 243 }
  : { runKind: 'project-type-presence-runs', receiptRoot: 'ProjectTypePresence',
      manifest: 'project-type-presence-manifest.json', inventory: 'setter-inventory.json',
      operations: 805, types: 181 };
const run = join(evidence, config.runKind, runName);
const inventoryPath = join(root, 'outputs/validation-expansion', config.inventory);
const output = join(root, 'RevitCopilot/Agent/Knowledge/revit2026-project-presence.json');
const read = path => JSON.parse(readFileSync(path, 'utf8').replace(/^\uFEFF/, ''));
const hash = path => createHash('sha256').update(readFileSync(path)).digest('hex').toUpperCase();
const require = (ok, reason) => { if (!ok) throw new Error(reason); };
const modelInfo = new Map([
  ['LECG_RVT_DISEÑO (ANTEPROYECTO).rvt', 'architecture'],
  ['LECG_RVT_CONTEXTO.rvt', 'topography'],
  ['LECG_RVT_ESTRUCTURAL.rvt', 'structure'],
  ['LECG_RVT_MEP.rvt', 'mep']
]);
const order = ['architecture', 'topography', 'structure', 'mep'];

const manifest = read(join(evidence, config.manifest));
const inventory = read(inventoryPath);
require(inventory.length === config.operations
  && hash(inventoryPath) === (manifest.inventory_sha256 ?? manifest.setter_inventory_sha256),
  'Operation inventory differs from the frozen manifest.');
const receiptPaths = readdirSync(join(run, config.receiptRoot), { withFileTypes: true })
  .filter(item => item.isDirectory()).map(item => join(run, config.receiptRoot, item.name, 'receipt.json'));
require(receiptPaths.length === 4, 'Require exactly four project presence receipts.');
const receipts = receiptPaths.map(path => ({ path, value: read(path) }))
  .sort((a, b) => order.indexOf(modelInfo.get(a.value.model)) - order.indexOf(modelInfo.get(b.value.model)));
for (const { value } of receipts) {
  require(modelInfo.has(value.model) && value.status === 'read-only-profile' && value.setter_attempted === false
    && value.cleanup_verified === true && value.declaring_type_count === config.types && value.types.length === config.types,
  `Invalid read-only presence receipt: ${value.model}`);
  const expected = manifest.models.find(model => model.name === value.model);
  require(expected?.sha256 === value.model_sha256, `Model hash differs: ${value.model}`);
}
const typeCounts = receipts.map(({ value }) => new Map(value.types.map(item => [item.declaring_type, item.target_count])));
const entries = inventory.map(item => ({
  operation: item.operation,
  counts: typeCounts.map(counts => counts.get(item.declaring_type) ?? 0)
}));
const digest = receipts.map(({ path }) => `${relative(root, path).replaceAll('\\', '/')}:${hash(path)}`).join('\n');
const pack = {
  schema_version: 1,
  revit_version: 2026,
  inventory_sha256: hash(inventoryPath),
  source_receipts_sha256: createHash('sha256').update(digest).digest('hex').toUpperCase(),
  scope: `Read-only declaring-type presence for ${mode} operations in four named CASA EUCALIPTO disposable copies; target counts do not establish accessor validity or universal applicability.`,
  operation_count: entries.length,
  models: receipts.map(({ value }) => ({ discipline: modelInfo.get(value.model), model: value.model,
    model_sha256: value.model_sha256, element_count: value.element_count })),
  entries
};
writeFileSync(output, JSON.stringify(pack, null, 2) + '\n');
console.log(JSON.stringify({ output: relative(root, output).replaceAll('\\', '/'), operations: entries.length,
  models: pack.models.length, source_receipts_sha256: pack.source_receipts_sha256 }));
