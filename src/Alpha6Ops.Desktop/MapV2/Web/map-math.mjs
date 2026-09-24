// Presentation-only calculations. Nothing in this module writes operational state.
const sources = ['Unknown','Observed','Inferred','Simulated','Replay'];
const reliabilities = ['Unknown','Current','Stale','Uncertain'];
const kinds = ['Planned','CompletedPlan','ObservedTrack','InferredTrack','Diversion'];
const statuses = ['Unknown','AtGate','Taxi','Airborne','Complete'];
const severities = ['Unknown','Normal','Information','Warning','Critical'];
const require = (condition) => { if (!condition) throw new Error('Invalid map payload'); };
const finite = Number.isFinite;
const integer = value => Number.isSafeInteger(value) && value >= 0;
const text = value => value == null || typeof value === 'string' && value.length <= 512;
const timestamp = value => typeof value === 'string' && /(?:Z|\+00:00)$/.test(value) && finite(Date.parse(value)) && Date.parse(value) >= Date.UTC(1900,0,1);
const point = p => p && finite(p.longitude) && finite(p.latitude) && Math.abs(p.longitude)<=180 && Math.abs(p.latitude)<=90;
function ids(items) {
  const seen = new Set();
  for (const item of items) {
    require(item && typeof item.id === 'string' && item.id.trim().length>0 && item.id.length<=160 && !/[\x00-\x1f\x7f]/.test(item.id) && !seen.has(item.id));
    seen.add(item.id);
  }
}
export function validateSnapshot(s, epoch, lastSequence) {
  require(s && s.schemaVersion===1 && s.generation===epoch && integer(s.sequence) && s.sequence>lastSequence && timestamp(s.capturedAt));
  for (const [key,max] of [['aircraft',5000],['airports',2048],['routes',2048],['weather',0],['regions',0]]) {
    require(Array.isArray(s[key]) && s[key].length<=max); ids(s[key]);
  }
  require(Array.isArray(s.sunVector) && s.sunVector.length===3 && s.sunVector.every(finite) && Math.abs(s.sunVector.reduce((a,b)=>a+b*b,0)-1)<0.00001);
  for (const a of s.aircraft) {
    require(point(a.position) && sources.includes(a.source) && reliabilities.includes(a.reliability) && statuses.includes(a.status));
    require(timestamp(a.observedAt) && integer(a.sourceGeneration) && integer(a.continuityId) && typeof a.interpolationAllowed==='boolean');
    require(text(a.callsign) && text(a.registration));
    for (const n of [a.altitudeFeet,a.headingDegrees,a.groundspeedKnots]) require(n==null || finite(n));
    require(a.headingDegrees==null || a.headingDegrees>=0 && a.headingDegrees<360);
    require(a.groundspeedKnots==null || a.groundspeedKnots>=0);
  }
  for (const a of s.airports) {
    require(point(a.position) && severities.includes(a.operationalStatus));
    require([a.name,a.icao,a.iata,a.role,a.weatherReference].every(text) && (a.statusAt==null || timestamp(a.statusAt)));
    require(a.delayMinutes==null || finite(a.delayMinutes));
    require(a.capacityFraction==null || finite(a.capacityFraction) && a.capacityFraction>=0 && a.capacityFraction<=1);
  }
  let count=0;
  for (const r of s.routes) {
    require(kinds.includes(r.kind) && sources.includes(r.source) && reliabilities.includes(r.reliability));
    require(r.kind!=='ObservedTrack' || ['Observed','Replay','Simulated'].includes(r.source));
    require(r.kind!=='InferredTrack' || r.source==='Inferred');
    require([r.originId,r.destinationId,r.rotationId].every(text) && (r.observedAt==null || timestamp(r.observedAt)));
    require(Array.isArray(r.segments) && Array.isArray(r.waypoints) && r.waypoints.length<=2048);
    ids(r.waypoints);
    for (const w of r.waypoints) { require(point(w.position) && text(w.label)); count++; }
    for (const segment of r.segments) {
      require(Array.isArray(segment) && segment.length>=2);
      count+=segment.length;
      for (let i=0;i<segment.length;i++) require(point(segment[i]) && (!i || Math.abs(segment[i].longitude-segment[i-1].longitude)<=180));
    }
    require(count<=100000);
  }
  return true;
}
export const normalizeLongitude = n => ((n+180)%360+360)%360-180;
export const heading = (a,b,t) => (a+(((b-a+540)%360)-180)*t+360)%360;
const rad = Math.PI/180;
const vector = p => [Math.cos(p.latitude*rad)*Math.cos(p.longitude*rad),Math.cos(p.latitude*rad)*Math.sin(p.longitude*rad),Math.sin(p.latitude*rad)];
export function angle(a,b) {
  const x=vector(a), y=vector(b);
  return Math.acos(Math.max(-1,Math.min(1,x.reduce((sum,v,i)=>sum+v*y[i],0))));
}
export function interpolate(a,b,t) {
  const x=vector(a), y=vector(b), theta=angle(a,b);
  if (theta<1e-8 || Math.PI-theta<1e-6) return t<1 ? a : b;
  const v=x.map((n,i)=>(n*Math.sin((1-t)*theta)+y[i]*Math.sin(t*theta))/Math.sin(theta));
  return {longitude:normalizeLongitude(Math.atan2(v[1],v[0])/rad),latitude:Math.atan2(v[2],Math.hypot(v[0],v[1]))/rad};
}
export function canInterpolate(a,b) {
  if (!a || !b || a.id!==b.id || a.source!==b.source || a.sourceGeneration!==b.sourceGeneration || a.continuityId!==b.continuityId || !a.interpolationAllowed || !b.interpolationAllowed || a.reliability!=='Current' || b.reliability!=='Current') return false;
  const seconds=(Date.parse(b.observedAt)-Date.parse(a.observedAt))/1000;
  return seconds>0 && seconds<=15 && angle(a.position,b.position)*3440.065<=2+seconds/3600*1500;
}
// Capture/arrival time and simulator observation time are separate clock domains.
export function continuousCapture(previousCapture, nextCapture, arrivalGap) {
  if (previousCapture==null) return false;
  const elapsed=Date.parse(nextCapture)-Date.parse(previousCapture);
  return elapsed>=0 && elapsed<=15000 && arrivalGap>=0 && arrivalGap<=15000;
}
export function labelDescriptors(s) {
  const result=s.airports.slice(0,40).map(a=>({position:a.position,text:`${a.icao||a.iata||a.id}${a.role?` · ${a.role}`:''}`,color:'#bbc2c8'}));
  const seen=new Set(s.airports.map(a=>`${a.position.longitude}:${a.position.latitude}`));
  for(const r of s.routes) for(const w of r.waypoints) {
    const key=`${w.position.longitude}:${w.position.latitude}`;
    if(result.length>=64 || seen.has(key) || !w.label) continue;
    seen.add(key); result.push({position:w.position,text:w.label,color:'#8e99a2'});
  }
  const aircraft=[...s.aircraft.filter(a=>a.highlighted),...s.aircraft.filter(a=>!a.highlighted)].slice(0,25);
  for(const a of aircraft) {
    if(result.length>=64) break;
    result.push({aircraftId:a.id,text:`${a.callsign||a.registration||a.id} · ${a.source}`,color:'#bbc2c8'});
  }
  return result;
}
export const collection = features => ({type:'FeatureCollection',features});
export function routeFeatures(routes) {
  return collection(routes.flatMap(r=>r.segments.map(segment=>({type:'Feature',properties:{id:r.id,kind:r.kind,source:r.source},geometry:{type:'LineString',coordinates:segment.map(p=>[p.longitude,p.latitude])}}))));
}
