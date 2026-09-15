import { readFileSync, writeFileSync, readdirSync } from 'node:fs';
import { createHash } from 'node:crypto';
import { resolve, join, dirname, relative, basename, extname } from 'node:path';
import { fileURLToPath } from 'node:url';
import { checkAttempts } from './reconcile-dedicated.mjs';
import { promoteWithinCorpus } from './reconcile-project-corpus.mjs';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '../..');
const evidence = join(root, 'docs/review/setter-validation-gate');
const ledgerPath = join(root, 'RevitCopilot/Agent/Knowledge/revit2026-validation.json');
const read = path => JSON.parse(readFileSync(path, 'utf8').replace(/^\uFEFF/, ''));
const hash = path => createHash('sha256').update(readFileSync(path)).digest('hex').toUpperCase();
const require = (ok, reason) => { if (!ok) throw new Error(reason); };
const same = (a, b) => JSON.stringify(a) === JSON.stringify(b);
const portable = path => relative(root, path).replaceAll('\\', '/');
const reference = path => ({ path: portable(path), sha256: hash(path) });
const total = entry => entry.passed_models + entry.context_failures
  + entry.unsupported_models + entry.same_value_models;

function hasRvt(path) {
  return readdirSync(path, { withFileTypes: true }).some(item => item.isDirectory()
    ? hasRvt(join(path, item.name)) : extname(item.name).toLowerCase() === '.rvt');
}

function reconcile(runName, trxName) {
  require(/^\d{8}T\d{6}$/.test(runName ?? ''), 'Provide the exact retry run name.');
  require(/^documentedretry-\d{8}T\d{6}\.trx$/.test(trxName ?? ''), 'Provide the retry TRX.');
  const manifestPath = join(evidence, 'documented-retry-manifest.json');
  const manifest = read(manifestPath);
  const run = join(evidence, 'documented-retry-runs', runName);
  const provenancePath = join(run, 'provenance.json');
  const provenance = read(provenancePath);
  const ledger = read(ledgerPath);
  require(hash(ledgerPath) === manifest.ledger_sha256, 'Ledger changed since retry preregistration.');
  require(hash(manifestPath) === provenance.manifest_sha256 && manifest.revision === provenance.revision,
    'Retry manifest/revision differs from runtime evidence.');
  require(hash(join(evidence, 'documented-retry-preregistration.md')) === manifest.preregistration_sha256,
    'Retry protocol changed.');
  require(hash(join(evidence, 'documented-constraint-manifest.json')) === manifest.parent_manifest_sha256,
    'Parent documented-constraint manifest changed.');
  require(hash(join(evidence, 'documented-20260915T212228.trx')) === manifest.failed_trx_sha256,
    'Failed parent TRX changed.');
  require(manifest.failed_run === '20260915T212311'
    && readdirSync(join(evidence, 'documented-constraint-runs', manifest.failed_run)).length > 0,
  'Failed parent run is missing.');
  require(provenance.runtime.startsWith('10.')
    && provenance.binaries.some(binary => binary.sha256 === manifest.api_sha256),
  'Runtime/API binary evidence missing.');
  require(manifest.models.length === 16 && new Set(manifest.models.map(model => model.sha256)).size === 16,
    'Expected 16 distinct frozen model hashes.');
  for (const model of manifest.models.filter(item => item.path)) require(hash(model.path) === model.sha256,
    `Autodesk source changed: ${model.name}`);
  const fixtureRoot = process.env.LECG_PROJECT_FIXTURE_ROOT;
  require(fixtureRoot, 'LECG_PROJECT_FIXTURE_ROOT is required to verify project sources.');
  for (const model of manifest.models.filter(item => item.relative_path)) {
    const path = resolve(fixtureRoot, model.relative_path);
    require(!relative(resolve(fixtureRoot), path).split(/[\\/]/).includes('..') && hash(path) === model.sha256,
      `Project source changed: ${model.name}`);
  }
  require(manifest.cases.length === 3 && manifest.baseline_entries.length === 3
    && manifest.baseline_entries.every(entry => same(entry, ledger.entries.find(item => item.operation === entry.operation))),
  'Retry cases/baselines differ from the frozen ledger.');

  const trxPath = join(evidence, trxName);
  const trx = readFileSync(trxPath, 'utf8');
  const counters = trx.match(/<Counters\s+([^>]+)\/>/)?.[1] ?? '';
  require(/\btotal="3"/.test(counters) && /\bpassed="3"/.test(counters) && /\bfailed="0"/.test(counters)
    && trx.includes('DocumentedRetryBatch'), 'TRX does not establish three completed retry cases.');
  const previousRun = ledger.campaign.match(/^changed-value-project-corpus-(\d{8}T\d{6})/)?.[1];
  require(previousRun, 'Ledger does not identify the prior project-corpus reconciliation.');
  const previousReportPath = join(evidence, 'project-corpus-runs', previousRun, 'ledger-reconciliation.json');
  require(hash(previousReportPath) === ledger.source_sha256, 'Previous ledger provenance differs.');

  const models = new Map(manifest.models.map(model => [model.name, model]));
  const caseDirectories = readdirSync(run, { withFileTypes: true }).filter(item => item.isDirectory())
    .map(item => join(run, item.name));
  require(caseDirectories.length === 3, 'Retry run must contain exactly three case directories.');
  const proposed = structuredClone(ledger);
  const updates = [];
  for (const planItem of manifest.cases) {
    const plan = { ...planItem, operation: 'api.set:Autodesk.Revit.DB.' + planItem.property };
    const caseDirectory = caseDirectories.find(path => read(join(path, 'case-summary.json')).operation === plan.operation);
    require(caseDirectory, `Missing retry case: ${plan.operation}`);
    const summary = read(join(caseDirectory, 'case-summary.json'));
    require(summary.models_planned === plan.models.length && summary.models_attempted === summary.attempts.length,
      'Retry summary differs from frozen plan.');
    const byModel = new Map(readdirSync(caseDirectory, { withFileTypes: true }).filter(item => item.isDirectory())
      .map(item => {
        const path = join(caseDirectory, item.name, 'receipt.json');
        const value = read(path);
        return [value.model, { path, value }];
      }));
    const receipts = summary.attempts.map(attempt => {
      const found = byModel.get(attempt.model);
      require(found && models.get(attempt.model)?.sha256 === found.value.model_sha256,
        'Retry receipt/model hash differs from the frozen corpus.');
      require(found.value.status === attempt.status && found.value.reason === attempt.reason,
        'Retry summary differs from its receipt.');
      return found;
    });
    require(byModel.size === receipts.length, 'Unaccounted retry receipt.');
    checkAttempts(plan, receipts.map(receipt => receipt.value));
    const validation = receipts.find(receipt => receipt.value.status === 'validated');
    require(validation, `Retry did not validate: ${plan.operation}`);
    const index = proposed.entries.findIndex(entry => entry.operation === plan.operation);
    require(index >= 0, 'Retry operation is missing from the ledger.');
    const before = proposed.entries[index];
    require(total(before) === 12, 'Retry baseline is not the original 12-model corpus.');
    proposed.entries[index] = promoteWithinCorpus(before, validation.value);
    updates.push({ before, after: proposed.entries[index], receipt: reference(validation.path) });
  }
  require(!hasRvt(run), 'Disposable retry copies remain on disk.');

  const setters = proposed.entries.filter(entry => entry.operation.startsWith('api.set:'));
  const originalNumerator = ledger.entries.filter(entry => entry.operation.startsWith('api.set:')
    && entry.state === 'changed_value_tested').length;
  const numerator = setters.filter(entry => entry.state === 'changed_value_tested').length;
  require(setters.length === 805 && numerator - originalNumerator === 3,
    'Retry denominator or expected three validations changed.');
  const countsByState = Object.fromEntries([...new Set(setters.map(entry => entry.state))].sort()
    .map(state => [state, setters.filter(entry => entry.state === state).length]));
  const report = { run: runName, failed_parent_run: manifest.failed_run,
    failed_parent_qualified: false, original_ledger_sha256: manifest.ledger_sha256,
    previous_reconciliation: reference(previousReportPath), manifest: reference(manifestPath),
    provenance: reference(provenancePath), trx: reference(trxPath), remaining_rvt_files: 0,
    original_numerator: originalNumerator, proposed_numerator: numerator,
    new_validations: 3, denominator: 805, counts_by_state: countsByState, updates };
  const reportText = JSON.stringify(report, null, 2) + '\n';
  proposed.campaign = 'changed-value-documented-retry-' + runName;
  proposed.source_sha256 = createHash('sha256').update(reportText).digest('hex').toUpperCase();
  proposed.scope += ' 3 further direct-API setter validations from the completed documented-constraint retry; the timed-out parent run is retained but did not qualify. ';
  const results = { report: 'documented-retry-3', new_validations: 3,
    entries: updates.map(update => ({ operation: update.after.operation, status: 'validated', receipt: update.receipt })) };
  for (const [name, value] of [['ledger-proposal.json', proposed], ['ledger-reconciliation.json', report],
    ['documented-retry-results.json', results]])
    writeFileSync(join(run, name), JSON.stringify(value, null, 2) + '\n', { flag: 'wx' });
  console.log(JSON.stringify({ numerator, new_validations: 3, denominator: 805,
    counts_by_state: countsByState, report: portable(join(run, 'ledger-reconciliation.json')) }));
}

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  try {
    require(process.argv.length === 4, 'Use reconcile-documented-retry.mjs <run-name> <trx-filename>.');
    reconcile(...process.argv.slice(2));
  } catch (error) { console.error(error.message); process.exitCode = 1; }
}
