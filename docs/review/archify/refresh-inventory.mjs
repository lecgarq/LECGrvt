// Offline documentation inventory; never loads Revit, runs model code, or invokes AI.
import { readFileSync, readdirSync, writeFileSync, renameSync, unlinkSync, existsSync } from "node:fs";
import { dirname, join, relative, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { createHash } from "node:crypto";
import { execFileSync } from "node:child_process";

const here = dirname(fileURLToPath(import.meta.url));
const root = resolve(here, "../../..");
const normalize = value => value.replaceAll("\\", "/");
const git = (...args) => execFileSync("git", args, { cwd: root, encoding: "utf8", windowsHide: true }).trim();
const sha = value => createHash("sha256").update(value).digest("hex");
const inputs = new Map();
function read(path) {
  const bytes = readFileSync(join(root, path));
  inputs.set(path, sha(bytes));
  return bytes.toString("utf8").replace(/^\uFEFF/, "");
}
function walk(path) {
  return readdirSync(join(root, path), { withFileTypes: true }).sort((a, b) => a.name.localeCompare(b.name, "en")).flatMap(entry => {
    if (["bin", "obj", "TestHelpers"].includes(entry.name)) return [];
    const next = normalize(join(path, entry.name));
    return entry.isDirectory() ? walk(next) : [next];
  });
}
function unique(items, key, label) {
  if (!items.length || new Set(items.map(item => item[key])).size !== items.length)
    throw new Error("Empty or duplicate " + label + "; review extraction before publishing.");
}
const commands = walk("src/Commands").filter(path => path.endsWith(".cs")).flatMap(path => {
  const code = read(path);
  return [...code.matchAll(/^\s*public\s+(?:(?:sealed|partial)\s+)*class\s+(\w+Command)\s*:/gm)]
    .map(match => ({ name: match[1], path, line: code.slice(0, match.index).split("\n").length }));
});
unique(commands, "name", "command classes");
const serviceFiles = walk("src/Services").filter(path => path.endsWith(".cs"));
const services = {};
for (const path of serviceFiles) {
  read(path);
  const domain = path.split("/").length > 3 ? path.split("/")[2] : "Shared";
  (services[domain] ??= []).push(path);
}
const catalogPath = "RevitCopilot/Agent/CapabilityCatalog.cs";
const literal = '"((?:[^"\\\\]|\\\\.)*)"';
const capabilityPattern = new RegExp("^\\s*new\\(" + [literal, literal, literal, literal].join(",\\s*") + "\\)", "gm");
const decode = value => JSON.parse('"' + value + '"');
const operations = [...read(catalogPath).matchAll(capabilityPattern)].map(match => ({
  name: decode(match[1]), kind: decode(match[2]), description: decode(match[3]),
  arguments: decode(match[4]), source: catalogPath
}));
unique(operations, "name", "native operation registrations");
if (operations.some(operation => !["read", "change"].includes(operation.kind)))
  throw new Error("Unrecognized operation kind.");
const recipePath = "RevitCopilot/Agent/WorkflowLibrary.cs";
const recipePattern = new RegExp("^\\s*new\\(" + [literal, literal, literal].join(",\\s*") + ',\\s*"curated_template"', "gm");
const recipes = [...read(recipePath).matchAll(recipePattern)].map(match => ({
  id: decode(match[1]), name: decode(match[2]), description: decode(match[3]),
  status: "curated_template", source: recipePath
}));
unique(recipes, "id", "curated recipes");
const mcpTools = ["RevitCopilot/McpServer/AgentTools.cs", "RevitCopilot/McpServer/RevitTools.cs"].flatMap(path =>
  [...read(path).matchAll(/\[McpServerTool\(Name\s*=\s*"([^"]+)"/g)].map(match => ({ name: match[1], source: path })));
unique(mcpTools, "name", "MCP tools");
const referencesPath = "RevitCopilot/Agent/Knowledge/revit2026-reference.json";
const validationPath = "RevitCopilot/Agent/Knowledge/revit2026-validation.json";
const references = JSON.parse(read(referencesPath));
const validation = JSON.parse(read(validationPath));
unique(references.entries, "id", "research references");
unique(validation.entries, "operation", "API validation entries");
function tally(entries, field) {
  return entries.reduce((result, entry) => {
    const value = String(entry[field] ?? "unmapped");
    result[value] = (result[value] ?? 0) + 1;
    return result;
  }, {});
}
const documentation = walk("docs").filter(path =>
  !path.startsWith("docs/review/archify/") && !path.includes("/revit-api/") && path.endsWith(".md"));
const planningDocuments = walk(".planning").filter(path => path.endsWith(".md"));
const sourceSupport = [
  "LECG.csproj", "RevitCopilot/RevitCopilot.csproj", "src/App.cs", "src/Core/Bootstrapper.cs",
  "src/Core/Ribbon/RibbonService.cs", "RevitCopilot/Revit/ToolExecutor.Changes.cs",
  "RevitCopilot/Agent/AgentBatch.cs", "RevitCopilot/Services/ProjectConversationStore.cs"
];
sourceSupport.forEach(read);
const unchangedInputs = [...inputs.keys()].every(path => {
  try { return inputs.get(path) === sha(execFileSync("git", ["show", "HEAD:" + path], { cwd: root, windowsHide: true, maxBuffer: 16 * 1024 * 1024 })); }
  catch { return false; }
});
const inventory = {
  schema_version: 1,
  purpose: "Repository navigation and bounded agent context; not an execution registry or proof of runtime correctness.",
  repository_revision: git("rev-parse", "HEAD"),
  source_inputs_match_revision: unchangedInputs,
  counts: {
    command_classes: commands.length, service_source_files: serviceFiles.length,
    mcp_top_level_tools: mcpTools.length, native_operations: operations.length,
    native_reads: operations.filter(operation => operation.kind === "read").length,
    native_changes: operations.filter(operation => operation.kind === "change").length,
    curated_recipes: recipes.length, shared_references: references.entries.length,
    api_validation_entries: validation.entries.length
  },
  commands, services, mcp_tools: mcpTools, native_operations: operations, curated_recipes: recipes,
  research: {
    source: referencesPath, provenance: references.provenance,
    entries_by_kind: tally(references.entries, "kind"),
    entries_by_research_status: tally(references.entries, "research_status"),
    note: "Full references remain in the original pack. A reference does not establish executable capability."
  },
  api_evidence: {
    source: validationPath, campaign: validation.campaign, scope: validation.scope,
    entries_by_state: tally(validation.entries, "state"),
    note: "Recorded campaign evidence, not tests rerun by this documentation refresh."
  },
  documentation, planning_documents: planningDocuments,
  input_sha256: Object.fromEntries([...inputs].sort(([a], [b]) => a.localeCompare(b, "en")))
};
const output = join(here, "knowledge-index.json");
const temporary = output + ".tmp";
try {
  writeFileSync(temporary, JSON.stringify(inventory, null, 2) + "\n");
  renameSync(temporary, output);
} finally { if (existsSync(temporary)) unlinkSync(temporary); }
console.log(JSON.stringify({ output: normalize(relative(root, output)), ...inventory.counts,
  source_inputs_match_revision: unchangedInputs, api_evidence: inventory.api_evidence.entries_by_state }, null, 2));
