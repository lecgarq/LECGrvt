import { readFileSync, writeFileSync, readdirSync } from 'node:fs';
import { createHash } from 'node:crypto';
import { resolve, join, dirname, relative, basename } from 'node:path';
import { fileURLToPath } from 'node:url';
import { checkAttempts } from './reconcile-dedicated.mjs';
import { receiptAttempt } from './reconcile-pilot.mjs';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '../..');
const evidence = join(root, 'docs/review/setter-validation-gate');
const read = path => JSON.parse(readFileSync(path, 'utf8').replace(/^\uFEFF/, ''));
const hash = path => createHash('sha256').update(readFileSync(path)).digest('hex').toUpperCase();
const require = (ok, reason) => { if (!ok) throw new Error(reason); };
const same = (a, b) => JSON.stringify(a) === JSON.stringify(b);
const portable = path => relative(root, path).replaceAll('\\', '/');
const reference = path => ({ path: portable(path), sha256: hash(path) });

export function promoteValidated(original, receipt, priorPassedModelHashes = new Set()) {
  receiptAttempt(receipt);
  require(receipt.status === 'validated', 'Only a validated receipt can promote a ledger entry.');
  if (priorPassedModelHashes.has(receipt.model_sha256)) return structuredClone(original);
  require(receipt.target_count > 0, 'Validated receipt does not prove a matching target in this model.');
  require(original.context_failures === 0,
    'Aggregate history cannot identify the prior model outcome when context failures exist.');
  require(original.same_value_models > 0, 'No same-value model is available for exact promotion.');
  const updated = structuredClone(original);
  updated.passed_models += 1;
  updated.same_value_models -= 1;
  updated.state = 'changed_value_tested';
  return updated;
}

function reconcile(runName, trxName) {
  require(/^\d{8}T\d{6}-[a-f\d]{32}$/.test(runName ?? ''), 'Provide an exact class-collector run name.');
  require(/^classcollectors-\d{8}T\d{6}-[a-f\d]{32}\.trx$/.test(trxName ?? ''),
    'Provide the completed class-collector TRX filename.');
  const run = join(evidence, 'class-collector-runs', runName);
  const manifestPath = join(evidence, 'class-collector-manifest.json');
  const manifest = read(manifestPath);
  const provenancePath = join(run, 'provenance.json');
  const provenance = read(provenancePath);
  const ledgerPath = join(root, 'RevitCopilot/Agent/Knowledge/revit2026-validation.json');
  const ledger = read(ledgerPath);
  require(hash(ledgerPath) === manifest.ledger_sha256, 'Ledger changed since preregistration.');
  require(hash(manifestPath) === provenance.manifest_sha256 && provenance.revision === manifest.revision,
    'Manifest/revision differs from runtime evidence.');
  require(hash(join(evidence, 'class-collector-preregistration.md')) === manifest.preregistration_sha256,
    'Frozen protocol changed.');
  require(hash(join(evidence, 'elementid-classification.csv')) === manifest.classification_sha256,
    'Classification changed.');
  require(hash(join(evidence, 'writable-snapshot-amendment.md')) === manifest.snapshot_amendment_sha256
    && provenance.snapshot_amendment_sha256 === manifest.snapshot_amendment_sha256,
    'Snapshot amendment differs.');
  require(provenance.runtime.startsWith('10.') && provenance.binaries.some(b => b.sha256 === manifest.api_sha256),
    'Runtime/API gate evidence missing.');
  for (const source of provenance.sources) require(hash(source.path) === source.sha256,
    'Harness source changed since execution.');
  require(hash(join(root, 'tools/SetterValidationProbe/SetterValidationProbe.csproj')) === provenance.project_sha256,
    'Harness project changed since execution.');

  const models = new Map(manifest.models.map(model => [model.name, model]));
  require(models.size === 12 && new Set([...models.values()].map(model => model.sha256)).size === 12,
    'Expected 12 distinct source models.');
  for (const model of models.values()) require(hash(model.path) === model.sha256, `Source model changed: ${model.name}`);
  require(manifest.cases.length === 18 && new Set(manifest.cases.map(item => item.property)).size === 18,
    'Expected 18 distinct frozen cases.');

  const trxPath = join(evidence, trxName);
  const trx = readFileSync(trxPath, 'utf8');
  const counters = trx.match(/<Counters\s+([^>]+)\/>/)?.[1] ?? '';
  require(/\btotal="18"/.test(counters) && /\bpassed="18"/.test(counters) && /\bfailed="0"/.test(counters)
    && trx.includes('ClassCollectorBatch'), 'TRX does not establish 18 completed harness cases.');

  const previousRun = ledger.campaign.replace(/^changed-value-dedicated-/, '');
  require(previousRun !== ledger.campaign, 'Ledger does not identify the expected prior campaign.');
  const previousReportPath = join(evidence, 'dedicated-runs', previousRun, 'ledger-reconciliation.json');
  require(hash(previousReportPath) === ledger.source_sha256, 'Prior ledger reconciliation differs.');
  const previousReport = read(previousReportPath);
  const pilotRun = basename(dirname(previousReport.previous_reconciliation.path));
  const pilotReportPath = join(evidence, 'pilot-runs', pilotRun, 'ledger-reconciliation.json');
  require(hash(pilotReportPath) === previousReport.previous_reconciliation.sha256,
    'Prior pilot reconciliation differs.');
  const pilotReport = read(pilotReportPath);
  const priorPassedByOperation = new Map();
  for (const update of pilotReport.updates.filter(item => item.after.passed_models > item.before.passed_models)) {
    const localReceipt = join(evidence, 'pilot-runs', pilotRun,
      basename(dirname(update.receipt)), 'receipt.json');
    require(hash(localReceipt) === update.receipt_sha256, 'Prior validating receipt differs.');
    const receipt = read(localReceipt);
    if (!priorPassedByOperation.has(receipt.operation)) priorPassedByOperation.set(receipt.operation, new Set());
    priorPassedByOperation.get(receipt.operation).add(receipt.model_sha256);
  }

  const caseDirectories = readdirSync(run, { withFileTypes: true }).filter(item => item.isDirectory())
    .map(item => join(run, item.name));
  require(caseDirectories.length === 18, 'Run does not contain exactly 18 case directories.');
  const summaries = caseDirectories.map(path => ({ path, value: read(join(path, 'case-summary.json')) }));
  require(new Set(summaries.map(item => item.value.operation)).size === 18, 'Duplicate or missing case summary.');

  const proposed = structuredClone(ledger);
  const updates = [], outcomes = [];
  for (const planItem of manifest.cases) {
    const plan = { ...planItem, operation: 'api.set:Autodesk.Revit.DB.' + planItem.property };
    const located = summaries.find(item => item.value.operation === plan.operation);
    require(located, `Missing summary: ${plan.operation}`);
    const summary = located.value;
    require(summary.models_planned === plan.models.length && summary.models_attempted === summary.attempts.length,
      'Case summary differs from frozen plan.');
    const receiptFiles = readdirSync(located.path, { withFileTypes: true }).filter(item => item.isDirectory())
      .map(item => join(located.path, item.name, 'receipt.json'));
    const receiptsByModel = new Map(receiptFiles.map(path => {
      const value = read(path);
      require(!value.copy || dirname(value.copy) === dirname(path), 'Receipt copy path differs from evidence directory.');
      return [value.model, { path, value }];
    }));
    require(receiptsByModel.size === receiptFiles.length && receiptFiles.length === summary.attempts.length,
      'Unaccounted or duplicate model receipt.');
    const receipts = summary.attempts.map(attempt => {
      const found = receiptsByModel.get(attempt.model);
      require(found, `Missing receipt for ${attempt.model}`);
      require(found.value.model_sha256 === models.get(attempt.model)?.sha256,
        'Receipt model hash differs from frozen corpus.');
      require(found.value.status === attempt.status && found.value.reason === attempt.reason,
        'Summary differs from receipt.');
      return found;
    });
    checkAttempts(plan, receipts.map(item => item.value));

    const original = ledger.entries.find(entry => entry.operation === plan.operation);
    require(original && ['same_value_only', 'changed_value_tested'].includes(original.state),
      'Class-collector case is outside its preregistered ledger phase.');
    let updated = structuredClone(original);
    const validation = receipts.find(item => item.value.status === 'validated');
    if (validation) updated = promoteValidated(original, validation.value,
      priorPassedByOperation.get(plan.operation) ?? new Set());
    proposed.entries[proposed.entries.findIndex(entry => entry.operation === plan.operation)] = updated;
    updates.push({ before: original, after: updated,
      prior_model_classification: validation && !same(original, updated)
        ? 'same_value_only: matching target_count > 0; prior aggregate has zero context failures; prior passed model hashes checked'
        : 'unchanged',
      receipts: receipts.map(item => reference(item.path)) });
    outcomes.push({ operation: plan.operation, status: validation ? 'validated' : 'rejected-with-reason',
      attempts: receipts.map(item => ({ model: item.value.model, status: item.value.status,
        reason: item.value.reason, setter_attempted: item.value.setter_attempted, ...reference(item.path) })) });
  }

  const setters = proposed.entries.filter(entry => entry.operation.startsWith('api.set:'));
  require(setters.length === 805 && proposed.entries.length === ledger.entries.length, 'Denominator/identity count changed.');
  require(updates.every(item => item.after.passed_models + item.after.context_failures
    + item.after.unsupported_models + item.after.same_value_models === 12),
    'A touched ledger entry no longer accounts for exactly 12 source models.');
  const originalNumerator = ledger.entries.filter(entry => entry.operation.startsWith('api.set:')
    && entry.state === 'changed_value_tested').length;
  const numerator = setters.filter(entry => entry.state === 'changed_value_tested').length;
  require(numerator - originalNumerator === 12, 'Expected exactly 12 new setter identities.');
  const countsByState = Object.fromEntries([...new Set(setters.map(entry => entry.state))].sort()
    .map(state => [state, setters.filter(entry => entry.state === state).length]));
  const report = { run: runName, revision: manifest.revision, original_ledger_sha256: manifest.ledger_sha256,
    previous_reconciliation: reference(previousReportPath), provenance: reference(provenancePath),
    trx: reference(trxPath), manifest: reference(manifestPath), original_numerator: originalNumerator,
    proposed_numerator: numerator, new_validations: numerator - originalNumerator, denominator: 805,
    counts_by_state: countsByState, updates };
  const reportText = JSON.stringify(report, null, 2) + '\n';
  proposed.campaign = 'changed-value-class-collectors-' + runName;
  proposed.source_sha256 = createHash('sha256').update(reportText).digest('hex').toUpperCase();
  proposed.scope += ` ${report.new_validations} further direct-API setter validations from the class-collector batch; source_sha256 identifies its ledger-reconciliation.json. One strongest outcome per model hash; every attempted write rolled back and every disposable copy was closed without saving.`;
  const results = { report: 'class-collector-18', total: outcomes.length,
    receipt_attempts: outcomes.reduce((sum, item) => sum + item.attempts.length, 0),
    counts_by_outcome: { validated: outcomes.filter(item => item.status === 'validated').length,
      'rejected-with-reason': outcomes.filter(item => item.status === 'rejected-with-reason').length },
    new_validations: report.new_validations, entries: outcomes };
  for (const [name, value] of [['ledger-proposal.json', proposed], ['ledger-reconciliation.json', report],
    ['class-collector-results.json', results]])
    writeFileSync(join(run, name), JSON.stringify(value, null, 2) + '\n', { flag: 'wx' });
  console.log(JSON.stringify({ numerator, new_validations: report.new_validations, denominator: 805,
    counts_by_state: countsByState, report: portable(join(run, 'ledger-reconciliation.json')) }));
}

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  try {
    require(process.argv.length === 4,
      'Use reconcile-class-collectors.mjs <run-name> <trx-filename>.');
    reconcile(...process.argv.slice(2));
  } catch (error) { console.error(error.message); process.exitCode = 1; }
}
