'use strict';
const $ = id => document.getElementById(id);
let current = null, serial = 0, sourceSerial = 0;
const actions = ['copy', 'downloadMd', 'downloadJson'];
const scopeNames = { all: 'Todo', both: 'Ambos ámbitos', repo: 'Repositorio', project: 'Casa Eucalipto', sessions: 'Conversación' };
function node(tag, text, className) { const el = document.createElement(tag); el.textContent = text; if (className) el.className = className; return el; }
async function api(url, options) {
  const response = await fetch(url, options);
  if (!response.ok) { const error = await response.json().catch(() => ({})); throw new Error(error.error || `No se pudo completar la consulta (HTTP ${response.status}).`); }
  return response.json();
}
function disableExports() { current = null; actions.forEach(id => $(id).disabled = true); }
function invalidate() { serial++; sourceSerial++; disableExports(); $('results').replaceChildren(); $('reader').hidden = true; $('count').textContent = 'Consulta modificada'; $('status').textContent = 'Pulsa Buscar para actualizar la evidencia.'; }
$('query').addEventListener('input', invalidate);
document.querySelectorAll('[name=scope]').forEach(el => el.addEventListener('change', invalidate));
async function showSource(id, hash, start = 0, end = 0) {
  const ticket = ++sourceSerial;
  try {
    const source = await api(`/api/source?id=${encodeURIComponent(id)}${hash ? '&hash=' + encodeURIComponent(hash) : ''}`);
    if (ticket !== sourceSerial) return;
    $('readerTitle').textContent = source.spec.path;
    $('readerMeta').textContent = `${source.spec.state} · Modificado: ${new Date(source.modifiedUtc).toLocaleString('es-MX')} · SHA256 ${source.hash}`;
    $('sourceText').replaceChildren(...source.lines.map((line, i) => node('span', `${String(i + 1).padStart(4)}  ${line}`, i + 1 >= start && i + 1 <= end ? 'selected' : '')));
    $('reader').hidden = false;
    const firstLine = $('sourceText').querySelector('.selected');
    $('sourceText').scrollTop = firstLine ? firstLine.offsetTop - $('sourceText').offsetTop : 0;
    $('reader').scrollIntoView({ block: 'start' });
    $('sourceText').focus({ preventScroll: true });
  } catch (error) { if (ticket === sourceSerial) $('status').textContent = error.message; }
}
$('openMaster').addEventListener('click', () => showSource('master'));
$('openLessons').addEventListener('click', () => showSource('lessons'));
$('closeSource').addEventListener('click', () => { sourceSerial++; $('reader').hidden = true; });
async function search(timeline = false) {
  if (!$('searchForm').reportValidity()) return;
  const ticket = ++serial;
  const query = $('query').value.trim(), scope = document.querySelector('[name=scope]:checked').value;
  disableExports(); sourceSerial++; $('reader').hidden = true; $('results').replaceChildren(); $('status').textContent = 'Leyendo fuentes locales…'; $('count').textContent = 'Buscando';
  try {
    const result = await api(`/api/${timeline ? 'timeline' : 'search'}?q=${encodeURIComponent(query)}&scope=${scope}`);
    if (ticket !== serial) return;
    current = result; $('count').textContent = `${result.hits.length} fragmentos`;
    $('status').textContent = `${result.hits.length ? (timeline ? 'Fragmentos más pertinentes, del más antiguo al más reciente según la fecha del hecho.' : 'Fragmentos ordenados por coincidencia; comprueba su alcance antes de concluir.') : 'Sin coincidencias. Prueba un término específico o amplía el ámbito.'} ${result.missingSources.length ? 'Fuentes ausentes: ' + result.missingSources.join(', ') : `${result.sourceCount} fuentes disponibles.`} ${result.mode ?? ''}`;
    result.hits.forEach((hit, i) => {
      const p = hit.passage, card = node('article', '', 'card'), badges = node('div', '', 'badges');
      badges.append(node('span', `[${i + 1}] ${scopeNames[p.scope]}`), node('span', p.date ? p.date.slice(0, 10) : 'Sin fecha'), node('span', [hit.score > 0 ? `Palabras ${hit.score.toFixed(2)}` : '', hit.similarity != null ? `Significado ${hit.similarity.toFixed(2)}` : ''].filter(Boolean).join(' · ')));
      if (p.status) badges.append(node('span', `Estado detectado: ${p.status}`));
      if (hit.validity) badges.append(node('span', hit.validity, hit.validity === 'registro más reciente' ? 'fresh' : 'stale'));
      card.append(badges, node('h3', p.heading), node('p', p.state, 'state'), node('p', `${p.path} · líneas ${p.start}–${p.end}`, 'path'), node('pre', p.text, 'excerpt'), node('p', hit.matched.length ? 'Términos: ' + hit.matched.join(' · ') : 'Encontrado por significado, sin palabras en común', 'path'));
      if (hit.related?.length) { const list = node('ul', '', 'related'); hit.related.forEach(r => list.append(node('li', r))); card.append(node('p', 'Relacionado en el grafo (captura de solo lectura; confirma el UniqueId):', 'path'), list); }
      const button = node('button', 'Ver fuente y líneas'); button.addEventListener('click', () => showSource(p.sourceId, p.hash, p.start, p.end)); card.append(button); $('results').append(card);
    });
    actions.forEach(id => $(id).disabled = false);
  } catch (error) { if (ticket === serial) { $('status').textContent = error.message; $('count').textContent = 'No disponible'; } }
}
$('searchForm').addEventListener('submit', event => { event.preventDefault(); search(); });
$('timelineButton').addEventListener('click', () => search(true));
document.querySelectorAll('[data-query]').forEach(button => button.addEventListener('click', () => { $('query').value = button.dataset.query; search(); }));
$('copy').addEventListener('click', async () => {
  if (!current) return;
  try { await navigator.clipboard.writeText(current.context); $('status').textContent = 'Contexto copiado. Puedes pegarlo en este chat para que Codex responda con citas.'; }
  catch { $('status').textContent = 'El navegador no permitió copiar. Usa Descargar contexto .md.'; }
});
function download(content, name, type) { const url = URL.createObjectURL(new Blob([content], { type })); const link = document.createElement('a'); link.href = url; link.download = name; document.body.append(link); link.click(); link.remove(); setTimeout(() => URL.revokeObjectURL(url), 1000); }
$('downloadMd').addEventListener('click', () => { if (current) download(current.context, 'lecg-contexto.md', 'text/markdown;charset=utf-8'); });
$('downloadJson').addEventListener('click', () => { if (current) download(JSON.stringify(current, null, 2), 'lecg-evidencia.json', 'application/json'); });
$('jevButton').addEventListener('click', async () => {
  if (!$('searchForm').reportValidity()) return;
  const query = $('query').value.trim(); $('jevButton').disabled = true; $('jevMessage').textContent = 'Enviando solo la pregunta a TypeSafe…';
  try {
    const answer = await api('/api/jev', { method: 'POST', headers: { 'Content-Type': 'application/json', 'X-LECG-Request': 'consult' }, body: JSON.stringify({ query }) });
    $('jevMessage').textContent = (answer.ok ? `Para «${query}»: ${scopeNames[answer.scope]} · confianza declarada ${(answer.confidence * 100).toFixed(0)}%. ` : '') + answer.message;
  } catch (error) { $('jevMessage').textContent = error.message; }
  finally { $('jevButton').disabled = false; }
});
api('/api/status').then(status => {
  $('sourceCount').textContent = status.sources.filter(s => s.available).length;
  $('jevState').textContent = status.jevConfigured ? 'Clave detectada · conexión sin probar' : 'Sin configurar';
  status.sources.forEach(source => { const button = node('button', `${source.available ? '↗' : '×'} ${source.path}`); button.disabled = !source.available; button.addEventListener('click', () => showSource(source.id)); $('sources').append(button); });
}).catch(error => { $('status').textContent = error.message; $('jevState').textContent = 'No disponible'; });
