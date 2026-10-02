import * as maplibregl from './vendor/maplibre-gl.mjs';
import { validateSnapshot, collection, routeFeatures, interpolate, heading, canInterpolate, normalizeLongitude, continuousCapture, labelDescriptors } from './map-math.mjs';

const bridge = window.chrome?.webview;
const empty = () => collection([]);
const $ = id => document.getElementById(id);
let map, epoch=0, snapshot=null, sequence=0, initialized=false, failed=false;
let selected=null, followId=null, restored=false, fitted=false, reducedMotion=false;
let tracks=new Map(), labelImages=[], labelRecords=[], labelSignature='', routeSignature='', solarSignature='', lastArrival=null;
let sourceUpdates=0, frameIntervals=[], previousFrame=0, lastRender=0, cameraTimer;
let presentationVisible=true, animationFrame=0, awaitingVisiblePaint=false, pendingPaint=null;
const enabled=new Set(['Basemap','DayNight','Airports','Routes','Aircraft','Selection','Labels']);
const media=matchMedia('(prefers-reduced-motion: reduce)');
const send = (type,body={}) => bridge?.postMessage({type,schemaVersion:1,epoch,...body});
const fail = error => {
  if (!failed) {
    failed=true;
    const reason=String(error?.error?.message??error?.message??error?.reason??'renderer-error').replace(/data:[^\s)]+/gi,'data:[redacted]').slice(0,160);
    send('error',{reason});
    $('selection').textContent='Map unavailable';
  }
};
window.addEventListener('error',fail);
window.addEventListener('unhandledrejection',fail);
const pointFeature = (id,p,properties={}) => ({type:'Feature',properties:{id,...properties},geometry:{type:'Point',coordinates:[p.longitude,p.latitude]}});
const setData = (id,data) => { map.getSource(id).setData(data); sourceUpdates++; };

function camera() {
  const c=map.getCenter();
  return {longitude:normalizeLongitude(c.lng),latitude:Math.max(-85.051129,Math.min(85.051129,c.lat)),zoom:map.getZoom(),bearing:normalizeLongitude(map.getBearing()),followId};
}
function reportCamera() {
  clearTimeout(cameraTimer);
  if(!presentationVisible || document.hidden) return;
  cameraTimer=setTimeout(()=>{ if(initialized) send('camera',camera()); },120);
}
function move(options) {
  map.easeTo({...options,duration:reducedMotion||media.matches?0:350});
}
function world() { followId=null; updateFollow(); move({center:[0,20],zoom:0.6,bearing:0,pitch:0}); }
function fitRoute() {
  const points=snapshot?.routes.filter(r=>['Planned','CompletedPlan','Diversion'].includes(r.kind)).flatMap(r=>r.segments.flat()) ?? [];
  if (!points.length) return false;
  // Unwrap around the first endpoint so dateline flights use the short view.
  const anchor=points[0].longitude;
  const bounds=new maplibregl.LngLatBounds();
  for (const p of points) bounds.extend([anchor+normalizeLongitude(p.longitude-anchor),Math.max(-85,Math.min(85,p.latitude))]);
  map.fitBounds(bounds,{padding:{top:65,bottom:50,left:55,right:55},maxZoom:7,duration:reducedMotion||media.matches?0:400});
  return true;
}
function focus() {
  followId=null; updateFollow();
  const entity=selected?.kind==='Airport' ? snapshot?.airports.find(a=>a.id===selected.id) : snapshot?.aircraft.find(a=>a.id===selected?.id) ?? snapshot?.aircraft[0];
  if (entity) move({center:[entity.position.longitude,entity.position.latitude],zoom:Math.max(5,map.getZoom())});
  else fitRoute();
}
function updateFollow() { $('follow').setAttribute('aria-pressed',String(!!followId)); }
function selectionText() {
  const a=selected?.kind==='Aircraft' && snapshot?.aircraft.find(a=>a.id===selected.id);
  const airport=selected?.kind==='Airport' && snapshot?.airports.find(a=>a.id===selected.id);
  const route=selected?.kind==='Route' && snapshot?.routes.find(r=>r.id===selected.id);
  $('selection').textContent=a ? `${a.callsign||a.registration||a.id} · ${a.source} · ${a.reliability}${a.headingDegrees==null?' · Heading unavailable':` · ${Math.round(a.headingDegrees)}°`}`
    : airport ? `${airport.icao||airport.iata||airport.id} · ${airport.role||'Airport'} · ${airport.name}\nOperational status: ${airport.operationalStatus}`
    : route ? `${route.originId||'Route'} → ${route.destinationId||''} · ${route.kind.replace(/([a-z])([A-Z])/g,'$1 $2')} · ${route.source}`
    : snapshot?.aircraft.length ? `${snapshot.aircraft.length} aircraft · Select an aircraft, airport or route` : snapshot?.routes.length ? 'Assigned route · Aircraft position unavailable' : 'Current flight geography unavailable';
}
function select(kind,id) {
  selected={kind,id}; selectionText(); renderAircraft(performance.now());
  map.setFilter('airport-selected',['==',['get','id'],kind==='Airport'?id:'']);
  map.setFilter('route-selected',['==',['get','id'],kind==='Route'?id:'']);
  send('selection',{sequence,kind,id});
}
function layerVisibility() {
  const groups={DayNight:['night'],Airports:['airports','airport-selected'],Routes:['planned','completed','observed','inferred','route-selected','waypoints'],Aircraft:['aircraft'],Labels:['labels']};
  for (const [group,layers] of Object.entries(groups)) for (const layer of layers) if(map.getLayer(layer)) map.setLayoutProperty(layer,'visibility',enabled.has(group)?'visible':'none');
  for (const input of document.querySelectorAll('[data-layer]')) input.checked=enabled.has(input.dataset.layer);
}

function imageFromCanvas(canvas) { const c=canvas.getContext('2d'); return c.getImageData(0,0,canvas.width,canvas.height); }
function aircraftImage() {
  const canvas=document.createElement('canvas'); canvas.width=64; canvas.height=64;
  const c=canvas.getContext('2d'); c.fillStyle='#ffffff';
  c.beginPath(); c.moveTo(32,5); c.lineTo(37,25); c.lineTo(57,40); c.lineTo(57,45); c.lineTo(36,36); c.lineTo(35,49); c.lineTo(43,55); c.lineTo(43,59); c.lineTo(32,55); c.lineTo(21,59); c.lineTo(21,55); c.lineTo(29,49); c.lineTo(28,36); c.lineTo(7,45); c.lineTo(7,40); c.lineTo(27,25); c.closePath(); c.fill();
  return imageFromCanvas(canvas);
}
function positionImage() {
  const canvas=document.createElement('canvas'); canvas.width=64; canvas.height=64;
  const c=canvas.getContext('2d'); c.strokeStyle='#ffffff'; c.lineWidth=5;
  c.beginPath(); c.arc(32,32,13,0,Math.PI*2); c.stroke();
  c.fillStyle='#ffffff'; c.beginPath(); c.arc(32,32,3,0,Math.PI*2); c.fill();
  return imageFromCanvas(canvas);
}
function labelImage(text,color) {
  const canvas=document.createElement('canvas'); const c=canvas.getContext('2d'); c.font='22px Segoe UI';
  canvas.width=Math.min(700,Math.ceil(c.measureText(text).width)+18); canvas.height=38;
  c.font='22px Segoe UI'; c.lineWidth=6; c.strokeStyle='#090c0f'; c.strokeText(text,8,27); c.fillStyle=color; c.fillText(text,8,27);
  return imageFromCanvas(canvas);
}
function updateLabels() {
  const descriptors=labelDescriptors(snapshot), signature=JSON.stringify(descriptors);
  if(signature===labelSignature) return;
  labelSignature=signature;
  setData('labels',empty());
  for (const id of labelImages) if (map.hasImage(id)) map.removeImage(id);
  labelImages=[];
  labelRecords=descriptors.map(record=>{
    const id=`label-${labelImages.length}`;
    labelImages.push(id); map.addImage(id,labelImage(record.text,record.color),{pixelRatio:2});
    return {...record,id};
  });
}
function updateSolar(sun) {
  const signature=sun.map(v=>v.toFixed(3)).join(','); if(signature===solarSignature) return; solarSignature=signature;
  const canvas=document.createElement('canvas'); canvas.width=512; canvas.height=256;
  const context=canvas.getContext('2d'), pixels=context.createImageData(512,256);
  for(let y=0;y<256;y++) {
    const lat=Math.atan(Math.sinh(Math.PI*(1-2*(y+0.5)/256)));
    for(let x=0;x<512;x++) {
      const lon=((x+0.5)/512*360-180)*Math.PI/180;
      const dot=Math.cos(lat)*Math.cos(lon)*sun[0]+Math.cos(lat)*Math.sin(lon)*sun[1]+Math.sin(lat)*sun[2];
      const i=(y*512+x)*4; pixels.data[i]=2; pixels.data[i+1]=5; pixels.data[i+2]=9;
      pixels.data[i+3]=Math.round(115*Math.max(0,Math.min(1,(0.07-dot)/0.24)));
    }
  }
  context.putImageData(pixels,0,0);
  const url=canvas.toDataURL();
  if(map.getSource('night')) map.getSource('night').updateImage({url});
  else {
    map.addSource('night',{type:'image',url,coordinates:[[-180,85.051129],[180,85.051129],[180,-85.051129],[-180,-85.051129]]});
    map.addLayer({id:'night',type:'raster',source:'night',paint:{'raster-opacity':1,'raster-fade-duration':0}},'planned');
  }
}

function renderAircraft(now) {
  if(!snapshot || !presentationVisible || document.hidden) return;
  const features=[], positions=new Map();
  for(const a of snapshot.aircraft) {
    const track=tracks.get(a.id); let p=a.position, h=a.headingDegrees??0;
    // Reduced motion snaps to the newest observation. Do not leave an animation
    // pending: otherwise the 20 Hz loop resubmits the same sources indefinitely.
    if(track?.previous && (reducedMotion || media.matches)) track.previous=null;
    if(track?.previous && now-lastArrival<=15000 && !reducedMotion && !media.matches) {
      const t=Math.max(0,Math.min(1,(now-track.at)/track.duration));
      p=interpolate(track.previous.position,a.position,t);
      h=heading(track.previous.headingDegrees??h,h,t);
      if(t>=1) track.previous=null;
    }
    positions.set(a.id,p);
    features.push(pointFeature(a.id,p,{heading:h,symbol:a.headingDegrees==null?'position':'plane',source:a.source,stale:a.reliability!=='Current',selected:selected?.kind==='Aircraft'&&selected.id===a.id||a.highlighted}));
    if(followId===a.id) map.jumpTo({center:[p.longitude,p.latitude]});
  }
  setData('aircraft',collection(features));
  setData('labels',collection(labelRecords.flatMap(record=>{
    const p=record.position??positions.get(record.aircraftId);
    return p ? [pointFeature(record.id,p,{image:record.id})] : [];
  })));
}
function animate(now) {
  animationFrame=0;
  if(!presentationVisible || document.hidden) return;
  if(!failed && initialized && now-lastRender>=50) {
    if(previousFrame) { frameIntervals.push(now-previousFrame); if(frameIntervals.length>300) frameIntervals.shift(); }
    previousFrame=now; lastRender=now;
    if(now-lastArrival<=15000 && [...tracks.values()].some(t=>t.previous)) renderAircraft(now);
  }
  animationFrame=requestAnimationFrame(animate);
}
function setPresentationVisible(visible) {
  presentationVisible=visible;tracks.clear();previousFrame=0;
  if(animationFrame)cancelAnimationFrame(animationFrame);animationFrame=0;
  clearTimeout(cameraTimer);
  if(pendingPaint){map.off('idle',pendingPaint);pendingPaint=null;}
  if(!visible)map.stop();
  else {awaitingVisiblePaint=true;map.resize();animationFrame=requestAnimationFrame(animate);}
}
function applySnapshot(next) {
  validateSnapshot(next,epoch,sequence);
  const firstSnapshot=sequence===0 || awaitingVisiblePaint;
  const now=performance.now(), nextTracks=new Map();
  const continuous=!awaitingVisiblePaint && !document.hidden && continuousCapture(snapshot?.capturedAt,next.capturedAt,lastArrival==null?Infinity:now-lastArrival);
  const oldAircraft=new Map(snapshot?.aircraft.map(a=>[a.id,a])??[]);
  for(const a of next.aircraft) {
    const old=oldAircraft.get(a.id);
    const previous=continuous && canInterpolate(old,a) ? old : null;
    const existing=tracks.get(a.id);
    const unchanged=old && old.observedAt===a.observedAt && old.sourceGeneration===a.sourceGeneration && old.continuityId===a.continuityId && old.source===a.source && a.interpolationAllowed && a.reliability==='Current';
    nextTracks.set(a.id,unchanged && continuous && existing ? existing : {previous,at:now,duration:previous?Math.min(1500,Date.parse(a.observedAt)-Date.parse(previous.observedAt)):0});
  }
  tracks=nextTracks; snapshot=next; lastArrival=now;
  const signature=JSON.stringify([next.routes,next.airports]);
  if(signature!==routeSignature) {
    routeSignature=signature; setData('routes',routeFeatures(next.routes));
    setData('airports',collection(next.airports.map(a=>pointFeature(a.id,a.position,{role:a.role||'airport'}))));
    setData('waypoints',collection(next.routes.flatMap(r=>r.waypoints.map(w=>pointFeature(w.id,w.position)))));
  }
  updateLabels();
  updateSolar(next.sunVector); layerVisibility(); renderAircraft(now);
  sequence=next.sequence;
  if(followId && !next.aircraft.some(a=>a.id===followId)) { followId=null; updateFollow(); }
  selectionText();
  if(!restored && !fitted) fitted=fitRoute();
  const acknowledge=()=>send('ack',{generation:next.generation,sequence:next.sequence});
  if(firstSnapshot) {
    // Image loading and source updates are asynchronous. Only the initial
    // reveal waits for a completed frame; normal streaming keeps its cadence.
    const firstPaint=()=>{
      if(failed || !presentationVisible || !map.getSource('night')?.loaded()) return;
      map.off('idle',firstPaint);
      pendingPaint=null;awaitingVisiblePaint=false;
      acknowledge();
    };
    pendingPaint=firstPaint;
    map.on('idle',firstPaint);
  } else acknowledge();
}

async function initialize(message) {
  if(initialized || epoch || !Number.isSafeInteger(message.epoch) || message.epoch<=0) return;
  epoch=message.epoch;
  const palette=await fetch('./theme.json').then(r=>{if(!r.ok)throw Error();return r.json();});
  map=new maplibregl.Map({container:'map',attributionControl:false,center:[0,20],zoom:0.6,maxZoom:12,minZoom:0,renderWorldCopies:true,style:{version:8,sources:{land:{type:'geojson',data:'./land.geojson'}},layers:[{id:'ocean',type:'background',paint:{'background-color':palette['S4.AppBackground']}},{id:'land',type:'fill',source:'land',paint:{'fill-color':palette['S4.SurfaceElevated']}},{id:'coast',type:'line',source:'land',paint:{'line-color':palette['S4.Divider'],'line-width':0.6}}]}});
  map.on('error',fail);
  map.getCanvas().addEventListener('webglcontextlost',fail);
  await new Promise(resolve=>map.once('load',resolve));
  for(const id of ['routes','airports','aircraft','waypoints','labels']) map.addSource(id,{type:'geojson',data:empty()});
  const routeLayer=(id,kinds,color,width,dash)=>map.addLayer({id,type:'line',source:'routes',filter:['in',['get','kind'],['literal',kinds]],paint:{'line-color':color,'line-width':width,...(dash?{'line-dasharray':dash}:{})}});
  routeLayer('planned',['Planned','Diversion'],palette['S4.SierraYellow'],1.7);
  routeLayer('completed',['CompletedPlan'],palette['S4.TextMuted'],1.5);
  routeLayer('observed',['ObservedTrack'],palette['S4.Information'],2);
  routeLayer('inferred',['InferredTrack'],palette['S4.Warning'],2,[3,3]);
  map.addLayer({id:'route-selected',type:'line',source:'routes',filter:['==',['get','id'],''],paint:{'line-color':palette['S4.SierraYellow'],'line-width':4,'line-opacity':0.35}});
  map.addLayer({id:'waypoints',type:'circle',source:'waypoints',minzoom:3,paint:{'circle-radius':2,'circle-color':palette['S4.TextMuted']}});
  map.addLayer({id:'airports',type:'circle',source:'airports',paint:{'circle-radius':4,'circle-color':palette['S4.SurfacePrimary'],'circle-stroke-color':palette['S4.TextSecondary'],'circle-stroke-width':1.5}});
  map.addLayer({id:'airport-selected',type:'circle',source:'airports',filter:['==',['get','id'],''],paint:{'circle-radius':7,'circle-color':palette['S4.SierraYellow'],'circle-opacity':0.5}});
  map.addImage('plane',aircraftImage(),{sdf:true,pixelRatio:2});
  map.addImage('position',positionImage(),{sdf:true,pixelRatio:2});
  map.addLayer({id:'aircraft',type:'symbol',source:'aircraft',layout:{'icon-image':['get','symbol'],'icon-size':0.8,'icon-rotate':['get','heading'],'icon-rotation-alignment':'map','icon-allow-overlap':true,'icon-ignore-placement':true},paint:{'icon-color':['case',['get','selected'],palette['S4.SierraYellow'],['==',['get','source'],'Inferred'],palette['S4.Warning'],palette['S4.TextPrimary']],'icon-opacity':['case',['get','stale'],0.45,1],'icon-halo-color':palette['S4.AppBackground'],'icon-halo-width':1}});
  map.addLayer({id:'labels',type:'symbol',source:'labels',layout:{'icon-image':['get','image'],'icon-anchor':'left','icon-offset':[9,0],'icon-allow-overlap':false,'icon-padding':8}});
  applyPresentation(message.presentation);
  layerVisibility(); updateFollow();
  map.on('moveend',reportCamera);
  map.on('dragstart',()=>{followId=null;updateFollow();});
  map.on('click',event=>{
    const feature=map.queryRenderedFeatures(event.point,{layers:['aircraft','airports','planned','completed','observed','inferred']})[0];
    if(feature) select(feature.layer.id==='aircraft'?'Aircraft':feature.layer.id==='airports'?'Airport':'Route',feature.properties.id);
  });
  initialized=true; send('initialized'); animationFrame=requestAnimationFrame(animate);
}

function applyPresentation(p) {
  if(p) {
    reducedMotion=p.reducedMotion===true;
    if(Array.isArray(p.enabledLayers)) { enabled.clear(); for(const layer of p.enabledLayers) enabled.add(layer); }
    selected=p.selection??null;
    const c=p.camera;
    if(p.restoreCamera===true && c && Number.isFinite(c.center?.longitude) && Number.isFinite(c.center?.latitude) && Number.isFinite(c.zoom) && Number.isFinite(c.bearing)) {
      map.jumpTo({center:[normalizeLongitude(c.center.longitude),Math.max(-85,Math.min(85,c.center.latitude))],zoom:Math.max(0,Math.min(12,c.zoom)),bearing:normalizeLongitude(c.bearing),pitch:0});
      followId=c.followAircraftId??null; restored=true;
    }
  }
  $('reduced-motion').checked=reducedMotion;
  map.setFilter('airport-selected',['==',['get','id'],selected?.kind==='Airport'?selected.id:'']);
  map.setFilter('route-selected',['==',['get','id'],selected?.kind==='Route'?selected.id:'']);
  layerVisibility(); updateFollow();
}

bridge?.addEventListener('message',event=>{
  try {
    const m=event.data;
    if(!m || m.schemaVersion!==1) throw Error();
    if(m.type==='initialize') { initialize(m).catch(fail); return; }
    if(m.epoch!==epoch) return;
    if(m.type==='visibility' && initialized && !failed) setPresentationVisible(m.visible===true);
    if(m.type==='snapshot' && initialized && !failed && presentationVisible) applySnapshot(m.snapshot);
    if(m.type==='presentation' && initialized && !failed) applyPresentation(m.presentation);
  } catch(error) { tracks.clear(); fail(error); }
});
$('zoom-in').onclick=()=>map&&move({zoom:Math.min(12,map.getZoom()+1)});
$('zoom-out').onclick=()=>map&&move({zoom:Math.max(0,map.getZoom()-1)});
$('home').onclick=()=>map&&world();
$('focus').onclick=()=>map&&focus();
$('follow').onclick=()=>{
  if(!map) return;
  followId=followId?null:snapshot?.aircraft.find(a=>a.id===selected?.id)?.id??snapshot?.aircraft[0]?.id??null;
  updateFollow(); renderAircraft(performance.now()); reportCamera();
};
$('layers').onclick=()=>{const open=$('layer-panel').hidden;$('layer-panel').hidden=!open;$('layers').setAttribute('aria-expanded',String(open));};
const reportPreferences=()=>{if(initialized)send('presentationPreferences',{enabledLayers:[...enabled],reducedMotion});};
for(const input of document.querySelectorAll('[data-layer]')) input.onchange=()=>{if(input.checked)enabled.add(input.dataset.layer);else enabled.delete(input.dataset.layer);if(map&&initialized)layerVisibility();reportPreferences();};
$('reduced-motion').onchange=()=>{reducedMotion=$('reduced-motion').checked;tracks.clear();if(map)renderAircraft(performance.now());reportPreferences();};
document.addEventListener('keydown',event=>{if(event.key==='Escape'){event.preventDefault();$('layer-panel').hidden=true;send('bypass');}});
document.addEventListener('visibilitychange',()=>{
  tracks.clear();previousFrame=0;
  if(animationFrame)cancelAnimationFrame(animationFrame);animationFrame=0;
  if(document.hidden){map?.stop();clearTimeout(cameraTimer);}
  else if(presentationVisible&&map){map.resize();animationFrame=requestAnimationFrame(animate);}
});
new ResizeObserver(()=>{if(presentationVisible&&!document.hidden)map?.resize();}).observe(document.body);
// Read-only coordinates let the explicit desktop diagnostic harness exercise real
// browser hit testing. No operational commands or mutable map objects are exposed.
function diagnosticPoints() {
  if(!map || !snapshot) return [];
  const rect=$('map').getBoundingClientRect();
  return [...snapshot.aircraft.map(a=>({kind:'Aircraft',id:a.id,position:a.position})),
    ...snapshot.airports.map(a=>({kind:'Airport',id:a.id,position:a.position})),
    ...snapshot.routes.filter(r=>r.segments[0]?.length>1).map(r=>({kind:'Route',id:r.id,position:r.segments[0][Math.floor(r.segments[0].length/2)]}))]
    .slice(0,80).map(a=>{const p=map.project([a.position.longitude,a.position.latitude]);return {kind:a.kind,id:a.id,x:p.x+rect.left,y:p.y+rect.top};});
}
window.sierraMapDiagnostics=Object.freeze({read:()=>({counts:{aircraft:snapshot?.aircraft.length??0,airports:snapshot?.airports.length??0,routes:snapshot?.routes.length??0,labels:labelRecords.length},sourceIDs:snapshot?.aircraft.map(a=>({id:a.id,source:a.source}))??[],lastSequence:sequence,frameIntervals:[...frameIntervals],sourceUpdates,selection:selected,solarSignature,points:diagnosticPoints(),selectionFilters:map?{airport:map.getFilter('airport-selected'),route:map.getFilter('route-selected')}:null,camera:map?camera():null})});
send('ready');
