import { readFileSync, writeFileSync, mkdirSync } from 'node:fs';
import { createHash } from 'node:crypto';
import { execFileSync } from 'node:child_process';
import { resolve, join, dirname, relative } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '../..');
const evidenceRoot = join(root, 'docs/review/setter-validation-gate');
const ledgerPath = join(root, 'RevitCopilot/Agent/Knowledge/revit2026-validation.json');
const surfacePath = join(evidenceRoot, 'setter-surface.json');
const contractPath = join(root, 'tools/SetterValidationProbe/ChangedValuePilot.cs');
const apiPath = 'C:/Program Files/Autodesk/Revit 2026/RevitAPI.dll';
const operation = 'api.set:Autodesk.Revit.DB.ModelCurve.TrussCurveType';
const read = path => JSON.parse(readFileSync(path, 'utf8').replace(/^\uFEFF/, ''));
const hash = path => createHash('sha256').update(readFileSync(path)).digest('hex').toUpperCase();
const require = (ok, reason) => { if (!ok) throw new Error(reason); };
const portable = path => relative(root, path).replaceAll('\\', '/');
const reference = path => ({ path: portable(path), sha256: hash(path) });

function classify(runName) {
  require(/^\d{8}T\d{6}$/.test(runName ?? ''), 'Use <yyyyMMddTHHmmss>.');
  const ledger = read(ledgerPath);
  const surface = read(surfacePath);
  const api = surface.entries.find(entry => entry.operation === operation);
  const before = ledger.entries.find(entry => entry.operation === operation);
  require(api?.api_documentation?.includes('applicable only to curves in Truss families'),
    'Autodesk metadata no longer establishes the truss-family restriction.');
  require(api.api_documentation.includes('curve not in a truss family'),
    'Autodesk metadata no longer establishes the non-family rejection.');
  require(before?.state === 'same_value_only' && before.passed_models === 0,
    'Unexpected current ledger state.');
  require(readFileSync(contractPath, 'utf8').includes('!doc.IsFamilyDocument'),
    'Harness/MCP document contract no longer refuses family documents.');

  const runs = join(evidenceRoot, 'out-of-contract-runs');
  mkdirSync(runs, { recursive: true });
  const run = join(runs, runName);
  mkdirSync(run, { recursive: false });
  const revision = execFileSync('git', ['rev-parse', 'HEAD'], { cwd: root, encoding: 'utf8' }).trim();
  const classification = {
    operation,
    status: 'out-of-contract',
    reason: 'Autodesk metadata limits the setter to truss family documents; family-document writes are excluded.',
    revision,
    models_opened: 0,
    family_documents_opened: 0,
    production_contract_changed: false,
    revit_api: { path: apiPath, sha256: hash(apiPath) },
    setter_surface: reference(surfacePath),
    contract_guard: reference(contractPath),
    original_ledger: reference(ledgerPath)
  };
  const classificationPath = join(run, 'classification.json');
  writeFileSync(classificationPath, JSON.stringify(classification, null, 2) + '\n', { flag: 'wx' });

  const proposed = structuredClone(ledger);
  const index = proposed.entries.findIndex(entry => entry.operation === operation);
  proposed.entries[index] = { operation, state: 'out_of_contract', passed_models: 0,
    context_failures: 0, unsupported_models: 0, same_value_models: 0 };
  const setters = proposed.entries.filter(entry => entry.operation.startsWith('api.set:'));
  const numerator = setters.filter(entry => entry.state === 'changed_value_tested').length;
  require(setters.length === 805 && numerator === 667, 'Setter denominator or numerator changed.');
  const countsByState = Object.fromEntries([...new Set(setters.map(entry => entry.state))].sort()
    .map(state => [state, setters.filter(entry => entry.state === state).length]));
  require(countsByState.out_of_contract === 1 && countsByState.same_value_only === 20,
    'Unexpected classification totals.');
  const report = { run: runName, revision, operation, original_ledger_sha256: hash(ledgerPath),
    classification: reference(classificationPath), before, after: proposed.entries[index],
    numerator, denominator: 805, counts_by_state: countsByState, models_opened: 0 };
  const reportText = JSON.stringify(report, null, 2) + '\n';
  const reportPath = join(run, 'ledger-reconciliation.json');
  writeFileSync(reportPath, reportText, { flag: 'wx' });
  proposed.campaign = 'changed-value-modelcurve-out-of-contract-' + runName;
  proposed.source_sha256 = createHash('sha256').update(reportText).digest('hex').toUpperCase();
  proposed.scope += ' ModelCurve.TrussCurveType is out of the project-document write contract because Autodesk restricts it to truss family documents. ';
  writeFileSync(join(run, 'ledger-proposal.json'), JSON.stringify(proposed, null, 2) + '\n', { flag: 'wx' });
  console.log(JSON.stringify({ numerator, denominator: 805, counts_by_state: countsByState,
    report: portable(reportPath) }));
}

try {
  require(process.argv.length === 3, 'Use classify-trusscurve-out-of-contract.mjs <run-name>.');
  classify(process.argv[2]);
} catch (error) { console.error(error.message); process.exitCode = 1; }
