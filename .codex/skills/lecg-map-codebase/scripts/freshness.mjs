import { execFileSync } from 'node:child_process';
import { resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { root, readInventory, inventoryFreshness } from './query-index.mjs';

const renderedRevision = 'd3a105199a04aa7295b634dba829397b805cd18e';
const git = (...args) => execFileSync('git', args, {
  cwd: root, encoding: 'utf8', timeout: 5000, windowsHide: true, stdio: ['ignore', 'pipe', 'pipe']
}).trim();

export function dashboardState(config, workspace = root) {
  const project = config.active_project;
  if (!project?.path || resolve(project.path).toLowerCase() !== resolve(workspace).toLowerCase()) return null;
  const readyForResolution = config.languages?.includes('csharp') === true && config.jetbrains_mode === false;
  return { state: readyForResolution ? 'reachable' : 'degraded', workspace: project.path,
    csharp_configured: config.languages?.includes('csharp') === true,
    backend: config.jetbrains_mode === false ? 'LSP' : 'unverified',
    symbol_resolution_verified: false,
    reason: 'Live matching workspace and backend probe; compiler readiness is confirmed by the first evidence-bearing symbol lookup, not a separate health call.' };
}

export async function serenaHealth() {
  // ponytail: inspect eight default local ports only; add explicit endpoint configuration if deployments move beyond these.
  const results = await Promise.all(Array.from({ length: 8 }, async (_, offset) => {
    const endpoint = `http://127.0.0.1:${24282 + offset}/get_config_overview`;
    try {
      const response = await fetch(endpoint, { redirect: 'error', signal: AbortSignal.timeout(600) });
      if (!response.ok) return null;
      // Bound the local response too; never expose unrelated project details.
      const reader = response.body.getReader();
      const chunks = [];
      let bytes = 0;
      while (true) {
        const { value, done } = await reader.read();
        if (done) break;
        bytes += value.byteLength;
        if (bytes > 256 * 1024) { await reader.cancel(); return null; }
        chunks.push(Buffer.from(value));
      }
      return dashboardState(JSON.parse(Buffer.concat(chunks).toString('utf8')));
    } catch { return null; }
  }));
  return results.find(Boolean) ?? { state: 'unavailable',
    reason: 'No matching live Serena dashboard verified on the bounded local probe; MCP availability remains unestablished.',
    next_query: 'No standalone health retry in query mode; report unavailable and use admissible non-semantic evidence.' };
}

export async function freshness() {
  let index;
  try {
    const inventory = readInventory();
    if (inventory.schema_version !== 1) throw new Error('schema');
    index = { ...inventoryFreshness(inventory), counts: inventory.counts };
  } catch {
    index = { status: 'STALE', inventory_admissible: false,
      reason: 'Inventory missing, unreadable or unsupported.',
      fix: 'node docs/review/archify/refresh-inventory.mjs' };
  }
  let commitsBehind = null;
  let ancestor = false;
  try {
    git('merge-base', '--is-ancestor', renderedRevision, 'HEAD');
    ancestor = true;
    commitsBehind = Number(git('rev-list', '--count', `${renderedRevision}..HEAD`));
  } catch { /* A missing or divergent rendered revision has unknown distance, not zero. */ }
  const projects = ['LECG.csproj', 'RevitCopilot/RevitCopilot.csproj',
    'RevitCopilot/McpServer/RevitCopilot.McpServer.csproj'].map(path => {
    try {
      const result = JSON.parse(execFileSync('dotnet', ['msbuild', path,
        '-getProperty:TargetFramework,DefineConstants', '-p:SkipRevitDeploy=true', '-nologo'],
      { cwd: root, encoding: 'utf8', windowsHide: true, timeout: 5000, stdio: ['ignore', 'pipe', 'pipe'] }));
      return { path, ...result.Properties };
    } catch { return { path, status: 'unresolved' }; }
  });
  return { index, projects, rendered_docs: { pinned_revision: renderedRevision, ancestor_of_head: ancestor,
    commits_behind: commitsBehind, status: 'ASSUMED_STALE',
    note: 'Inventory refresh does not refresh rendered HTML; do not open it as evidence.' },
    serena: await serenaHealth() };
}

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  const args = process.argv.slice(2);
  if (args.length > 1 || (args.length === 1 && args[0] !== '--strict')) {
    process.stderr.write('Usage: node freshness.mjs [--strict]\n');
    process.exitCode = 1;
  } else {
    const result = await freshness();
    process.stdout.write(JSON.stringify(result) + '\n');
    // Normal stale/unavailable outcomes never masquerade as command failures.
    process.exitCode = args.includes('--strict') && !result.index.inventory_admissible ? 1 : 0;
  }
}
