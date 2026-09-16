import { readFileSync, writeFileSync, readdirSync } from 'node:fs';
import { createHash } from 'node:crypto';
import { resolve, join, dirname, relative } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '../..');
const evidence = join(root, 'docs/review/setter-validation-gate');
const runName = process.argv[2];
if (!/^\d{8}T\d{6}$/.test(runName ?? '')) throw new Error('Use export-project-native-preview-evidence.mjs <run-name>.');
const run = join(evidence, 'project-native-preview-runs', runName, 'ProjectNativePreview');
const output = join(root, 'RevitCopilot/Agent/Knowledge/revit2026-native-project-previews.json');
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
const manifest = read(join(evidence, 'project-native-preview-manifest.json'));
const paths = readdirSync(run, { withFileTypes: true }).filter(item => item.isDirectory())
  .map(item => join(run, item.name, 'receipt.json'));
require(paths.length === 4, 'Require exactly four native-preview receipts.');
const receipts = paths.map(path => ({ path, value: read(path) }))
  .sort((a, b) => order.indexOf(disciplines.get(a.value.model)) - order.indexOf(disciplines.get(b.value.model)));
for (const { value } of receipts) {
  require(disciplines.has(value.model) && value.status === 'preview-only-native-validation'
    && value.setter_attempted === true && value.copy_removed === true && value.cleanup_verified === true
    && value.rollback_control_verified === true && value.preview_operation_count === 17
    && value.previews.length === 17 && new Set(value.previews.map(row => row.operation)).size === 17
    && value.previews.every(row => ['preview_succeeded', 'missing_fixture'].includes(row.status)
      && (row.status === 'missing_fixture' ? row.attempts === 0 : row.attempts > 0 && row.target_kind)),
  `Invalid native-preview receipt: ${value.model}`);
  require(manifest.models.find(model => model.name === value.model)?.sha256 === value.model_sha256,
    `Model hash differs: ${value.model}`);
}
const maps = receipts.map(({ value }) => new Map(value.previews.map(row => [row.operation, row])));
const operations = [...maps[0].keys()].sort();
require(operations.length === 17 && maps.every(map => operations.every(operation => map.has(operation))),
  'Native-preview operation sets differ across models.');
const fixtureKind = 'Autodesk.Revit.DB.DirectShape(test_fixture)';
const entries = operations.map(operation => ({ operation, contexts: maps.map(map => {
  const row = map.get(operation);
  return { status: row.status, attempts: row.attempts, target_kind: row.target_kind ?? null,
    synthetic_fixture_used: row.target_kind === fixtureKind, reason: row.reasons?.[0] ?? null };
}) }));
const digest = receipts.map(({ path }) => `${relative(root, path).replaceAll('\\', '/')}:${hash(path)}`).join('\n');
const pack = {
  schema_version: 1, revit_version: 2026,
  copilot_assembly_sha256: manifest.copilot_assembly_sha256,
  source_receipts_sha256: createHash('sha256').update(digest).digest('hex').toUpperCase(),
  scope: 'Production MCP change preview path executed in four named CASA EUCALIPTO disposable copies; committed inner transactions were rolled back by a transaction group, apply was never called, no returned model values were retained, and synthetic delete fixtures are explicitly flagged.',
  operation_count: entries.length, context_count: entries.length * receipts.length,
  models: receipts.map(({ value }) => ({ discipline: disciplines.get(value.model), model: value.model,
    model_sha256: value.model_sha256, element_count: value.element_count,
    synthetic_delete_fixture_available: value.synthetic_delete_fixture === true })),
  entries
};
writeFileSync(output, JSON.stringify(pack, null, 2) + '\n');
console.log(JSON.stringify({ output: relative(root, output).replaceAll('\\', '/'), operations: pack.operation_count,
  contexts: pack.context_count, source_receipts_sha256: pack.source_receipts_sha256 }));
