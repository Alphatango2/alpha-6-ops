import test from 'node:test';
import assert from 'node:assert/strict';
import { interpolate, heading, canInterpolate, validateSnapshot, routeFeatures, normalizeLongitude, continuousCapture, labelDescriptors } from '../../src/Alpha6Ops.Desktop/MapV2/Web/map-math.mjs';

const point = (longitude, latitude) => ({ longitude, latitude });
const aircraft = (extra = {}) => ({ id:'a', position:point(0,0), source:'Observed', reliability:'Current', status:'Airborne', observedAt:'2026-09-24T00:00:00Z', sourceGeneration:1, continuityId:1, interpolationAllowed:true, headingDegrees:359, ...extra });
const snapshot = () => ({ schemaVersion:1, generation:1, sequence:1, capturedAt:'2026-09-24T00:00:01Z', sunVector:[1,0,0], aircraft:[aircraft()], airports:[], routes:[], weather:[], regions:[] });
test('spherical shortest dateline and heading paths', () => {
  assert.ok(Math.abs(Math.abs(interpolate(point(179,40),point(-179,40),0.5).longitude)-180)<0.001);
  assert.equal(heading(359,1,0.5),0);
  assert.equal(normalizeLongitude(540),-180);
});
test('continuity rejects gaps, sources, time reversal, teleports and stale samples', () => {
  const a=aircraft(), b=aircraft({observedAt:'2026-09-24T00:00:01Z',position:point(0.001,0)});
  assert.equal(canInterpolate(a,b),true);
  for (const extra of [{sourceGeneration:2},{continuityId:2},{source:'Replay'},{observedAt:a.observedAt},{observedAt:'2026-09-24T00:00:20Z'},{position:point(90,0)},{reliability:'Stale'},{interpolationAllowed:false}]) assert.equal(canInterpolate(a,{...b,...extra}),false);
});
test('strict wire validation rejects malformed, duplicate and obsolete snapshots', () => {
  assert.equal(validateSnapshot(snapshot(),1,0),true);
  for (const mutate of [s=>s.aircraft.push(s.aircraft[0]),s=>s.aircraft[0].source='fake',s=>s.aircraft[0].position.latitude=91,s=>s.sunVector=[2,0,0],s=>s.sequence=0,s=>s.generation=2,s=>s.weather=[{}]]) {
    const s=snapshot(); mutate(s); assert.throws(()=>validateSnapshot(s,1,0));
  }
  assert.throws(()=>validateSnapshot(snapshot(),1,1));
});
test('route segments remain separate; inferred semantics preserved', () => {
  const features=routeFeatures([{id:'r',kind:'InferredTrack',source:'Inferred',segments:[[point(0,0),point(1,0)],[point(4,0),point(5,0)]]}]);
  assert.equal(features.features.length,2);
  assert.equal(features.features[0].properties.kind,'InferredTrack');
  assert.deepEqual(features.features[1].geometry.coordinates,[[4,0],[5,0]]);
});
test('payload budgets, UTC and explicit route sources are enforced', () => {
  const s=snapshot();
  s.aircraft=Array.from({length:5001},(_,i)=>aircraft({id:`aircraft-${i}`}));
  assert.throws(()=>validateSnapshot(s,1,0));
  const invalidTime=snapshot(); invalidTime.capturedAt='2026-09-24T00:00:00-05:00';
  assert.throws(()=>validateSnapshot(invalidTime,1,0));
  const r={id:'r',kind:'ObservedTrack',source:'Inferred',reliability:'Current',segments:[[point(0,0),point(1,0)]],waypoints:[]};
  const route=snapshot(); route.routes=[r]; assert.throws(()=>validateSnapshot(route,1,0));
  r.source='Observed'; r.segments=[[point(179,0),point(-179,0)]]; assert.throws(()=>validateSnapshot(route,1,0));
  r.segments=[[point(179,0),point(180,0)],[point(-180,0),point(-179,0)]];
  assert.equal(validateSnapshot(route,1,0),true);
});
test('simulator dates do not affect capture continuity or current movement', () => {
  for(const year of [2000,2040]) {
    const a=aircraft({observedAt:`${year}-01-01T00:00:00Z`});
    const b=aircraft({observedAt:`${year}-01-01T00:00:01Z`,position:point(0.001,0)});
    assert.equal(canInterpolate(a,b),true);
    assert.equal(continuousCapture('2026-09-24T00:00:00Z','2026-09-24T00:00:01Z',1000),true);
  }
  assert.equal(continuousCapture('2026-09-24T00:00:00Z','2026-09-24T00:00:01Z',16000),false);
  assert.equal(continuousCapture('2026-09-24T00:00:00Z','2026-09-24T00:00:20Z',1000),false);
  assert.equal(continuousCapture('2026-09-24T00:00:01Z','2026-09-24T00:00:00Z',1000),false);
});
test('label image descriptors ignore movement and track timestamps, with bounded aircraft labels', () => {
  const s=snapshot(); s.aircraft=Array.from({length:100},(_,i)=>aircraft({id:`a${i}`}));
  const before=labelDescriptors(s);
  assert.equal(before.length,25);
  s.aircraft[0].position=point(1,1);
  s.aircraft[0].observedAt='2040-01-01T00:00:00Z';
  s.routes=[{waypoints:[],observedAt:'2040-01-01T00:00:00Z',segments:[[point(1,1),point(2,2)]]}];
  assert.deepEqual(labelDescriptors(s),before);
  s.airports=Array.from({length:40},(_,i)=>({id:`airport-${i}`,position:point(i,0)}));
  assert.equal(labelDescriptors(s).length,64);
});
