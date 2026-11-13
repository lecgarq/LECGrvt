'use strict';
const $ = id => document.getElementById(id), NS = 'http://www.w3.org/2000/svg';
let data, byId, adjacency, selected = 'root', mode = 'knowledge', relationLimit = 40, selectionVersion = 0;
let box = [0,0,1000,600], drag = null, moved = false;
const el = (tag, text, cls) => { const n = document.createElement(tag); n.textContent = text; if (cls) n.className = cls; return n; };
const svg = (tag, attributes = {}, text = '') => { const n = document.createElementNS(NS, tag); Object.entries(attributes).forEach(([k,v]) => n.setAttribute(k, String(v))); n.textContent = text; return n; };
async function api(url) { const r = await fetch(url); if (!r.ok) { const b = await r.json().catch(() => ({})); throw new Error(b.error || `Consulta no disponible (HTTP ${r.status}).`); } return r.json(); }
function viewBox() { $('map').setAttribute('viewBox', box.join(' ')); }
function fit() { box = [0,0,1000,600]; viewBox(); }
function allowed(e) { return $('candidates').checked || e.state !== 'candidato'; }
function nearby(n) {
  if (!n.bounds || !$('candidates').checked) return [];
  const gap = b => Math.sqrt([0,1,2].reduce((s,i) => s + Math.max(0,n.bounds.min[i]-b.max[i],b.min[i]-n.bounds.max[i])**2,0));
  return data.nodes.filter(b => b.id !== n.id && b.bounds).map(b => ({node:b,gap:gap(b.bounds)})).filter(b => b.gap<=50)
    .sort((a,b) => a.gap-b.gap || a.node.id.localeCompare(b.node.id)).map(b => ({from:n.id,to:b.node.id,relation:'proximidad AABB',state:'candidato',evidence:`Separación entre envolventes: ${b.gap.toFixed(2)} mm. No es distancia entre acabados ni prueba de choque.`}));
}
function relations(n) { return [...(adjacency.get(n.id)||[]).filter(allowed),...nearby(n)]; }
function other(e,id=selected) { return byId.get(e.from === id ? e.to : e.from); }
function choose(id, switchView=false) {
  if (!byId.has(id)) return;
  selected=id; relationLimit=40; selectionVersion++; $('sourceReader').hidden=true; $('actionStatus').textContent='';
  if (switchView) mode='knowledge'; render(); detail();
}
function actionable(group, node) {
  group.setAttribute('role','button'); group.setAttribute('tabindex','0'); group.setAttribute('aria-label',node.label);
  group.append(svg('title',{},node.label+' · '+node.state));
  group.addEventListener('click',() => { if(!moved) choose(node.id); });
  group.addEventListener('keydown',e => { if(e.key==='Enter'||e.key===' '){e.preventDefault();choose(node.id);} });
  return group;
}
function renderKnowledge() {
  const center=byId.get(selected), edges=relations(center), neighbors=[...new Map(edges.map(e=>[other(e).id,other(e)])).values()];
  neighbors.sort((a,b)=>(a.kind==='root'?-1:0)-(b.kind==='root'?-1:0)||a.label.localeCompare(b.label));
  const shown=neighbors.slice(0,20), positions=new Map([[center.id,[500,300]]]);
  shown.forEach((n,i) => {const angle=(i/shown.length)*Math.PI*2-Math.PI/2;positions.set(n.id,[500+350*Math.cos(angle),300+220*Math.sin(angle)]);});
  for(const n of shown){const [x,y]=positions.get(n.id), e=edges.find(e=>other(e).id===n.id);$('map').append(svg('line',{x1:500,y1:300,x2:x,y2:y,class:'graph-line'+(e.state==='candidato'?' candidate':'')}));}
  for(const n of [center,...shown]){
    const [x,y]=positions.get(n.id), g=actionable(svg('g'),n);
    g.append(svg('circle',{cx:x,cy:y,r:n.id===selected?25:12,class:'graph-dot'+(n.id===selected?' selected':n.kind==='class'||n.path?.endsWith('.cs')?' code':'')}));
    const text=n.label.length>34?n.label.slice(0,31)+'…':n.label;
    g.append(svg('text',{x,y:y+(n.id===selected?49:30),class:'graph-label'},text));
    $('map').append(g);
  }
  $('mapStatus').textContent=`${shown.length} de ${neighbors.length} vecinos visibles · ${edges.length} relaciones. La lista de la ficha permite recorrer todas. Selecciona un nodo para seguir el camino.`;
}
function visibleSpatial() {return data.nodes.filter(n=>n.bounds&&($('level').value==='all'||n.level===$('level').value)&&($('category').value==='all'||n.category===$('category').value));}
function renderSpatial() {
  const items=visibleSpatial(), axes={xy:[0,1],xz:[0,2],yz:[1,2]}[$('projection').value], [a,b]=axes;
  if(!items.length){$('mapStatus').textContent='No hay envolventes en este filtro. Prueba otra categoría o todas las plantas.';return;}
  const lo=[Math.min(...items.map(n=>n.bounds.min[a])),Math.min(...items.map(n=>n.bounds.min[b]))],hi=[Math.max(...items.map(n=>n.bounds.max[a])),Math.max(...items.map(n=>n.bounds.max[b]))];
  const scale=Math.min(900/Math.max(hi[0]-lo[0],1),500/Math.max(hi[1]-lo[1],1));
  const x=v=>50+(v-lo[0])*scale,y=v=>550-(v-lo[1])*scale;
  const points=new Map(items.map(n=>[n.id,[x((n.bounds.min[a]+n.bounds.max[a])/2),y((n.bounds.min[b]+n.bounds.max[b])/2)]]));
  for(const n of items){const g=actionable(svg('g'),n),bounds=n.bounds;g.append(svg('rect',{x:x(bounds.min[a]),y:y(bounds.max[b]),width:Math.max((bounds.max[a]-bounds.min[a])*scale,.6),height:Math.max((bounds.max[b]-bounds.min[b])*scale,.6),class:'envelope'+(n.id===selected?' selected':'')}));$('map').append(g);}
  const edges=relations(byId.get(selected));
  for(const e of edges){const p1=points.get(e.from),p2=points.get(e.to);if(p1&&p2)$('map').append(svg('line',{x1:p1[0],y1:p1[1],x2:p2[0],y2:p2[1],class:'graph-line'+(e.state==='candidato'?' candidate':'')}));}
  for(const n of items){const [cx,cy]=points.get(n.id),g=actionable(svg('g'),n);g.append(svg('circle',{cx,cy,r:n.id===selected?6:3,class:'spatial-dot'+(n.id===selected?' selected':'')}));$('map').append(g);}
  $('map').append(svg('text',{x:50,y:587,class:'axis-label'},`${$('projection').selectedOptions[0].textContent} · extensión ${(hi[0]-lo[0]).toFixed(0)} × ${(hi[1]-lo[1]).toFixed(0)} mm · envolventes del modelo`));
  const selectedVisible=points.has(selected);
  $('mapStatus').textContent=`${items.length} envolventes. ${selectedVisible?'Elemento seleccionado visible.':'El nodo seleccionado no está representado en este filtro.'} Las cajas no son contornos reales; no permiten certificar encuentros.`;
}
function render() {
  if(!data)return;
  $('knowledge').setAttribute('aria-pressed',mode==='knowledge');$('spatial').setAttribute('aria-pressed',mode==='spatial');$('spatialFilters').hidden=mode!=='spatial';
  $('mapKind').textContent=mode==='spatial'?'PROYECCIÓN DEL MODELO':'MAPA DE RELACIONES';$('mapTitle').textContent=mode==='spatial'?'Casa Eucalipto · envolventes':byId.get(selected).label;
  $('map').replaceChildren(); if(mode==='spatial')renderSpatial();else renderKnowledge();viewBox();
}
function detail() {
  if(!data)return;
  const n=byId.get(selected);$('nodeTitle').textContent=n.label;$('nodeState').textContent=n.state;$('metadata').replaceChildren();
  const rows=[['Tipo',n.kind],['Archivo',n.path],['Líneas',n.start?`${n.start}–${n.end}`:null],['Elemento',n.elementId],['UniqueId',n.uniqueId],['Nivel',n.kind==='element'?(n.level||'Sin confirmar'):n.level],['Categoría',n.category],['SHA256',n.hash]];
  if(n.bounds)rows.push(['Envolvente mínima XYZ (mm)',n.bounds.min.map(v=>v.toFixed(2)).join(', ')],['Envolvente máxima XYZ (mm)',n.bounds.max.map(v=>v.toFixed(2)).join(', ')]);
  rows.filter(([,v])=>v!==null&&v!==undefined&&v!=='').forEach(([k,v])=>$('metadata').append(el('dt',k),el('dd',String(v))));
  $('locate').disabled=!n.bounds;$('read').disabled=!n.path||!(/\.(cs|md|csproj|props)$/i.test(n.path));$('copyContext').disabled=false;$('downloadContext').disabled=false;
  const edges=relations(n);$('neighbors').replaceChildren();$('edgeTitle').textContent=`Relaciones (${edges.length})`;
  edges.slice(0,relationLimit).forEach(e=>{const row=el('div','','edge'),otherNode=other(e);row.append(el('span',`${e.from===selected?'→':'←'} ${e.relation} · ${e.state}`,'tag'+(e.state==='candidato'?' candidate':'')));const button=el('button',otherNode.label);button.addEventListener('click',()=>choose(otherNode.id,true));row.append(button);if(e.evidence)row.append(el('small',e.evidence));$('neighbors').append(row);});
  $('more').hidden=edges.length<=relationLimit;
}
function search() {
  const normalize=s=>s.normalize('NFD').replace(/[\u0300-\u036f]/g,'').toLowerCase();const q=normalize($('find').value.trim());$('matches').replaceChildren();
  if(!q){$('searchCount').textContent='Busca un concepto, nombre o ID. También puedes navegar por los nodos.';return;}
  const terms=q.split(/\s+/),matches=data.nodes.filter(n=>terms.every(t=>normalize(`${n.label} ${n.path||''} ${n.uniqueId||''}`).includes(t)));
  $('searchCount').textContent=`${matches.length} coincidencias · mostrando ${Math.min(60,matches.length)}`;
  matches.slice(0,60).forEach(n=>{const button=el('button',`${n.kind==='element'?'▣':'↗'} ${n.label}`);button.addEventListener('click',()=>{choose(n.id,true);});$('matches').append(button);});
}
function download(content,name,type){const url=URL.createObjectURL(new Blob([content],{type})),a=document.createElement('a');a.href=url;a.download=name;document.body.append(a);a.click();a.remove();setTimeout(()=>URL.revokeObjectURL(url),1000);}
$('find').addEventListener('input',()=>{if(data)search();});
document.querySelectorAll('[data-node]').forEach(button=>button.addEventListener('click',()=>{if(data){fit();choose(button.dataset.node,true);}}));
$('knowledge').addEventListener('click',()=>{mode='knowledge';fit();render();});$('spatial').addEventListener('click',()=>{mode='spatial';fit();render();});
['level','category','projection'].forEach(id=>$(id).addEventListener('change',()=>{fit();render();}));$('candidates').addEventListener('change',()=>{render();detail();});
$('relations').addEventListener('click',()=>{mode='knowledge';fit();render();});
$('locate').addEventListener('click',()=>{const n=byId.get(selected);mode='spatial';$('level').value=n.level||'all';$('category').value=n.category||'all';fit();render();$('map').scrollIntoView({block:'center'});});
$('more').addEventListener('click',()=>{relationLimit+=40;detail();});
$('read').addEventListener('click',async()=>{const n=byId.get(selected),ticket=selectionVersion;try{const source=await api(`/api/graph/source?id=${encodeURIComponent(n.id)}${n.hash?'&hash='+encodeURIComponent(n.hash):''}`);if(ticket!==selectionVersion)return;$('sourceMeta').textContent=`${source.spec.path} · SHA256 ${source.hash}`;$('sourceText').replaceChildren(...source.lines.map((line,i)=>el('span',`${i+1}  ${line}`,n.start&&i+1>=n.start&&i+1<=n.end?'highlight':'')));$('sourceReader').hidden=false;const start=$('sourceText').querySelector('.highlight');$('sourceText').scrollTop=start?start.offsetTop-$('sourceText').offsetTop:0;$('sourceReader').scrollIntoView({block:'nearest'});}catch(e){$('actionStatus').textContent=e.message;}});
async function contextAction(copy){const id=selected;try{const r=await api('/api/graph/context?id='+encodeURIComponent(id));if(id!==selected)return;if(copy){await navigator.clipboard.writeText(r.context);$('actionStatus').textContent='Contexto del nodo copiado para este chat.';}else{download(r.context,'lecg-grafo-contexto.md','text/markdown;charset=utf-8');$('actionStatus').textContent='Contexto preparado para descarga.';}}catch(e){$('actionStatus').textContent=e.message+' Puedes intentar descargar el contexto.';}}
$('copyContext').addEventListener('click',()=>contextAction(true));$('downloadContext').addEventListener('click',()=>contextAction(false));
$('exportJson').addEventListener('click',()=>download(JSON.stringify(data,null,2),'lecg-grafo.json','application/json'));
$('exportSvg').addEventListener('click',async()=>{try{const copy=$('map').cloneNode(true);copy.setAttribute('width','1000');copy.setAttribute('height','600');const style=svg('style',{},await (await fetch('/graph.css')).text());copy.prepend(style);const bg=svg('rect',{x:box[0],y:box[1],width:box[2],height:box[3],fill:'#0d1a20'});copy.insertBefore(bg,style.nextSibling);download(new XMLSerializer().serializeToString(copy),'lecg-grafo-vista.svg','image/svg+xml');}catch(e){$('actionStatus').textContent=e.message;}});
function zoom(factor){const w=Math.max(100,Math.min(3000,box[2]*factor)),h=w*.6;box=[box[0]+(box[2]-w)/2,box[1]+(box[3]-h)/2,w,h];viewBox();}
$('zoomIn').addEventListener('click',()=>zoom(.8));$('zoomOut').addEventListener('click',()=>zoom(1.25));$('fit').addEventListener('click',fit);
$('map').addEventListener('wheel',e=>{e.preventDefault();zoom(e.deltaY>0?1.15:1/1.15);},{passive:false});
$('map').addEventListener('pointerdown',e=>{drag={x:e.clientX,y:e.clientY,box:[...box]};moved=false;});
$('map').addEventListener('pointermove',e=>{if(!drag)return;const dx=e.clientX-drag.x,dy=e.clientY-drag.y;if(Math.abs(dx)+Math.abs(dy)>4)moved=true;const rect=$('map').getBoundingClientRect();box=[drag.box[0]-dx*drag.box[2]/rect.width,drag.box[1]-dy*drag.box[3]/rect.height,drag.box[2],drag.box[3]];viewBox();});
window.addEventListener('pointerup',()=>drag=null);$('map').addEventListener('pointerleave',()=>drag=null);
api('/api/graph').then(result=>{
  data=result;byId=new Map(data.nodes.map(n=>[n.id,n]));adjacency=new Map();data.edges.forEach(e=>{for(const id of [e.from,e.to]){if(!adjacency.has(id))adjacency.set(id,[]);adjacency.get(id).push(e);}});
  $('summary').textContent=`${data.nodes.length.toLocaleString('es-MX')} nodos · ${data.edges.length.toLocaleString('es-MX')} relaciones. ${data.nodes.filter(n=>n.bounds).length.toLocaleString('es-MX')} envolventes del edificio. Documentos, declaraciones de código y elementos enlazados por evidencia.`;
  $('dates').textContent=`Modelo: ${data.model}. Captura BIM: ${new Date(data.modelCapturedUtc).toLocaleString('es-MX')}. Índice de código: ${new Date(data.codeCapturedUtc).toLocaleString('es-MX')}.`;
  data.limits.forEach(t=>$('limits').append(el('li',t)));
  [...new Set(data.nodes.map(n=>n.level).filter(Boolean))].sort().forEach(l=>{const option=el('option',l);option.value=l;$('level').append(option);});
  data.nodes.filter(n=>n.kind==='category').sort((a,b)=>a.label.localeCompare(b.label)).forEach(n=>{const option=el('option',n.label);option.value=n.id.slice('category:'.length);$('category').append(option);});
  $('level').value='01 - PLANTA BAJA';$('category').value='OST_Walls';$('exportJson').disabled=false;$('exportSvg').disabled=false;
  choose('root');
}).catch(e=>{$('summary').textContent='No se pudo cargar el grafo.';$('mapStatus').textContent=e.message;});
