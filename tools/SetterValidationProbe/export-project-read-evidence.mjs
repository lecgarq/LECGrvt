import { readFileSync, writeFileSync, readdirSync } from 'node:fs';
import { createHash } from 'node:crypto';
import { resolve, join, dirname, relative } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '../..');
const evidence = join(root, 'docs/review/setter-validation-gate');
const runName = process.argv[2];
if (!/^\d{8}T\d{6}$/.test(runName ?? '')) throw new Error('Use export-project-read-evidence.mjs <run-name>.');
const run = join(evidence, 'project-accessor-read-runs', runName, 'ProjectAccessorRead');
const output = join(root, 'RevitCopilot/Agent/Knowledge/revit2026-project-reads.json');
const read = path => JSON.parse(readFileSync(path, 'utf8').replace(/^\uFEFF/, ''));
const hash = path => createHash('sha256').update(readFileSync(path)).digest('hex').toUpperCase();
const require = (ok, reason) => { if (!ok) throw new Error(reason); };
const disciplines = new Map([
  ['LECG_RVT_DISEÑO (ANTEPROYECTO).rvt', 'architecture'],
  ['LECG_RVT_CONTEXTO.rvt', 'topography'],
  ['LECG_RVT_ESTRUCTURAL.rvt', 'structure'],
  ['LECG_RVT_MEP.rvt', 'mep']
]);
const order = ['architecture', 'topography', 'structure', 'mep'];
const manifest = read(join(evidence, 'project-accessor-read-manifest.json'));
const paths = readdirSync(run, { withFileTypes: true }).filter(item => item.isDirectory())
  .map(item => join(run, item.name, 'receipt.json'));
require(paths.length === 4, 'Require exactly four project read receipts.');
const receipts = paths.map(path => ({ path, value: read(path) }))
  .sort((a, b) => order.indexOf(disciplines.get(a.value.model)) - order.indexOf(disciplines.get(b.value.model)));
for (const { value } of receipts) {
  require(disciplines.has(value.model) && value.status === 'read-only-accessor-validation'
    && value.setter_attempted === false && value.cleanup_verified === true
    && value.read_operation_count === 1411 && value.reads.length === 1411
    && new Set(value.reads.map(row => row.operation)).size === 1411
    && value.reads.every(row => ['read_succeeded', 'context_unsupported', 'missing_fixture'].includes(row.status)
      && (row.status === 'missing_fixture' ? row.target_count === 0 && row.attempts === 0
        : row.target_count > 0 && row.attempts > 0)
      && (row.status !== 'context_unsupported' || row.reasons?.[0])),
  `Invalid read-validation receipt: ${value.model}`);
  require(manifest.models.find(model => model.name === value.model)?.sha256 === value.model_sha256,
    `Model hash differs: ${value.model}`);
}
const maps = receipts.map(({ value }) => new Map(value.reads.map(row => [row.operation, row])));
const operations = [...maps[0].keys()].sort();
require(operations.length === 1411 && maps.every(map => operations.every(operation => map.has(operation))),
  'Read operation sets differ across models.');
const entries = operations.map(operation => ({ operation, contexts: maps.map(map => {
  const row = map.get(operation);
  return { status: row.status, target_count: row.target_count, attempts: row.attempts,
    reason: row.reasons?.[0] ?? null };
}) }));
const digest = receipts.map(({ path }) => `${relative(root, path).replaceAll('\\', '/')}:${hash(path)}`).join('\n');
const pack = {
  schema_version: 1, revit_version: 2026,
  copilot_assembly_sha256: manifest.copilot_assembly_sha256,
  source_receipts_sha256: createHash('sha256').update(digest).digest('hex').toUpperCase(),
  scope: 'Production API getter path executed against compatible elements in four named CASA EUCALIPTO disposable copies; outcomes are context evidence, not universal applicability.',
  operation_count: entries.length, context_count: entries.length * receipts.length,
  models: receipts.map(({ value }) => ({ discipline: disciplines.get(value.model), model: value.model,
    model_sha256: value.model_sha256, element_count: value.element_count })),
  entries
};
writeFileSync(output, JSON.stringify(pack, null, 2) + '\n');
console.log(JSON.stringify({ output: relative(root, output).replaceAll('\\', '/'), operations: pack.operation_count,
  contexts: pack.context_count, source_receipts_sha256: pack.source_receipts_sha256 }));
