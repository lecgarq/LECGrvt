import { readFileSync, writeFileSync, readdirSync } from 'node:fs';
import { createHash } from 'node:crypto';
import { resolve, join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '../..');
const evidence = join(root, 'docs/review/setter-validation-gate');
const read = path => JSON.parse(readFileSync(path, 'utf8').replace(/^\uFEFF/, ''));
const hash = path => createHash('sha256').update(readFileSync(path)).digest('hex').toUpperCase();
const states = { validated: 'changed_value_tested', 'still-same-value': 'same_value_only',
  'rejected-with-reason': 'context_rejected', 'out-of-contract': 'out_of_contract' };
const rank = { missing_fixture: 0, context_rejected: 1, same_value_only: 2, changed_value_tested: 3 };

// One strongest evidenced outcome per source-model hash, not additive retry counters.
export function aggregate(operation, attempts) {
  const models = new Map();
  for (const a of attempts) {
    if (!a.model_sha256 || (!Object.hasOwn(rank, a.state) && a.state !== 'out_of_contract'))
      throw new Error('Unknown state or missing model hash.');
    const prior = models.get(a.model_sha256);
    if ((a.state === 'out_of_contract' && prior === 'changed_value_tested') ||
        (a.state === 'changed_value_tested' && prior === 'out_of_contract'))
      throw new Error('Contradictory contract/pass evidence.');
    if (a.state === 'out_of_contract' || (!prior || (prior !== 'out_of_contract' && rank[a.state] > rank[prior])))
      models.set(a.model_sha256, a.state);
  }
  const values = [...models.values()];
  const count = s => values.filter(v => v === s).length;
  if (count('out_of_contract') && count('changed_value_tested')) throw new Error('Contradictory contract/pass evidence.');
  const state = count('out_of_contract') ? 'out_of_contract'
    : ['changed_value_tested', 'same_value_only', 'context_rejected', 'missing_fixture'].find(s => count(s)) ?? 'missing_fixture';
  return { operation, state, passed_models: count('changed_value_tested'), context_failures: count('context_rejected'),
    unsupported_models: count('missing_fixture'), same_value_models: count('same_value_only') };
}

export function receiptAttempt(receipt) {
  if (!Object.hasOwn(states, receipt.status) || !receipt.cleanup_verified || receipt.infrastructure_failure || receipt.cleanup_error)
    throw new Error('Incomplete or failed-infrastructure receipt cannot enter the ledger.');
  if (receipt.setter_attempted && !receipt.rollback_verified) throw new Error('Missing verified rollback.');
  if (receipt.status === 'validated' && (!receipt.setter_attempted || receipt.commit_status !== 'Committed'
      || JSON.stringify(receipt.before) === JSON.stringify(receipt.after_commit))) throw new Error('Invalid changed-value evidence.');
  if (receipt.status === 'validated' && JSON.stringify(receipt.desired) !== JSON.stringify(receipt.after_commit))
    throw new Error('Requested and committed values do not match.');
  return { model_sha256: receipt.model_sha256, state: states[receipt.status] };
}

function reconcile(runName) {
  if (!/^\d{8}T\d{6}-[a-f\d]{32}$/.test(runName ?? '')) throw new Error('Provide one exact pilot run directory name.');
  const run = join(evidence, 'pilot-runs', runName);
  const manifestPath = join(evidence, 'pilot-manifest.json');
  const manifest = read(manifestPath);
  const provenance = read(join(run, 'provenance.json'));
  if (hash(manifestPath) !== provenance.manifest_sha256 || hash(manifest.baseline_path) !== manifest.baseline_sha256)
    throw new Error('Changed manifest or historical receipt.');
  const ledgerPath = join(root, 'RevitCopilot/Agent/Knowledge/revit2026-validation.json');
  if (hash(ledgerPath) !== manifest.ledger_sha256) throw new Error('Ledger changed since preregistration; refuse blind merge.');
  const ledger = read(ledgerPath);
  const history = read(manifest.baseline_path);
  const allowed = read(join(evidence, 'setter-surface.json')).entries.filter(e => e.phase1).map(e => e.operation);
  const receipts = readdirSync(run, { withFileTypes: true }).filter(e => e.isDirectory())
    .map(e => ({ path: join(run, e.name, 'receipt.json'), ...read(join(run, e.name, 'receipt.json')) }));
  if (receipts.length !== 8 || new Set(receipts.map(r => r.operation)).size !== 8) throw new Error('Require exactly eight completed unique pilot receipts.');
  const pilot = ['Electrical.CableType.ConductorMaterial', 'Material.CutBackgroundPatternId', 'TextElement.Text',
    'Plumbing.PipingSystemType.FluidTemperature', 'Structure.LoadCase.Number', 'ReferencePlane.BubbleEnd',
    'ViewSheetSet.IsAutomatic', 'Electrical.ElectricalSystem.CircuitConnectionType'].map(p => 'api.set:Autodesk.Revit.DB.' + p);
  if (receipts.some(r => !pilot.includes(r.operation))) throw new Error('Receipt set differs from fixed pilot.');
  const models = new Map(manifest.models.map(m => [m.name, m.sha256]));
  const proposed = structuredClone(ledger);
  const updates = [];
  for (const r of receipts) {
    if (!allowed.includes(r.operation) || r.model !== manifest.pilot_model || r.model_sha256 !== models.get(r.model))
      throw new Error('Receipt outside preregistered phase/model.');
    const original = ledger.entries.find(e => e.operation === r.operation);
    if (original.state !== 'same_value_only') throw new Error('Pilot must not reclassify the other two triage groups.');
    const rows = history.operations.filter(a => a.operation === r.operation);
    const attempts = rows.map(a => {
      const state = ({ roundtrip_only: 'same_value_only', context_rejected: 'context_rejected', missing_fixture: 'missing_fixture' })[a.status];
      if (!state) throw new Error('Unexpected historical pilot state; manual provenance review required.');
      return { model_sha256: models.get(a.model), state };
    });
    if (JSON.stringify(aggregate(r.operation, attempts)) !== JSON.stringify(original))
      throw new Error('Historical reconstruction does not exactly match the ledger.');
    const updated = aggregate(r.operation, [...attempts, receiptAttempt(r)]);
    proposed.entries[proposed.entries.findIndex(e => e.operation === r.operation)] = updated;
    updates.push({ before: original, after: updated, receipt: r.path, receipt_sha256: hash(r.path) });
  }
  const setters = proposed.entries.filter(e => e.operation.startsWith('api.set:'));
  if (setters.length !== 805) throw new Error('Setter denominator changed.');
  const report = { run: runName, original_ledger_sha256: manifest.ledger_sha256,
    historical_receipt_path: manifest.baseline_path, historical_receipt_sha256: manifest.baseline_sha256,
    provenance_path: join(run, 'provenance.json'), provenance_sha256: hash(join(run, 'provenance.json')),
    proposed_numerator: setters.filter(e => e.state === 'changed_value_tested').length,
    denominator: 805, counts_by_state: Object.fromEntries([...new Set(setters.map(e => e.state))].sort().map(s => [s, setters.filter(e => e.state === s).length])), updates };
  const reportText = JSON.stringify(report, null, 2) + '\n';
  proposed.campaign = 'changed-value-pilot-' + runName;
  proposed.source_sha256 = createHash('sha256').update(reportText).digest('hex').toUpperCase();
  const gain = report.proposed_numerator - ledger.entries.filter(e => e.operation.startsWith('api.set:') && e.state === 'changed_value_tested').length;
  proposed.scope += ` ${gain} new direct-API setter validations from the named pilot only; source_sha256 identifies ledger-reconciliation.json, which links the historical campaign and new receipts. Observable rollback is not complete internal-state certification; every pilot copy was discarded without saving.`;
  // Never overwrite the production ledger or an earlier proposal. Review/apply is a separate step.
  writeFileSync(join(run, 'ledger-proposal.json'), JSON.stringify(proposed, null, 2) + '\n', { flag: 'wx' });
  writeFileSync(join(run, 'ledger-reconciliation.json'), reportText, { flag: 'wx' });
  const entries = receipts.map(r => aggregate(r.operation, [receiptAttempt(r)]));
  writeFileSync(join(run, 'pilot-results.json'), JSON.stringify({ report: 'eight-setter-pilot',
    source_model: manifest.pilot_model, source_model_sha256: models.get(manifest.pilot_model),
    ledger_reconciliation_sha256: proposed.source_sha256, total: entries.length,
    counts_by_outcome: Object.fromEntries(Object.keys(states).map(s => [s, receipts.filter(r => r.status === s).length])),
    entries }, null, 2) + '\n', { flag: 'wx' });
  console.log(JSON.stringify({ proposed_numerator: report.proposed_numerator, denominator: 805,
    counts_by_state: report.counts_by_state, path: join(run, 'ledger-reconciliation.json') }));
}

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  try {
    if (process.argv.length !== 3) throw new Error('Use reconcile-pilot.mjs <exact-run-name>.');
    reconcile(process.argv[2]);
  } catch (e) { console.error(e.message); process.exitCode = 1; }
}
