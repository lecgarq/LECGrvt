import { readFileSync, writeFileSync, readdirSync } from 'node:fs';
import { createHash } from 'node:crypto';
import { resolve, join, dirname, relative } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '../..');
const evidence = join(root, 'docs/review/setter-validation-gate');
const ledgerPath = join(root, 'RevitCopilot/Agent/Knowledge/revit2026-validation.json');
const output = join(root, 'RevitCopilot/Agent/Knowledge/revit2026-validation-contexts.json');
const read = path => JSON.parse(readFileSync(path, 'utf8').replace(/^\uFEFF/, ''));
const hashBytes = bytes => createHash('sha256').update(bytes).digest('hex').toUpperCase();
const hash = path => hashBytes(readFileSync(path));
const require = (ok, reason) => { if (!ok) throw new Error(reason); };
const models = new Map([
  ['LECG_RVT_DISEÑO (ANTEPROYECTO).rvt', ['architecture', '1ABDE76F3C94BB2401FB1FA537FA88759A717963D271560211FDEB6D4234B39A']],
  ['LECG_RVT_CONTEXTO.rvt', ['topography', 'DA4F85A71366B37BF338276146AE99611163EB49D070EC7615E327295CE129E0']],
  ['LECG_RVT_ESTRUCTURAL.rvt', ['structure', 'BCFF2E2CA20174C8D89734150E0A4D7110B9DED50E7CC523265D159A90F35B02']],
  ['LECG_RVT_MEP.rvt', ['mep', '55C7A010C8C19630B7EC9816BBFF78FFABF46237EB88BF2AF387CEA8B5539201']]
]);
const ranks = { 'rejected-with-reason': 1, 'still-same-value': 2, validated: 3 };
const disciplineOrder = new Map(['architecture', 'topography', 'structure', 'mep'].map((value, index) => [value, index]));
const viewSheetSetWorkflow = 'ViewSheetSetting.CurrentViewSheetSet -> set property -> ViewSheetSetting.Save()';

function receipts(path) {
  const found = [];
  for (const item of readdirSync(path, { withFileTypes: true })) {
    const child = join(path, item.name);
    if (item.isDirectory()) found.push(...receipts(child));
    else if (item.name === 'receipt.json') found.push(child);
  }
  return found;
}

const ledgerBytes = readFileSync(ledgerPath);
const ledger = JSON.parse(ledgerBytes.toString('utf8').replace(/^\uFEFF/, ''));
const operations = new Set(ledger.entries.map(entry => entry.operation));
const selected = new Map();
const workflows = new Map();
for (const path of receipts(evidence).sort()) {
  const value = read(path);
  const model = models.get(value.model);
  if (!model || !operations.has(value.operation) || !value.cleanup_verified || !Object.hasOwn(ranks, value.status)) continue;
  require(value.model_sha256 === model[1], `Project model hash differs: ${path}`);
  const key = `${value.operation}\u0000${value.model_sha256}`;
  if (value.classification_only === true && value.persistence_probe_changed === true
      && value.view_sheet_setting_save === true) workflows.set(key, { path, value });
  const prior = selected.get(key);
  if (!prior || ranks[value.status] >= ranks[prior.value.status]) selected.set(key, { path, value, discipline: model[0] });
}

const byOperation = new Map();
for (const item of selected.values()) {
  const context = {
    discipline: item.discipline,
    model: item.value.model,
    model_sha256: item.value.model_sha256,
    status: item.value.status,
    reason: item.value.reason ? String(item.value.reason).slice(0, 600) : null,
    compound_workflow_validated: workflows.has(`${item.value.operation}\u0000${item.value.model_sha256}`),
    required_workflow: workflows.has(`${item.value.operation}\u0000${item.value.model_sha256}`) ? viewSheetSetWorkflow : null
  };
  if (!byOperation.has(item.value.operation)) byOperation.set(item.value.operation, []);
  byOperation.get(item.value.operation).push(context);
}
const entries = [...byOperation].sort(([a], [b]) => a.localeCompare(b)).map(([operation, contexts]) => ({
  operation,
  contexts: contexts.sort((a, b) => disciplineOrder.get(a.discipline) - disciplineOrder.get(b.discipline))
}));
const receiptSources = new Map([...selected.values(), ...workflows.values()].map(item => [item.path, item]));
const receiptDigest = [...receiptSources.values()].sort((a, b) => a.path.localeCompare(b.path))
  .map(item => `${relative(root, item.path).replaceAll('\\', '/')}:${hash(item.path)}`).join('\n');
const pack = {
  schema_version: 1,
  revit_version: 2026,
  campaign: ledger.campaign,
  validation_ledger_sha256: hashBytes(ledgerBytes),
  source_receipts_sha256: hashBytes(receiptDigest),
  scope: 'Named CASA EUCALIPTO discipline fixtures; disposable-copy outcomes are context evidence, not universal guarantees.',
  receipt_count: selected.size,
  workflow_receipt_count: workflows.size,
  entries
};
writeFileSync(output, JSON.stringify(pack, null, 2) + '\n');
console.log(JSON.stringify({ output: relative(root, output).replaceAll('\\', '/'),
  operations: entries.length, contexts: selected.size, workflows: workflows.size, ledger_campaign: ledger.campaign }));
