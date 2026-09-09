import { readFileSync, writeFileSync, readdirSync } from 'node:fs';
import { createHash } from 'node:crypto';
import { resolve, join, dirname, basename } from 'node:path';
import { fileURLToPath } from 'node:url';
import { aggregate, receiptAttempt } from './reconcile-pilot.mjs';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '../..');
const evidence = join(root, 'docs/review/setter-validation-gate');
const read = path => JSON.parse(readFileSync(path, 'utf8').replace(/^\uFEFF/, ''));
const hash = path => createHash('sha256').update(readFileSync(path)).digest('hex').toUpperCase();
const require = (ok, reason) => { if (!ok) throw new Error(reason); };
const same = (a, b) => JSON.stringify(a) === JSON.stringify(b);
const reference = path => ({ path, sha256: hash(path) });

// A partial batch, post-success retry or reordered fixture list cannot silently qualify.
export function checkAttempts(plan, receipts) {
  require(receipts.length > 0 && receipts.length <= plan.models.length, 'Missing/excess attempts.');
  receipts.forEach((r, i) => {
    require(r.operation === plan.operation && r.model === plan.models[i], 'Attempt differs from frozen order.');
    receiptAttempt(r);
    require(i === receipts.length - 1 || r.status !== 'validated', 'Attempt after first success.');
  });
  require(receipts.at(-1).status === 'validated' || receipts.length === plan.models.length,
    'Unsuccessful case did not exhaust its frozen fixtures.');
}

function reconcile(runName, trxName) {
  require(/^\d{8}T\d{6}-[a-f\d]{32}$/.test(runName ?? ''), 'Provide an exact dedicated run name.');
  require(/^dedicated-\d{8}T\d{6}-[a-f\d]{32}\.trx$/.test(trxName ?? ''), 'Provide the completed dedicated TRX filename.');
  const run = join(evidence, 'dedicated-runs', runName);
  const manifestPath = join(evidence, 'dedicated-writable-manifest.json');
  const manifest = read(manifestPath);
  const provenancePath = join(run, 'provenance.json');
  const provenance = read(provenancePath);
  const ledgerPath = join(root, 'RevitCopilot/Agent/Knowledge/revit2026-validation.json');
  const ledger = read(ledgerPath);
  require(hash(ledgerPath) === manifest.ledger_sha256, 'Ledger changed since preregistration.');
  require(hash(manifestPath) === provenance.manifest_sha256 && provenance.revision === manifest.revision,
    'Manifest/revision differs from runtime evidence.');
  require(hash(manifest.baseline_path) === manifest.baseline_sha256, 'Historical corpus receipt changed.');
  require(hash(join(evidence, 'dedicated-preregistration.md')) === manifest.preregistration_sha256,
    'Frozen protocol changed.');
  require(hash(join(evidence, 'dedicated-manifest.json')) === manifest.original_manifest_sha256,
    'Original manifest changed.');
  require(hash(join(evidence, 'writable-snapshot-amendment.md')) === manifest.snapshot_amendment_sha256
    && provenance.snapshot_amendment_sha256 === manifest.snapshot_amendment_sha256, 'Snapshot amendment differs.');
  require(hash(manifest.readonly_probe.path) === manifest.readonly_probe.sha256, 'Read-only gate receipt changed.');
  const gate = read(manifest.readonly_probe.path);
  require(gate.parameter_probe?.IsReadOnly === true && gate.parameter_probe.element_id === 1462965
    && gate.parameter_probe.parameter_id === -1006490 && gate.cleanup_verified && !gate.setter_attempted,
    'Read-only amendment condition not established.');
  const originalPlan = read(join(evidence, 'dedicated-manifest.json'));
  require(same(manifest.cases, originalPlan.cases) && same(manifest.models, originalPlan.models),
    'Amendment changed the case/fixture selection.');
  require(hash(join(evidence, 'elementid-classification.csv')) === manifest.classification_sha256,
    'Classification changed.');
  require(provenance.runtime.startsWith('10.') && provenance.binaries.some(b => b.sha256 === manifest.api_sha256),
    'Runtime gate evidence missing.');
  for (const s of provenance.sources) require(hash(s.path) === s.sha256, 'Harness source changed since execution.');
  require(hash(join(root, 'tools/SetterValidationProbe/SetterValidationProbe.csproj')) === provenance.project_sha256,
    'Harness project changed since execution.');
  const trxPath = join(evidence, trxName);
  const trx = readFileSync(trxPath, 'utf8');
  const counters = trx.match(/<Counters\s+([^>]+)\/>/)?.[1] ?? '';
  require(/\btotal="14"/.test(counters) && /\bpassed="14"/.test(counters) && /\bfailed="0"/.test(counters)
    && trx.includes('DedicatedCollectionBatch'), 'TRX does not establish 14 completed harness cases.');
  const previousRun = join(evidence, 'pilot-runs', manifest.previous_pilot_run);
  const previousReportPath = join(previousRun, 'ledger-reconciliation.json');
  require(hash(previousReportPath) === ledger.source_sha256, 'Previous ledger provenance differs.');
  const previousReport = read(previousReportPath);
  const history = read(manifest.baseline_path);
  const models = new Map(manifest.models.map(m => [m.name, m.sha256]));
  require(models.size === 12 && new Set(models.values()).size === 12, 'Expected 12 distinct source hashes.');
  require(manifest.cases.length === 14 && new Set(manifest.cases.map(c => c.operation)).size === 14,
    'Expected 14 distinct frozen cases.');
  const directories = readdirSync(run, { withFileTypes: true }).filter(d => d.isDirectory()).map(d => d.name).sort();
  require(same(directories, manifest.cases.map(c => c.property).sort()), 'Run case directories differ from frozen set.');
  const proposed = structuredClone(ledger), updates = [], batchEntries = [], outcomes = [];
  for (const plan of manifest.cases) {
    const original = ledger.entries.find(e => e.operation === plan.operation);
    require(same(original, manifest.baseline_entries.find(e => e.operation === plan.operation)), 'Baseline entry differs.');
    require(['same_value_only', 'changed_value_tested'].includes(original.state), 'Outside phase 1.');
    const historicalRows = history.operations.filter(a => a.operation === plan.operation);
    require(historicalRows.length === 12 && new Set(historicalRows.map(a => a.model)).size === 12, 'Incomplete historical corpus.');
    const attempts = historicalRows.map(a => ({ model_sha256: models.get(a.model), state:
      ({ roundtrip_only: 'same_value_only', context_rejected: 'context_rejected', missing_fixture: 'missing_fixture' })[a.status] }));
    for (const prior of previousReport.updates.filter(u => u.after.operation === plan.operation)) {
      require(hash(prior.receipt) === prior.receipt_sha256, 'Prior pilot receipt changed.');
      attempts.push(receiptAttempt(read(prior.receipt)));
    }
    require(same(aggregate(plan.operation, attempts), original), 'Historical reconstruction differs from current ledger.');
    const casePath = join(run, plan.property), summary = read(join(casePath, 'case-summary.json'));
    require(summary.operation === plan.operation && summary.historical_reachable_models === plan.models.length,
      'Case summary differs from plan.');
    const receipts = summary.attempts.map(a => {
      const path = join(casePath, basename(a.model, '.rvt'), 'receipt.json');
      const r = read(path);
      require(r.model_sha256 === models.get(r.model), 'Receipt model hash differs from corpus.');
      require(r.status === a.status && r.reason === a.reason, 'Summary differs from receipt.');
      return { ...r, evidence: reference(path) };
    });
    const modelDirs = readdirSync(casePath, { withFileTypes: true }).filter(d => d.isDirectory()).map(d => d.name).sort();
    require(same(modelDirs, receipts.map(r => basename(r.model, '.rvt')).sort())
      && summary.models_attempted === receipts.length, 'Unaccounted model attempt.');
    checkAttempts(plan, receipts);
    const current = receipts.map(receiptAttempt);
    const updated = aggregate(plan.operation, [...attempts, ...current]);
    proposed.entries[proposed.entries.findIndex(e => e.operation === plan.operation)] = updated;
    batchEntries.push(aggregate(plan.operation, current));
    outcomes.push({ operation: plan.operation, status: receipts.some(r => r.status === 'validated') ? 'validated'
      : receipts.some(r => r.status === 'still-same-value') ? 'still-same-value'
      : receipts.some(r => r.status === 'out-of-contract') ? 'out-of-contract' : 'rejected-with-reason',
      attempts: receipts.map(r => ({ model: r.model, status: r.status, reason: r.reason, setter_attempted: r.setter_attempted,
        ...r.evidence })) });
    updates.push({ before: original, after: updated, receipts: receipts.map(r => r.evidence) });
  }
  const setters = proposed.entries.filter(e => e.operation.startsWith('api.set:'));
  require(setters.length === 805 && proposed.entries.length === ledger.entries.length, 'Denominator/identity count changed.');
  const numerator = setters.filter(e => e.state === 'changed_value_tested').length;
  const priorNumerator = ledger.entries.filter(e => e.operation.startsWith('api.set:') && e.state === 'changed_value_tested').length;
  const report = { run: runName, revision: manifest.revision, original_ledger_sha256: manifest.ledger_sha256,
    previous_reconciliation: reference(previousReportPath), historical_receipt: reference(manifest.baseline_path),
    provenance: reference(provenancePath), trx: reference(trxPath), manifest: reference(manifestPath),
    original_numerator: priorNumerator, proposed_numerator: numerator, new_validations: numerator - priorNumerator, denominator: 805,
    counts_by_state: Object.fromEntries([...new Set(setters.map(e => e.state))].sort().map(s => [s, setters.filter(e => e.state === s).length])), updates };
  const reportText = JSON.stringify(report, null, 2) + '\n';
  proposed.campaign = 'changed-value-dedicated-' + runName;
  proposed.source_sha256 = createHash('sha256').update(reportText).digest('hex').toUpperCase();
  proposed.scope += ` ${report.new_validations} further direct-API setter validations from the dedicated-collection batch only; source_sha256 identifies its ledger-reconciliation.json. One strongest outcome per model hash; classification probes excluded. Each unsaved detached copy discarded after observable rollback verification.`;
  for (const [name, data] of [['ledger-proposal.json', proposed], ['ledger-reconciliation.json', report],
    ['dedicated-results.json', { report: 'dedicated-collection-14', total: batchEntries.length,
      counts_by_outcome: Object.fromEntries(['validated', 'still-same-value', 'rejected-with-reason', 'out-of-contract'].map(s => [s, outcomes.filter(o => o.status === s).length])),
      entries: batchEntries, outcomes }]])
    writeFileSync(join(run, name), JSON.stringify(data, null, 2) + '\n', { flag: 'wx' });
  console.log(JSON.stringify({ numerator, new_validations: report.new_validations, denominator: 805, report: join(run, 'ledger-reconciliation.json') }));
}

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  try { require(process.argv.length === 4, 'Use reconcile-dedicated.mjs <run-name> <trx-filename>.'); reconcile(...process.argv.slice(2)); }
  catch (e) { console.error(e.message); process.exitCode = 1; }
}
