import { existsSync, readFileSync, writeFileSync, readdirSync } from 'node:fs';
import { createHash } from 'node:crypto';
import { resolve, join, dirname, relative, basename, extname } from 'node:path';
import { fileURLToPath } from 'node:url';
import { checkAttempts } from './reconcile-dedicated.mjs';
import { receiptAttempt } from './reconcile-pilot.mjs';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '../..');
const evidence = join(root, 'docs/review/setter-validation-gate');
const ledgerPath = join(root, 'RevitCopilot/Agent/Knowledge/revit2026-validation.json');
const read = path => JSON.parse(readFileSync(path, 'utf8').replace(/^\uFEFF/, ''));
const hash = path => createHash('sha256').update(readFileSync(path)).digest('hex').toUpperCase();
const require = (ok, reason) => { if (!ok) throw new Error(reason); };
const same = (a, b) => JSON.stringify(a) === JSON.stringify(b);
const portable = path => relative(root, path).replaceAll('\\', '/');
const reference = path => ({ path: portable(path), sha256: hash(path) });
export const total = entry => entry.passed_models + entry.context_failures
  + entry.unsupported_models + entry.same_value_models;

export function promoteWithinCorpus(original, receipt) {
  receiptAttempt(receipt);
  require(receipt.status === 'validated', 'Only validated evidence can promote a setter.');
  require(original.state === 'same_value_only' && original.passed_models === 0,
    'Expected an unpromoted same-value setter.');
  require(original.same_value_models > 0, 'No prior same-value outcome is available to replace.');
  const updated = structuredClone(original);
  updated.passed_models += 1;
  updated.same_value_models -= 1;
  updated.state = 'changed_value_tested';
  return updated;
}

export function appendProjectAttempt(original, receipt) {
  receiptAttempt(receipt);
  require(receipt.status !== 'out-of-contract', 'Project corpus may not expand the production contract.');
  const updated = structuredClone(original);
  if (receipt.status === 'validated') {
    updated.passed_models += 1;
    updated.state = 'changed_value_tested';
  } else if (receipt.status === 'still-same-value') {
    updated.same_value_models += 1;
  } else if (['no_existing_target_in_unmodified_sample', 'no_proven_valid_alternative'].includes(receipt.reason)) {
    updated.unsupported_models += 1;
  } else {
    updated.context_failures += 1;
  }
  return updated;
}

export function filesWithExtension(path, extension) {
  const result = [];
  for (const item of readdirSync(path, { withFileTypes: true })) {
    const child = join(path, item.name);
    if (item.isDirectory()) result.push(...filesWithExtension(child, extension));
    else if (extname(item.name).toLowerCase() === extension) result.push(child);
  }
  return result;
}

export function completedTrx(path, count, fixture) {
  const trx = readFileSync(path, 'utf8');
  const counters = trx.match(/<Counters\s+([^>]+)\/>/)?.[1] ?? '';
  require(new RegExp(`\\btotal="${count}"`).test(counters)
    && new RegExp(`\\bpassed="${count}"`).test(counters)
    && /\bfailed="0"/.test(counters) && trx.includes(fixture),
  `TRX does not establish ${count} completed ${fixture} cases.`);
}

export function evidenceForPlan(run, manifest, expectedCount) {
  const models = new Map(manifest.models.map(model => [model.name, model]));
  const caseDirectories = readdirSync(run, { withFileTypes: true }).filter(item => item.isDirectory())
    .map(item => join(run, item.name));
  require(caseDirectories.length === expectedCount, `Expected ${expectedCount} case directories.`);
  const summaries = caseDirectories.map(path => ({ path, value: read(join(path, 'case-summary.json')) }));
  require(new Set(summaries.map(item => item.value.operation)).size === expectedCount,
    'Duplicate or missing case summary.');
  return manifest.cases.map(item => {
    const plan = { ...item, operation: 'api.set:Autodesk.Revit.DB.' + item.property };
    const located = summaries.find(summary => summary.value.operation === plan.operation);
    require(located, `Missing case summary: ${plan.operation}`);
    const summary = located.value;
    require(summary.models_planned === plan.models.length && summary.models_attempted === summary.attempts.length,
      'Case summary differs from frozen plan.');
    const receiptFiles = readdirSync(located.path, { withFileTypes: true }).filter(item => item.isDirectory())
      .map(item => join(located.path, item.name, 'receipt.json'));
    const receiptsByModel = new Map(receiptFiles.map(path => {
      const value = read(path);
      require(!value.copy || (basename(value.copy) === 'disposable.rvt'
        && basename(dirname(value.copy)) === basename(dirname(path))),
      'Receipt copy path differs from its portable evidence directory.');
      return [value.model, { path, value }];
    }));
    require(receiptsByModel.size === receiptFiles.length && receiptFiles.length === summary.attempts.length,
      'Unaccounted or duplicate receipt.');
    const receipts = summary.attempts.map(attempt => {
      const found = receiptsByModel.get(attempt.model);
      require(found && models.get(attempt.model), `Missing frozen receipt/model: ${attempt.model}`);
      require(found.value.model_sha256 === models.get(attempt.model).sha256,
        'Receipt hash differs from frozen model.');
      require(found.value.status === attempt.status && found.value.reason === attempt.reason,
        'Summary differs from receipt.');
      return found;
    });
    checkAttempts(plan, receipts.map(receipt => receipt.value));
    return { plan, receipts };
  });
}

function reconcile(nonElementRunName, nonElementTrxName, projectRunName, projectTrxName) {
  require(/^\d{8}T\d{6}$/.test(nonElementRunName ?? ''), 'Provide the exact non-element run name.');
  require(/^nonelement-\d{8}T\d{6}\.trx$/.test(nonElementTrxName ?? ''), 'Provide the non-element TRX.');
  require(/^\d{8}T\d{6}$/.test(projectRunName ?? ''), 'Provide the exact project-corpus run name.');
  require(/^projectcorpus-\d{8}T\d{6}\.trx$/.test(projectTrxName ?? ''), 'Provide the project-corpus TRX.');

  const ledger = read(ledgerPath);
  const nonManifestPath = join(evidence, 'non-element-manifest.json');
  const projectManifestPath = join(evidence, 'project-corpus-manifest.json');
  const nonManifest = read(nonManifestPath);
  const projectManifest = read(projectManifestPath);
  const nonRun = join(evidence, 'non-element-runs', nonElementRunName);
  const projectRun = join(evidence, 'project-corpus-runs', projectRunName);
  const nonProvenancePath = join(nonRun, 'provenance.json');
  const projectProvenancePath = join(projectRun, 'provenance.json');
  const nonProvenance = read(nonProvenancePath);
  const projectProvenance = read(projectProvenancePath);
  const ledgerHash = hash(ledgerPath);

  require(ledgerHash === nonManifest.ledger_sha256 && ledgerHash === projectManifest.ledger_sha256,
    'Ledger changed since preregistration.');
  require(hash(nonManifestPath) === nonProvenance.manifest_sha256
    && nonManifest.revision === nonProvenance.revision, 'Non-element manifest/revision differs from runtime evidence.');
  require(hash(projectManifestPath) === projectProvenance.manifest_sha256
    && projectManifest.revision === projectProvenance.revision, 'Project manifest/revision differs from runtime evidence.');
  require(projectManifest.prior_non_element_manifest_sha256 === hash(nonManifestPath)
    && projectManifest.prior_non_element_run === nonElementRunName, 'Project manifest does not bind the prior run.');
  require(hash(join(evidence, 'non-element-preregistration.md')) === nonManifest.preregistration_sha256,
    'Non-element protocol changed.');
  require(hash(join(evidence, 'project-corpus-preregistration.md')) === projectManifest.preregistration_sha256,
    'Project-corpus protocol changed.');
  require(hash(join(evidence, 'elementid-classification.csv')) === nonManifest.classification_sha256
    && nonManifest.classification_sha256 === projectManifest.classification_sha256, 'Classification changed.');
  require(hash(join(evidence, 'writable-snapshot-amendment.md')) === nonManifest.snapshot_amendment_sha256
    && nonManifest.snapshot_amendment_sha256 === projectManifest.snapshot_amendment_sha256,
    'Snapshot protocol changed.');
  for (const [manifest, provenance] of [[nonManifest, nonProvenance], [projectManifest, projectProvenance]]) {
    require(provenance.runtime.startsWith('10.') && provenance.binaries.some(binary => binary.sha256 === manifest.api_sha256),
      'Runtime/API binary evidence missing.');
  }

  require(nonManifest.models.length === 12 && projectManifest.models.length === 4,
    'Unexpected corpus size.');
  const allHashes = [...nonManifest.models, ...projectManifest.models].map(model => model.sha256);
  require(new Set(allHashes).size === 16, 'Source model hashes are not distinct across corpora.');
  for (const model of nonManifest.models) require(hash(model.path) === model.sha256,
    `Autodesk source changed: ${model.name}`);
  const fixtureRoot = process.env.LECG_PROJECT_FIXTURE_ROOT;
  require(fixtureRoot, 'LECG_PROJECT_FIXTURE_ROOT is required to verify project sources.');
  for (const model of projectManifest.models) {
    const path = resolve(fixtureRoot, model.relative_path);
    require(relative(resolve(fixtureRoot), path).split(/[\\/]/).every(part => part !== '..')
      && hash(path) === model.sha256, `Project source changed: ${model.name}`);
  }
  require(nonManifest.cases.length === 14 && projectManifest.cases.length === 11,
    'Unexpected frozen case count.');
  require(projectManifest.baseline_entries.length === 11
    && projectManifest.baseline_entries.every(entry => same(entry, ledger.entries.find(item => item.operation === entry.operation))),
  'Project baseline entries differ from the frozen ledger.');

  const nonTrxPath = join(evidence, nonElementTrxName);
  const projectTrxPath = join(evidence, projectTrxName);
  completedTrx(nonTrxPath, 14, 'NonElementBatch');
  completedTrx(projectTrxPath, 11, 'ProjectCorpusBatch');
  const priorRun = ledger.campaign.match(/^changed-value-class-collectors-(\d{8}T\d{6})/)?.[1];
  require(priorRun, 'Ledger does not identify the previous reconciliation.');
  const previousReportPath = join(evidence, 'class-collector-runs', priorRun, 'ledger-reconciliation.json');
  require(hash(previousReportPath) === ledger.source_sha256, 'Previous ledger provenance differs.');

  const proposed = structuredClone(ledger);
  const nonEvidence = evidenceForPlan(nonRun, nonManifest, 14);
  const nonUpdates = [];
  for (const item of nonEvidence) {
    const index = proposed.entries.findIndex(entry => entry.operation === item.plan.operation);
    require(index >= 0 && total(proposed.entries[index]) === 12, 'Non-element baseline is not the 12-model corpus.');
    const before = structuredClone(proposed.entries[index]);
    const validation = item.receipts.find(receipt => receipt.value.status === 'validated');
    if (validation) proposed.entries[index] = promoteWithinCorpus(proposed.entries[index], validation.value);
    nonUpdates.push({ before, after: proposed.entries[index],
      replacement_basis: validation ? (before.context_failures
        ? 'validated same corpus; replaced the sole/strongest prior same-value outcome despite aggregate-only model history'
        : 'validated same corpus; replaced one prior same-value outcome') : 'unchanged',
      receipts: item.receipts.map(receipt => reference(receipt.path)) });
  }

  const projectEvidence = evidenceForPlan(projectRun, projectManifest, 11);
  const projectUpdates = [], projectResults = [];
  let projectAttempts = 0;
  for (const item of projectEvidence) {
    const index = proposed.entries.findIndex(entry => entry.operation === item.plan.operation);
    require(index >= 0 && total(proposed.entries[index]) === 12, 'Project case baseline no longer represents 12 models.');
    const before = structuredClone(proposed.entries[index]);
    for (const receipt of item.receipts) proposed.entries[index] = appendProjectAttempt(proposed.entries[index], receipt.value);
    projectAttempts += item.receipts.length;
    require(total(proposed.entries[index]) === 12 + item.receipts.length,
      'Project model outcomes did not reconcile exactly.');
    projectUpdates.push({ before, after: proposed.entries[index],
      receipts: item.receipts.map(receipt => reference(receipt.path)) });
    projectResults.push({ operation: item.plan.operation,
      attempts: item.receipts.map(receipt => ({ model: receipt.value.model, status: receipt.value.status,
        reason: receipt.value.reason, ...reference(receipt.path) })) });
  }
  require(projectAttempts === 44, 'Expected four project attempts for each unsuccessful case.');
  require(filesWithExtension(projectRun, '.rvt').length === 0, 'Disposable project copies remain on disk.');

  const setters = proposed.entries.filter(entry => entry.operation.startsWith('api.set:'));
  require(setters.length === 805 && proposed.entries.length === ledger.entries.length,
    'Denominator or operation identities changed.');
  const originalNumerator = ledger.entries.filter(entry => entry.operation.startsWith('api.set:')
    && entry.state === 'changed_value_tested').length;
  const numerator = setters.filter(entry => entry.state === 'changed_value_tested').length;
  require(numerator - originalNumerator === 12, 'Expected exactly 12 new validated setter identities.');
  const countsByState = Object.fromEntries([...new Set(setters.map(entry => entry.state))].sort()
    .map(state => [state, setters.filter(entry => entry.state === state).length]));
  const report = { non_element_run: nonElementRunName, project_corpus_run: projectRunName,
    original_ledger_sha256: ledgerHash, previous_reconciliation: reference(previousReportPath),
    non_element_manifest: reference(nonManifestPath), project_corpus_manifest: reference(projectManifestPath),
    non_element_provenance: reference(nonProvenancePath), project_corpus_provenance: reference(projectProvenancePath),
    non_element_trx: reference(nonTrxPath), project_corpus_trx: reference(projectTrxPath),
    project_copy_cleanup: { expected_receipts: 44, remaining_rvt_files: 0 },
    original_numerator: originalNumerator, proposed_numerator: numerator,
    new_validations: numerator - originalNumerator, denominator: 805,
    counts_by_state: countsByState, non_element_updates: nonUpdates, project_corpus_updates: projectUpdates };
  const reportText = JSON.stringify(report, null, 2) + '\n';
  proposed.campaign = 'changed-value-project-corpus-' + projectRunName;
  proposed.source_sha256 = createHash('sha256').update(reportText).digest('hex').toUpperCase();
  proposed.scope += ` ${report.new_validations} further direct-API setter validations from the non-element batch; `
    + `${projectAttempts} supplemental outcomes from four user-authorized detached project fixtures. `
    + 'Project evidence is context-specific; family-document writes remain outside the contract. ';
  const results = { report: 'project-corpus-11', models: projectManifest.models.map(model => ({
    discipline: model.discipline, name: model.name, sha256: model.sha256 })),
  total_cases: projectResults.length, receipt_attempts: projectAttempts,
  counts_by_outcome: {
    validated: projectResults.filter(item => item.attempts.some(attempt => attempt.status === 'validated')).length,
    'no-validation': projectResults.filter(item => !item.attempts.some(attempt => attempt.status === 'validated')).length
  }, entries: projectResults };
  for (const [name, value] of [['ledger-proposal.json', proposed], ['ledger-reconciliation.json', report],
    ['project-corpus-results.json', results]])
    writeFileSync(join(projectRun, name), JSON.stringify(value, null, 2) + '\n', { flag: 'wx' });
  console.log(JSON.stringify({ numerator, new_validations: report.new_validations, denominator: 805,
    project_attempts: projectAttempts, project_validations: results.counts_by_outcome.validated,
    counts_by_state: countsByState, report: portable(join(projectRun, 'ledger-reconciliation.json')) }));
}

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  try {
    require(process.argv.length === 6,
      'Use reconcile-project-corpus.mjs <non-element-run> <non-element-trx> <project-run> <project-trx>.');
    reconcile(...process.argv.slice(2));
  } catch (error) { console.error(error.message); process.exitCode = 1; }
}
