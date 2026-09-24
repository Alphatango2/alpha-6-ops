import {Map,NavigationControl,setWorkerUrl,LngLat} from './vendor/maplibre-gl.mjs';
setWorkerUrl(new URL('./vendor/maplibre-gl-worker.mjs',import.meta.url).href);
const send=(type,body={})=>window.chrome.webview.postMessage({type,...body});
window.addEventListener('error',e=>send('error',{message:e.message}));
window.addEventListener('unhandledrejection',e=>send('error',{message:String(e.reason)}));
const theme=await (await fetch('theme.json')).json();
for(const [css,key] of Object.entries({ocean:'AppBackground',surface:'SurfacePrimary',text:'TextPrimary',muted:'TextMuted',yellow:'SierraYellow',border:'Divider'}))document.documentElement.style.setProperty('--'+css,theme['S4.'+key]);
const colors={ocean:theme['S4.AppBackground'],land:theme['S4.SurfaceElevated'],border:theme['S4.Divider'],text:theme['S4.TextSecondary'],muted:theme['S4.TextMuted'],yellow:theme['S4.SierraYellow']};
const collection=features=>({type:'FeatureCollection',features});
const feature=(id,coordinates,properties,type='Point')=>({type:'Feature',id,geometry:{type,coordinates},properties:{id,...properties}});
const map=new Map({container:'map',center:[55,18],zoom:1.6,minZoom:0.5,maxZoom:12,attributionControl:false,renderWorldCopies:false,
  style:{version:8,sources:{land:{type:'geojson',data:'land.geojson'}},layers:[{id:'ocean',type:'background',paint:{'background-color':colors.ocean}},
    {id:'land',type:'fill',source:'land',paint:{'fill-color':colors.land}},
    {id:'coastline',type:'line',source:'land',paint:{'line-color':colors.border,'line-width':0.65}}]}});
map.addControl(new NavigationControl({showCompass:false}),'top-left');
const mapErrors=[];map.on('error',e=>mapErrors.push(String(e.error)));
let snapshot,resolveSnapshot;const snapshotReady=new Promise(r=>resolveSnapshot=r);
let selected='synthetic-001',count=1,follow=false,routesVisible=true,nightVisible=false,lastSet=0,renderEvents=0;
let current=[];const animationIntervals=[];let benchmarkFrames=null;let benchmarkStart=0;let benchmarkUpdates=0;let renderedUpdates=0;let previousFrame=performance.now();
function icon(color){const c=document.createElement('canvas');c.width=c.height=40;const x=c.getContext('2d');x.fillStyle=color;x.beginPath();x.moveTo(20,2);x.lineTo(24,16);x.lineTo(37,25);x.lineTo(37,29);x.lineTo(23,24);x.lineTo(23,33);x.lineTo(28,37);x.lineTo(28,39);x.lineTo(20,36);x.lineTo(12,39);x.lineTo(12,37);x.lineTo(17,33);x.lineTo(17,24);x.lineTo(3,29);x.lineTo(3,25);x.lineTo(16,16);x.closePath();x.fill();return x.getImageData(0,0,40,40);}
function setFollow(value){follow=value&&selected!==null;if(follow)map.setRenderWorldCopies(true);document.getElementById('follow').textContent='Follow: '+(follow?'on':'off');}
function setSelection(kind,id){selected=kind==='Aircraft'?id:null;if(!selected)setFollow(false);document.getElementById('selection').textContent=`${kind}: ${id}\nSource: Simulated / synthetic prototype`;return{kind,id};}
function samplePosition(i,time){
  if(i===0)return[151.1772-time*.04,-33.9461+time*.025];
  return[((i*137.507764+time*.06)%340)-170,((i*47.331)%130)-65];
}
function aircraft(time){
  // Interpolate between generated 2Hz samples with a 500ms presentation delay.
  // These are synthetic renderer inputs, not FlightSessionCoordinator telemetry.
  const sample=Math.floor(time*2)/2,blend=(time-sample)*2;
  return collection(Array.from({length:count},(_,i)=>{
    const a=samplePosition(i,Math.max(0,sample-.5)),b=samplePosition(i,sample);let delta=b[0]-a[0];if(delta>180)delta-=360;if(delta< -180)delta+=360;
    const lon=((a[0]+delta*blend+540)%360)-180;const id=i===0?'synthetic-001':'synthetic-'+i;
    return feature(id,[lon,a[1]+(b[1]-a[1])*blend],{id,heading:i===0?315:i*37%360,source:'Simulated',icon:id===selected?'selected-plane':'simulated-plane'});
  }));
}
// NOAA approximate solar equations; UTC minute and east-positive longitude.
// Geometry is physical; the fixed display instant is explicitly a synthetic scenario clock.
function solar(date){const day=(Date.UTC(date.getUTCFullYear(),date.getUTCMonth(),date.getUTCDate())-Date.UTC(date.getUTCFullYear(),0,0))/86400000;
 const year=date.getUTCFullYear(),days=(year%4===0&&(year%100!==0||year%400===0))?366:365;
 const minutes=date.getUTCHours()*60+date.getUTCMinutes()+date.getUTCSeconds()/60;const gamma=2*Math.PI/days*(day-1+(minutes/60-12)/24);
 const eq=229.18*(.000075+.001868*Math.cos(gamma)-.032077*Math.sin(gamma)-.014615*Math.cos(2*gamma)-.040849*Math.sin(2*gamma));
 const dec=.006918-.399912*Math.cos(gamma)+.070257*Math.sin(gamma)-.006758*Math.cos(2*gamma)+.000907*Math.sin(2*gamma)-.002697*Math.cos(3*gamma)+.00148*Math.sin(3*gamma);
 const lon=(180-minutes/4-eq/4)*Math.PI/180;return[Math.cos(dec)*Math.cos(lon),Math.cos(dec)*Math.sin(lon),Math.sin(dec)];}
function nightImage(date,west,east){const c=document.createElement('canvas');c.width=512;c.height=512;const x=c.getContext('2d');const image=x.createImageData(c.width,c.height);const s=solar(date);
 for(let y=0;y<c.height;y++){const lat=Math.atan(Math.sinh(Math.PI*(1-2*y/(c.height-1))));for(let xx=0;xx<c.width;xx++){const lon=(west+(east-west)*xx/(c.width-1))*Math.PI/180;
 const dot=Math.cos(lat)*Math.cos(lon)*s[0]+Math.cos(lat)*Math.sin(lon)*s[1]+Math.sin(lat)*s[2];const elevation=Math.asin(Math.max(-1,Math.min(1,dot)))*180/Math.PI;
 const k=4*(y*c.width+xx);image.data[k]=0;image.data[k+1]=3;image.data[k+2]=8;image.data[k+3]=Math.round(145*Math.max(0,Math.min(1,-elevation/6)));}}
 x.putImageData(image,0,0);return c.toDataURL();}
await new Promise((resolve,reject)=>{map.on('load',resolve);map.on('error',e=>reject(e.error));});
map.addImage('selected-plane',icon(colors.yellow));map.addImage('simulated-plane',icon(colors.muted));
for(const [name,west,east] of [['night-west',-180,0],['night-east',0,180]]){
 map.addSource(name,{type:'image',url:nightImage(new Date('2026-09-24T12:00:00Z'),west,east),coordinates:[[west,85.05112878],[east,85.05112878],[east,-85.05112878],[west,-85.05112878]]});
 map.addLayer({id:name,type:'raster',source:name,layout:{visibility:'none'},paint:{'raster-opacity':.8,'raster-fade-duration':0}});
}
for(const name of ['airports','routes','aircraft'])map.addSource(name,{type:'geojson',data:collection([])});
map.addLayer({id:'route-line',type:'line',source:'routes',paint:{'line-color':colors.yellow,'line-width':1.6,'line-opacity':.75}});
map.addLayer({id:'airport-dot',type:'circle',source:'airports',paint:{'circle-radius':5,'circle-color':colors.yellow,'circle-stroke-color':colors.ocean,'circle-stroke-width':2}});
map.addLayer({id:'airport-label',type:'symbol',source:'airports',layout:{'text-field':['get','name'],'text-font':['Segoe UI'],'text-size':12,'text-offset':[0,1.5]},paint:{'text-color':colors.text,'text-halo-color':colors.ocean,'text-halo-width':2}});
map.addLayer({id:'aircraft-symbol',type:'symbol',source:'aircraft',layout:{'icon-image':['get','icon'],'icon-size':.65,'icon-rotate':['get','heading'],'icon-rotation-alignment':'map','icon-allow-overlap':true}});
map.on('render',()=>renderEvents++);
function pick(point){const f=map.queryRenderedFeatures(point,{layers:['aircraft-symbol','airport-dot','route-line']})[0];if(f)return setSelection(f.layer.id==='aircraft-symbol'?'Aircraft':f.layer.id==='airport-dot'?'Airport':'Route',String(f.properties.id));return null;}
map.on('click',e=>pick(e.point));
document.getElementById('select').onclick=()=>setSelection('Aircraft','synthetic-001');
document.getElementById('world').onclick=()=>{setFollow(false);map.setRenderWorldCopies(false);map.easeTo({center:[55,18],zoom:1.6,duration:600});};
document.getElementById('focus').onclick=()=>map.easeTo({center:current[0]?.geometry.coordinates??[151,-34],zoom:4,duration:600});
document.getElementById('follow').onclick=()=>setFollow(!follow);
map.on('dragstart',()=>setFollow(false));
document.getElementById('routes').onclick=e=>{routesVisible=!routesVisible;map.setLayoutProperty('route-line','visibility',routesVisible?'visible':'none');e.target.textContent='Routes: '+(routesVisible?'on':'off');};
document.getElementById('night').onclick=e=>{nightVisible=!nightVisible;for(const id of ['night-west','night-east'])map.setLayoutProperty(id,'visibility',nightVisible?'visible':'none');e.target.textContent='Day / night: '+(nightVisible?'on':'off');};
window.chrome.webview.addEventListener('message',event=>{
  if(event.data.type!=='snapshot'||event.data.snapshot.schemaVersion!==1)return;snapshot=event.data.snapshot;
  if(snapshot.aircraft.some(a=>a.source!=='Simulated'))throw new Error('This prototype accepts only synthetic aircraft');
  map.getSource('airports').setData(collection(snapshot.airports.map(a=>feature(a.id,[a.position.longitude,a.position.latitude],{name:a.icao}))));
  map.getSource('routes').setData(collection(snapshot.routes.map(r=>feature(r.id,r.segments.map(s=>s.map(p=>[p.longitude,p.latitude])),{kind:r.kind},'MultiLineString'))));
  resolveSnapshot();document.getElementById('status').textContent='Offline basemap ready · synthetic position updates';
});
function frame(now){const dt=now-previousFrame;previousFrame=now;if(benchmarkFrames)benchmarkFrames.push(dt);
 if(snapshot&&now-lastSet>=1000/30){lastSet=now;const time=(now-animationEpoch)/1000;const data=aircraft(time);current=data.features;map.getSource('aircraft').setData(data);renderedUpdates++;
  if(follow){const target=data.features.find(f=>f.properties.id===selected);if(target)map.jumpTo({center:target.geometry.coordinates});else setFollow(false);}}
 requestAnimationFrame(frame);
}
const animationEpoch=performance.now();requestAnimationFrame(frame);
const wait=ms=>new Promise(r=>setTimeout(r,ms));
async function idle(){await new Promise(resolve=>{if(map.loaded())resolve();else map.once('idle',resolve);});}
function glInfo(){const gl=map.getCanvas().getContext('webgl2');const debug=gl.getExtension('WEBGL_debug_renderer_info');return{vendor:debug?gl.getParameter(debug.UNMASKED_VENDOR_WEBGL):gl.getParameter(gl.VENDOR),renderer:debug?gl.getParameter(debug.UNMASKED_RENDERER_WEBGL):gl.getParameter(gl.RENDERER)};}
window.prototype={runDiagnostics:async(benchmark)=>{try{
 await snapshotReady;await wait(1000);const checks=[];const check=(condition,name)=>{if(!condition)throw new Error(name);checks.push(name);};
 check(snapshot.aircraft[0].source==='Simulated','Host snapshot uses typed synthetic source');
 check(map.getSource('airports')!==undefined&&snapshot.airports.length===2,'Two airports cross WPF bridge');
 check(snapshot.routes[0].segments[0].length>16,'Densified geographic route crosses WPF bridge');
 const original=map.getZoom();map.jumpTo({center:[151,-34],zoom:4});await wait(400);check(map.getZoom()>original,'Pan/zoom camera responds');
 const p=map.project(current[0].geometry.coordinates);check(map.queryRenderedFeatures(p,{layers:['aircraft-symbol']}).length>0,'GPU symbol is hit-testable');
 selected=null;check(pick(p)?.id==='synthetic-001'&&document.getElementById('selection').textContent.includes('synthetic-001'),'Shared click selection identifies stable entity without operational commands');
 const before=current[0].geometry.coordinates[0];await wait(600);check(current[0].geometry.coordinates[0]!==before,'Position updates dynamically');
 check(current[0].properties.heading===315,'Aircraft symbol consumes heading');
 document.getElementById('follow').click();await wait(100);check(follow&&map.getCenter().distanceTo(LngLat.convert(current[0].geometry.coordinates))<1000,'Follow centers the selected aircraft: '+JSON.stringify({follow,selected,center:map.getCenter(),target:current[0].geometry.coordinates,updates:renderedUpdates}));
 document.getElementById('world').click();check(!follow&&document.getElementById('follow').textContent==='Follow: off','World resets follow and its visible state');
 document.getElementById('routes').click();check(map.getLayoutProperty('route-line','visibility')==='none','Independent route visibility');document.getElementById('routes').click();
 const noon=solar(new Date('2026-03-20T12:00:00Z')),midnight=solar(new Date('2026-03-20T00:00:00Z'));
 check(noon[0]>.99&&midnight[0]<-.99,'UTC solar position changes day/night physically');
 map.jumpTo({center:[55,18],zoom:1.6});await wait(400);
 if(benchmark)for(const n of [1,25,100,500,1000,5000]){
  count=n;document.getElementById('status').textContent=`Benchmark · ${n.toLocaleString()} synthetic aircraft`;
  await wait(900);benchmarkFrames=[];benchmarkStart=performance.now();const startRender=renderEvents,startUpdates=renderedUpdates;send('phase',{count:n});
  // Scripted camera movement adds a repeatable interaction workload to aircraft updates.
  map.easeTo({center:[60,20],zoom:1.8,duration:6000,easing:t=>t});await wait(6000);
  const elapsed=performance.now()-benchmarkStart;const frames=benchmarkFrames;benchmarkFrames=null;const sorted=frames.slice().sort((a,b)=>a-b);
  const visible=map.queryRenderedFeatures({layers:['aircraft-symbol']});
  const visibleFeatureCount=new Set(visible.map(f=>f.properties.id)).size;
  check(visibleFeatureCount>=(n===1?1:n*.3),`Visible aircraft verified at ${n} entities: ${visibleFeatureCount}`);
  send('benchmark',{count:n,authoritativeSyntheticHz:2,targetPresentationHz:30,elapsedMs:elapsed,animationFrames:frames.length,meanFrameMs:frames.reduce((a,b)=>a+b,0)/frames.length,p95FrameMs:sorted[Math.floor(sorted.length*.95)],maxFrameMs:sorted.at(-1),framesOver50ms:frames.filter(f=>f>50).length,renderEvents:renderEvents-startRender,sourceUpdates:renderedUpdates-startUpdates,featureCount:current.length,visibleFeatureCount,viewport:[map.getCanvas().width,map.getCanvas().height],gpu:glInfo(),workload:'One batched GeoJSON symbol layer; global offline 110m land; no all-aircraft labels/weather/trails; scripted pan/zoom; full setData at ~30Hz'});
  map.jumpTo({center:[55,18],zoom:1.6});
 }
 count=1;map.jumpTo({center:[55,18],zoom:1.6});document.getElementById('status').textContent='Offline · 1 synthetic aircraft · 2 Hz inputs / 30 Hz presentation';
 document.getElementById('night').click();await wait(700);
 check(mapErrors.length===0,'No geographic/lighting resource errors: '+mapErrors.join('; '));
 check(map.isSourceLoaded('night-east')&&map.isSourceLoaded('night-west'),'Geographic day/night mask sources loaded');
 send('checks',{passed:true,checks,gpu:glInfo(),prototypeOnly:true});send('complete');
 }catch(error){send('error',{message:String(error.stack??error)});}}};
send('ready');
