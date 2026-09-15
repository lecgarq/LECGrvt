import { readFileSync, writeFileSync, readdirSync } from 'node:fs';
import { createHash } from 'node:crypto';
import { resolve, join, dirname, relative, extname } from 'node:path';
import { fileURLToPath } from 'node:url';
import { checkAttempts } from './reconcile-dedicated.mjs';
import { appendProjectAttempt, promoteWithinCorpus } from './reconcile-project-corpus.mjs';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '../..');
const evidence = join(root, 'docs/review/setter-validation-gate');
const ledgerPath = join(root, 'RevitCopilot/Agent/Knowledge/revit2026-validation.json');
const read = path => JSON.parse(readFileSync(path, 'utf8').replace(/^\uFEFF/, ''));
const hash = path => createHash('sha256').update(readFileSync(path)).digest('hex').toUpperCase();
const require = (ok, reason) => { if (!ok) throw new Error(reason); };
const same = (a, b) => JSON.stringify(a) === JSON.stringify(b);
const portable = path => relative(root, path).replaceAll('\\', '/');
const reference = path => ({ path: portable(path), sha256: hash(path) });

function hasRvt(path) {
  return readdirSync(path, { withFileTypes: true }).some(item => item.isDirectory()
    ? hasRvt(join(path, item.name)) : extname(item.name).toLowerCase() === '.rvt');
}

function reconcile(runName, trxName) {
  require(/^\d{8}T\d{6}$/.test(runName ?? ''), 'Provide the exact run name.');
  require(/^documentedfourth-\d{8}T\d{6}\.trx$/.test(trxName ?? ''), 'Provide the batch TRX.');
  const manifestPath = join(evidence, 'documented-fourth-manifest.json');
  const manifest = read(manifestPath);
  const run = join(evidence, 'documented-fourth-runs', runName);
  const provenancePath = join(run, 'provenance.json');
  const provenance = read(provenancePath);
  const ledger = read(ledgerPath);
  require(hash(ledgerPath) === manifest.ledger_sha256, 'Ledger changed since preregistration.');
  require(hash(manifestPath) === provenance.manifest_sha256 && manifest.revision === provenance.revision,
    'Manifest/revision differs from runtime evidence.');
  require(hash(join(evidence, 'documented-fourth-preregistration.md')) === manifest.preregistration_sha256,
    'Preregistration changed.');
  const parent = manifest.source_manifests[0];
  require(hash(join(evidence, parent.path)) === parent.sha256, 'Parent manifest changed.');
  require(provenance.runtime.startsWith('10.')
    && provenance.binaries.some(binary => binary.sha256 === manifest.api_sha256),
  'Runtime/API binary evidence missing.');
  for (const model of manifest.models.filter(item => item.path))
    require(hash(model.path) === model.sha256, `Autodesk source changed: ${model.name}`);
  const fixtureRoot = process.env.LECG_PROJECT_FIXTURE_ROOT;
  require(fixtureRoot, 'LECG_PROJECT_FIXTURE_ROOT is required to verify project sources.');
  for (const model of manifest.models.filter(item => item.relative_path)) {
    const path = resolve(fixtureRoot, model.relative_path);
    require(!relative(resolve(fixtureRoot), path).split(/[\\/]/).includes('..') && hash(path) === model.sha256,
      `Project source changed: ${model.name}`);
  }
  require(manifest.cases.length === 6 && manifest.baseline_entries.length === 6
    && manifest.baseline_entries.every(entry => same(entry,
      ledger.entries.find(item => item.operation === entry.operation))),
  'Cases/baselines differ from the frozen ledger.');

  const trxPath = join(evidence, trxName);
  const trx = readFileSync(trxPath, 'utf8');
  const counters = trx.match(/<Counters\s+([^>]+)\/>/)?.[1] ?? '';
  require(/\btotal="6"/.test(counters) && /\bpassed="6"/.test(counters)
    && /\bfailed="0"/.test(counters) && trx.includes('DocumentedFourthBatch'),
  'TRX does not establish six completed cases.');
  const priorRun = ledger.campaign.match(/^changed-value-documented-third-(\d{8}T\d{6})/)?.[1];
  require(priorRun, 'Ledger does not identify the prior reconciliation.');
  const previousReportPath = join(evidence, 'documented-third-runs', priorRun, 'ledger-reconciliation.json');
  require(hash(previousReportPath) === ledger.source_sha256, 'Previous ledger provenance differs.');

  const modelMap = new Map(manifest.models.map(model => [model.name, model]));
  const caseDirectories = readdirSync(run, { withFileTypes: true }).filter(item => item.isDirectory())
    .map(item => join(run, item.name));
  require(caseDirectories.length === 6, 'Run must contain exactly six case directories.');
  const proposed = structuredClone(ledger);
  const updates = [];
  const outcomes = [];
  for (const plan of manifest.cases) {
    const caseDirectory = caseDirectories.find(path => read(join(path, 'case-summary.json')).operation === plan.operation);
    require(caseDirectory, `Missing case: ${plan.operation}`);
    const summaryPath = join(caseDirectory, 'case-summary.json');
    const summary = read(summaryPath);
    require(summary.models_planned === plan.models.length && summary.models_attempted === summary.attempts.length,
      'Summary differs from frozen plan.');
    const byModel = new Map(readdirSync(caseDirectory, { withFileTypes: true }).filter(item => item.isDirectory())
      .map(item => {
        const path = join(caseDirectory, item.name, 'receipt.json');
        const value = read(path);
        return [value.model, { path, value }];
      }));
    const receipts = summary.attempts.map(attempt => {
      const found = byModel.get(attempt.model);
      require(found && modelMap.get(attempt.model)?.sha256 === found.value.model_sha256,
        'Receipt/model hash differs from frozen corpus.');
      require(found.value.status === attempt.status && found.value.reason === attempt.reason,
        'Summary differs from its receipt.');
      return found;
    });
    require(byModel.size === receipts.length, 'Unaccounted receipt.');
    checkAttempts(plan, receipts.map(receipt => receipt.value));
    const validations = receipts.filter(receipt => receipt.value.status === 'validated');
    require(validations.length <= 1, `Multiple validations for one setter: ${plan.operation}`);
    if (validations.length) {
      const validation = validations[0];
      const model = modelMap.get(validation.value.model);
      const mode = model.relative_path ? 'project' : 'within';
      const index = proposed.entries.findIndex(entry => entry.operation === plan.operation);
      const before = proposed.entries[index];
      proposed.entries[index] = mode === 'project'
        ? appendProjectAttempt(before, validation.value)
        : promoteWithinCorpus(before, validation.value);
      updates.push({ mode, before, after: proposed.entries[index], receipt: reference(validation.path) });
    }
    outcomes.push({ operation: plan.operation, status: validations.length ? 'validated' : 'rejected-with-reason',
      attempts: receipts.map(receipt => ({ model: receipt.value.model, status: receipt.value.status,
        reason: receipt.value.reason, receipt: reference(receipt.path) })), summary: reference(summaryPath) });
  }
  require(!hasRvt(run), 'Disposable copies remain on disk.');

  const setters = proposed.entries.filter(entry => entry.operation.startsWith('api.set:'));
  const originalNumerator = ledger.entries.filter(entry => entry.operation.startsWith('api.set:')
    && entry.state === 'changed_value_tested').length;
  const numerator = setters.filter(entry => entry.state === 'changed_value_tested').length;
  const newValidations = numerator - originalNumerator;
  require(setters.length === 805 && newValidations >= manifest.expected_yield_min
    && newValidations <= manifest.expected_yield_max && newValidations === updates.length,
  'Denominator or preregistered validation yield changed.');
  const countsByState = Object.fromEntries([...new Set(setters.map(entry => entry.state))].sort()
    .map(state => [state, setters.filter(entry => entry.state === state).length]));
  const report = { run: runName, original_ledger_sha256: manifest.ledger_sha256,
    previous_reconciliation: reference(previousReportPath), manifest: reference(manifestPath),
    provenance: reference(provenancePath), trx: reference(trxPath), remaining_rvt_files: 0,
    original_numerator: originalNumerator, proposed_numerator: numerator, new_validations: newValidations,
    denominator: 805, counts_by_state: countsByState, updates, outcomes };
  const reportText = JSON.stringify(report, null, 2) + '\n';
  proposed.campaign = 'changed-value-documented-fourth-' + runName;
  proposed.source_sha256 = createHash('sha256').update(reportText).digest('hex').toUpperCase();
  proposed.scope += ' Direct-API validations from documented batch four were run only on disposable copies; rejected and unchanged attempts remain unpromoted. ';
  const results = { report: 'documented-fourth-6', new_validations: newValidations, entries: outcomes };
  for (const [name, value] of [['ledger-proposal.json', proposed], ['ledger-reconciliation.json', report],
    ['documented-fourth-results.json', results]])
    writeFileSync(join(run, name), JSON.stringify(value, null, 2) + '\n', { flag: 'wx' });
  console.log(JSON.stringify({ numerator, new_validations: newValidations, denominator: 805,
    counts_by_state: countsByState, report: portable(join(run, 'ledger-reconciliation.json')) }));
}

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  try {
    require(process.argv.length === 4, 'Use reconcile-documented-fourth.mjs <run-name> <trx-filename>.');
    reconcile(...process.argv.slice(2));
  } catch (error) { console.error(error.message); process.exitCode = 1; }
}
