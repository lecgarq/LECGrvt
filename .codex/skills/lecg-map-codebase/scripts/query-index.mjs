import { readFileSync } from 'node:fs';
import { execFileSync } from 'node:child_process';
import { createHash } from 'node:crypto';
import { dirname, resolve, sep } from 'node:path';
import { fileURLToPath } from 'node:url';

export const root = resolve(dirname(fileURLToPath(import.meta.url)), '../../../..');
const indexPath = 'docs/review/archify/knowledge-index.json';
const validationPath = 'RevitCopilot/Agent/Knowledge/revit2026-validation.json';
const limits = { entries: 25, bytes: 4096 };
const arity = { freshness: [0, 0], counts: [0, 0], service: [1, 1],
  command: [1, 1], operation: [1, 1], 'mcp-tool': [1, 1], recipes: [0, 1], validation: [1, 1] };
const readJson = path => JSON.parse(readFileSync(resolve(root, path), 'utf8').replace(/^\uFEFF/, ''));
export const readInventory = () => readJson(indexPath);

export function parseArgs(args) {
  const [command, ...terms] = args;
  const bounds = Object.hasOwn(arity, command ?? '') ? arity[command] : null;
  if (!bounds || terms.length < bounds[0] || terms.length > bounds[1]
      || terms.some(term => !term.trim() || term.length > 160 || term.trim().startsWith('-')
        || /[\x00-\x1f\x7f]/u.test(term))) {
    throw new Error('Use freshness | counts | service <domain> | command <name> | operation <name> | mcp-tool <name> | recipes [term] | validation <state>. No flags or expressions.');
  }
  return { command, term: terms[0]?.trim().toLowerCase() };
}

export function inventoryFreshness(index) {
  let head = null;
  try {
    head = execFileSync('git', ['rev-parse', 'HEAD'], {
      cwd: root, encoding: 'utf8', windowsHide: true, timeout: 5000, stdio: ['ignore', 'pipe', 'pipe']
    }).trim();
  } catch { /* No HEAD means inventory is inadmissible. */ }
  const hashes = Object.entries(index.input_sha256 ?? {});
  const inputsMatchWorkingTree = hashes.length > 0 && hashes.every(([path, expected]) => {
    const absolute = resolve(root, path);
    if (!absolute.startsWith(root + sep)) return false;
    try { return createHash('sha256').update(readFileSync(absolute)).digest('hex') === expected; }
    catch { return false; }
  });
  let inputSetMatch = false;
  try {
    const paths = execFileSync('git', ['ls-files', '--cached', '--others', '--exclude-standard', '-z',
      '--', 'src/Commands', 'src/Services'], {
      cwd: root, encoding: 'utf8', windowsHide: true, timeout: 5000, stdio: ['ignore', 'pipe', 'pipe']
    }).split('\0').filter(path => path.endsWith('.cs') && !/(?:^|\/)(?:bin|obj|TestHelpers)\//.test(path));
    inputSetMatch = paths.every(path => Object.hasOwn(index.input_sha256 ?? {}, path));
  } catch { /* Unknown input coverage also makes inventory inadmissible. */ }
  const admissible = Boolean(head && head === index.repository_revision
    && index.source_inputs_match_revision === true && inputsMatchWorkingTree && inputSetMatch);
  return { repository_revision: index.repository_revision ?? null, head,
    source_inputs_match_revision: index.source_inputs_match_revision === true,
    inputs_match_working_tree: inputsMatchWorkingTree,
    input_set_matches: inputSetMatch,
    status: admissible ? 'CURRENT' : 'STALE',
    inventory_admissible: admissible,
    ...(admissible ? {} : { fix: 'node docs/review/archify/refresh-inventory.mjs' }) };
}

export function selectEntries(index, command, term, readValidation = () => readJson(validationPath)) {
  switch (command) {
    case 'freshness': return [];
    case 'counts': return Object.entries(index.counts).map(([name, count]) => ({ name, count }));
    case 'service': return Object.entries(index.services)
      .filter(([domain]) => domain.toLowerCase() === term)
      .flatMap(([domain, paths]) => paths.map(path => ({ domain, path })));
    case 'command': return index.commands.filter(item => item.name.toLowerCase() === term);
    case 'operation': return index.native_operations.filter(item => item.name.toLowerCase() === term);
    case 'mcp-tool': return index.mcp_tools.filter(item => item.name.toLowerCase() === term);
    case 'recipes': return index.curated_recipes.filter(item => !term
      || [item.id, item.name, item.description].some(value => value.toLowerCase().includes(term)));
    case 'validation': {
      // The index contains state totals, not individual operation validation records.
      if (term !== 'unvalidated-setters' && !Object.hasOwn(index.api_evidence.entries_by_state, term))
        throw new Error('Unknown validation state. Use a state recorded in the existing validation inventory.');
      return readValidation().entries.filter(item => term === 'unvalidated-setters'
        ? item.operation.startsWith('api.set:') && item.state !== 'changed_value_tested'
        : item.state.toLowerCase() === term)
        .map(({ operation, state, passed_models, context_failures, unsupported_models, same_value_models }) =>
          ({ operation, state, passed_models, context_failures, unsupported_models, same_value_models }));
    }
    default: throw new Error('Unsupported command.');
  }
}

export function serializeBounded(metadata, entries) {
  const selected = entries.slice(0, limits.entries);
  while (true) {
    const output = JSON.stringify({ ...metadata, total: entries.length, returned: selected.length,
      entries: selected, ...(selected.length < entries.length
        ? { truncation: `TRUNCATED: ${selected.length}/${entries.length} entries — ${metadata.export_command
          ? 'use explicit report export for the full list' : 'refine the query'}` } : {}) }) + '\n';
    if (Buffer.byteLength(output, 'utf8') <= limits.bytes) return output;
    if (!selected.length) throw new Error('Metadata exceeds the 4 KB output limit.');
    selected.pop();
  }
}

function main() {
  try {
    // Validate the command before reading anything. No arbitrary paths or expressions.
    const { command, term } = parseArgs(process.argv.slice(2));
    const index = readInventory();
    if (index.schema_version !== 1) throw new Error('Unsupported inventory schema.');
    const freshness = inventoryFreshness(index);
    const entries = selectEntries(index, command, term);
    process.stdout.write(serializeBounded({ command, source: indexPath, tier: 'inventory',
      ...freshness, ...(command === 'validation' ? { records_source: validationPath,
        recorded_state_total: index.api_evidence.entries_by_state[term],
        ...(term === 'unvalidated-setters' ? {
          export_command: 'node .codex/skills/lecg-map-codebase/scripts/export-report.mjs unvalidated-setters'
        } : {}) } : {}) }, entries));
  } catch (error) {
    // Do not expose input file contents or unbounded exception messages.
    process.stderr.write(JSON.stringify({ error: String(error.message).slice(0, 500) }) + '\n');
    process.exitCode = 1;
  }
}

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) main();
