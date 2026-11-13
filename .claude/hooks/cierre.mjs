// Hook Stop: si en este turno hubo cambios confirmados en Revit (un .csx con Commit enviado por puente.ps1 o candado.sh)
// o trabajo en Blender, y esta sesión no escribió en el segundo cerebro, pide registrarlo antes de terminar.
// Solo mira lo que hizo ESTA sesión (su transcripción): otras sesiones escriben el mismo registro a la vez.
// Prueba: node cierre.mjs --test
import { existsSync, readFileSync } from "node:fs";
import { isAbsolute, join } from "node:path";

const ROOT = "C:/LECG/RevitAddins/LECG";
const BRAIN = /registro-maestro\.md|second-brain[\\/]|segundo-cerebro[\\/]/i;
const registry = () => { try { return JSON.parse(readFileSync(join(ROOT, ".claude/skills/lecg-proyecto/proyectos.json"), "utf8")); } catch { return {}; } };

// Entradas desde el último mensaje real del usuario (no resultados de herramientas).
export function turno(entries) {
  const isUser = e => e.type === "user" && !e.isMeta && !e.isCompactSummary && !e.isSidechain &&
    (typeof e.message?.content === "string" || (e.message?.content ?? []).some(b => b.type === "text"));
  let start = entries.length - 1;
  while (start >= 0 && !isUser(entries[start])) start--;
  return entries.slice(start + 1);
}

export function evaluar(entries, read = p => (existsSync(p) ? readFileSync(p, "utf8") : null)) {
  const uses = turno(entries).filter(e => e.type === "assistant" && !e.isSidechain)
    .flatMap(e => (e.message?.content ?? []).filter(b => b.type === "tool_use"));
  let revit = 0, blender = 0, registered = false, project = null;
  // Los .csx se suelen escribir con Write en el mismo turno y enviarse con rutas de variables ("$t\x.csx"):
  // se reconocen por el nombre del archivo.
  const base = p => p.split(/[\\/]/).pop().toLowerCase();
  const written = new Map(uses.filter(u => u.name === "Write" && /\.csx$/i.test(u.input?.file_path ?? ""))
    .map(u => [base(u.input.file_path), String(u.input.content ?? "")]));
  for (const u of uses) {
    const cmd = String(u.input?.command ?? "");
    if ((u.name === "Edit" || u.name === "Write") && BRAIN.test(u.input?.file_path ?? "")) registered = true;
    if ((u.name === "Bash" || u.name === "PowerShell") && BRAIN.test(cmd) && /(>>|Add-Content|Out-File|AppendAll|WriteAll)/i.test(cmd)) registered = true;
    if (/^mcp__blender__execute_blender_code$/.test(u.name) || /blender(\.exe)?["']?\s+-b\b/i.test(cmd)) blender++;
    if (/puente\.ps1|candado\.sh/i.test(cmd)) {
      const file = cmd.match(/-CodeFile\s+["']?([^"'\s]+\.csx)/i)?.[1] ?? cmd.match(/candado\.sh["']?\s+["']?([^"'\s]+\.csx)/i)?.[1];
      if (!file) continue; // leer o editar puente.ps1 no es un envío
      const code = written.get(base(file)) ?? (/\$/.test(file) ? null : read(isAbsolute(file) ? file : join(ROOT, file)));
      // ponytail: «Commit(» en el .csx = cambio confirmado. Si no se encuentra el código, no se cuenta (mejor perder un aviso que
      // pedir registro en cada lectura); ampliar si se ven cambios sin registrar. Un ensayo solo con RollBack no cuenta.
      if (code !== null && /\.Commit\s*\(/.test(code)) { revit++; project = cmd.match(/-Proyecto\s+["']?([a-z]+)/i)?.[1]?.toLowerCase() ?? project; }
    }
  }
  // ponytail: Blender cuenta como estudio desde 3 pasos en el turno (1–2 suelen ser solo inspeccionar la escena); medido en 525 turnos reales.
  if (blender < 3) blender = 0;
  if (registered || (revit === 0 && blender === 0)) return null;
  const what = [revit && `${revit} envío(s) al puente con cambios confirmados en Revit`, blender && `${blender} paso(s) de trabajo en Blender`].filter(Boolean).join(" y ");
  // Cada proyecto registra en su propio expediente (campo «registro» de proyectos.json); sin proyecto, el registro maestro.
  const reg = registry(), where = (project && reg[project]?.registro) || "docs/review/second-brain/registro-maestro.md";
  const label = project && reg[project]?.nombre ? ` de ${reg[project].nombre}` : "";
  return `En este turno hubo ${what}${label}, y esta sesión no escribió en el segundo cerebro. Antes de terminar, añade la entrada al final de ${where} con el formato de la skill eucalipto-segundo-cerebro (fecha y hora, tema, estado: propuesta | aplicado | verificado…, IDs, evidencia, lo conservado y lo pendiente; «Sin guardar/sincronizar» si aplica). Si fue un ensayo revertido o una prueba sin resultado que registrar, dilo en una línea y termina.`;
}

if (process.argv[2] === "--test") {
  const a = (c, m) => { if (!c) { console.error("FALLA: " + m); process.exit(1); } };
  const user = t => ({ type: "user", message: { content: t } });
  const tool = (name, input) => ({ type: "assistant", message: { content: [{ type: "tool_use", name, input }] } });
  const result = { type: "user", message: { content: [{ type: "tool_result", content: "ok" }] } };
  const commit = p => ((p = p.replaceAll("\\", "/")) === `${ROOT}/.tmp/x/aplicar.csx` ? "using(var t=new Transaction(doc)){t.Start();t.Commit();}" : p.endsWith("ensayo.csx") ? "t.RollBack();" : null);
  const puente = f => tool("PowerShell", { command: `powershell -File puente.ps1 -Proyecto eucalipto -CodeFile ${f} -RequestId 1` });
  a(evaluar([user("aplica"), puente(".tmp/x/aplicar.csx"), result], commit), "commit sin registro debe pedir registro");
  a(!evaluar([user("aplica"), puente(".tmp/x/aplicar.csx"), result, tool("Edit", { file_path: "C:\\LECG\\RevitAddins\\LECG\\docs\\review\\second-brain\\registro-maestro.md" })], commit), "registrado con Edit pasa");
  a(!evaluar([user("aplica"), puente(".tmp/x/aplicar.csx"), tool("Bash", { command: "printf 'x' >> docs/review/second-brain/registro-maestro.md" })], commit), "registrado con >> pasa");
  a(!evaluar([user("prueba"), puente(".tmp/x/ensayo.csx")], commit), "ensayo con RollBack no cuenta");
  a(!evaluar([user("lee"), tool("Bash", { command: "cat .claude/skills/lecg-proyecto/scripts/puente.ps1" })], commit), "leer puente.ps1 sin -CodeFile no cuenta");
  const b = tool("mcp__blender__execute_blender_code", { code: "" });
  a(evaluar([user("estudio"), b, b, b], commit), "estudio en Blender sin registro pide registro");
  a(!evaluar([user("mira"), b], commit), "un paso de Blender (inspeccionar) no cuenta");
  a(!evaluar([puente(".tmp/x/aplicar.csx"), user("¿y ahora?"), tool("Read", { file_path: "a" })], commit), "solo cuenta el turno actual");
  a(!evaluar([user("x"), tool("Bash", { command: "bash .tmp/x/candado.sh .tmp/x/otro.csx ID" })], commit), "csx ilegible no cuenta");
  a(evaluar([user("aplica"), tool("Write", { file_path: "C:/tmp/q/aplicar-muro.csx", content: "t.Commit();" }), tool("PowerShell", { command: "& puente.ps1 -Proyecto e -CodeFile \"$t\\aplicar-muro.csx\" -RequestId 2" })], commit), "csx escrito en el turno y enviado con variable cuenta");
  a(!evaluar([user("lee"), tool("Write", { file_path: "C:/tmp/q/leer-muro.csx", content: "var w = doc.GetElement(id);" }), tool("PowerShell", { command: "& puente.ps1 -Proyecto e -CodeFile \"$t\\leer-muro.csx\" -RequestId 3" })], commit), "lectura escrita en el turno no cuenta");
  console.log("ok");
} else {
  let s = "";
  process.stdin.on("data", d => (s += d)).on("end", () => {
    try {
      const ev = JSON.parse(s);
      if (ev.stop_hook_active || !ev.transcript_path || !existsSync(ev.transcript_path)) return;
      const entries = readFileSync(ev.transcript_path, "utf8").split("\n").flatMap(l => { try { return [JSON.parse(l)]; } catch { return []; } });
      const reason = evaluar(entries);
      if (reason) process.stdout.write(JSON.stringify({ decision: "block", reason }));
    } catch { /* nunca impedir que la sesión termine por un error del hook */ }
  });
}
