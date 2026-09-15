import { readFileSync, writeFileSync } from 'node:fs';
import { createHash } from 'node:crypto';
import { resolve, join, dirname, relative } from 'node:path';
import { fileURLToPath } from 'node:url';
import { appendProjectAttempt, completedTrx, evidenceForPlan,
  filesWithExtension, total } from './reconcile-project-corpus.mjs';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '../..');
const evidence = join(root, 'docs/review/setter-validation-gate');
const ledgerPath = join(root, 'RevitCopilot/Agent/Knowledge/revit2026-validation.json');
const read = path => JSON.parse(readFileSync(path, 'utf8').replace(/^\uFEFF/, ''));
const hash = path => createHash('sha256').update(readFileSync(path)).digest('hex').toUpperCase();
const require = (ok, reason) => { if (!ok) throw new Error(reason); };
const same = (a, b) => JSON.stringify(a) === JSON.stringify(b);
const portable = path => relative(root, path).replaceAll('\\', '/');
const reference = path => ({ path: portable(path), sha256: hash(path) });

export function reconcileProjectBatch(batchName, runName, trxName) {
  require(/^project-[a-z-]+$/.test(batchName ?? ''), 'Provide the project batch name.');
  require(/^\d{8}T\d{6}$/.test(runName ?? ''), 'Provide the exact run name.');
  const trxPrefix = batchName.replaceAll('-', '');
  require(new RegExp(`^${trxPrefix}-\\d{8}T\\d{6}\\.trx$`).test(trxName ?? ''), 'Provide the batch TRX.');
  const manifestPath = join(evidence, `${batchName}-manifest.json`);
  const manifest = read(manifestPath);
  const run = join(evidence, `${batchName}-runs`, runName);
  const provenancePath = join(run, 'provenance.json');
  const provenance = read(provenancePath);
  const ledger = read(ledgerPath);
  require(hash(ledgerPath) === manifest.ledger_sha256, 'Ledger changed since preregistration.');
  require(hash(manifestPath) === provenance.manifest_sha256 && manifest.revision === provenance.revision,
    'Manifest/revision differs from runtime evidence.');
  require(hash(join(evidence, `${batchName}-preregistration.md`)) === manifest.preregistration_sha256,
    'Preregistration changed.');
  const parent = manifest.source_manifests[0];
  require(hash(join(evidence, parent.path)) === parent.sha256, 'Parent manifest changed.');
  const previousReportPath = join(evidence, manifest.previous_reconciliation.path);
  require(hash(previousReportPath) === manifest.previous_reconciliation.sha256
    && ledger.source_sha256 === manifest.previous_reconciliation.sha256,
  'Previous ledger provenance differs.');
  require(provenance.runtime.startsWith('10.')
    && provenance.binaries.some(binary => binary.sha256 === manifest.api_sha256),
  'Runtime/API binary evidence missing.');
  require(manifest.models.length >= 1 && manifest.models.every(model => model.relative_path),
    'Expected project fixtures with relative paths.');
  const fixtureRoot = process.env.LECG_PROJECT_FIXTURE_ROOT;
  require(fixtureRoot, 'LECG_PROJECT_FIXTURE_ROOT is required to verify project sources.');
  const sources = manifest.models.map(model => {
    const path = resolve(fixtureRoot, model.relative_path);
    require(!relative(resolve(fixtureRoot), path).split(/[\\/]/).includes('..')
      && hash(path) === model.sha256, `Project source changed: ${model.name}`);
    return { name: model.name, sha256: model.sha256 };
  });
  const expectedCases = manifest.cases.length;
  require(expectedCases >= 1 && manifest.baseline_entries.length === expectedCases
    && manifest.baseline_entries.every(entry => same(entry,
      ledger.entries.find(item => item.operation === entry.operation))),
  'Cases/baselines differ from the frozen ledger.');

  const trxPath = join(evidence, trxName);
  completedTrx(trxPath, expectedCases, manifest.test_class);
  const planned = evidenceForPlan(run, manifest, expectedCases);
  const proposed = structuredClone(ledger);
  const updates = [];
  const outcomes = [];
  for (const item of planned) {
    const index = proposed.entries.findIndex(entry => entry.operation === item.plan.operation);
    require(index >= 0, `Missing ledger entry: ${item.plan.operation}`);
    const before = structuredClone(proposed.entries[index]);
    for (const receipt of item.receipts)
      proposed.entries[index] = appendProjectAttempt(proposed.entries[index], receipt.value);
    require(total(proposed.entries[index]) === total(before) + item.receipts.length,
      `Project attempts did not reconcile: ${item.plan.operation}`);
    updates.push({ before, after: proposed.entries[index],
      receipts: item.receipts.map(receipt => reference(receipt.path)) });
    outcomes.push({ operation: item.plan.operation, attempts: item.receipts.map(receipt => ({
      model: receipt.value.model, status: receipt.value.status, reason: receipt.value.reason,
      receipt: reference(receipt.path) })) });
  }
  require(filesWithExtension(run, '.rvt').length === 0, 'Disposable copies remain on disk.');

  const setters = proposed.entries.filter(entry => entry.operation.startsWith('api.set:'));
  const originalNumerator = ledger.entries.filter(entry => entry.operation.startsWith('api.set:')
    && entry.state === 'changed_value_tested').length;
  const numerator = setters.filter(entry => entry.state === 'changed_value_tested').length;
  const newValidations = numerator - originalNumerator;
  require(setters.length === 805 && newValidations >= manifest.expected_yield_min
    && newValidations <= manifest.expected_yield_max,
  'Denominator or preregistered validation yield changed.');
  const countsByState = Object.fromEntries([...new Set(setters.map(entry => entry.state))].sort()
    .map(state => [state, setters.filter(entry => entry.state === state).length]));
  const report = { run: runName, original_ledger_sha256: manifest.ledger_sha256,
    previous_reconciliation: reference(previousReportPath), manifest: reference(manifestPath),
    provenance: reference(provenancePath), trx: reference(trxPath), sources,
    remaining_rvt_files: 0, original_numerator: originalNumerator, proposed_numerator: numerator,
    new_validations: newValidations, denominator: 805, counts_by_state: countsByState, updates, outcomes };
  const reportText = JSON.stringify(report, null, 2) + '\n';
  proposed.campaign = manifest.campaign_prefix + '-' + runName;
  proposed.source_sha256 = createHash('sha256').update(reportText).digest('hex').toUpperCase();
  proposed.scope += ` ${planned.reduce((sum, item) => sum + item.receipts.length, 0)} supplemental architecture-project outcomes from detached disposable copies. `;
  const results = { report: `${batchName}-${expectedCases}`, new_validations: newValidations, entries: outcomes };
  for (const [name, value] of [['ledger-proposal.json', proposed], ['ledger-reconciliation.json', report],
    [`${batchName}-results.json`, results]])
    writeFileSync(join(run, name), JSON.stringify(value, null, 2) + '\n', { flag: 'wx' });
  console.log(JSON.stringify({ numerator, new_validations: newValidations, denominator: 805,
    counts_by_state: countsByState, report: portable(join(run, 'ledger-reconciliation.json')) }));
}

try {
  require(process.argv.length === 5,
    'Use reconcile-project-architecture.mjs <project-batch-name> <run-name> <trx-filename>.');
  reconcileProjectBatch(...process.argv.slice(2));
} catch (error) { console.error(error.message); process.exitCode = 1; }
