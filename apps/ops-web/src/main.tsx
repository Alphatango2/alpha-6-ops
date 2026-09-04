import React, { useEffect, useState } from 'react';
import { createRoot } from 'react-dom/client';
import { useFlightTimeline, FlightTimelineScrubber } from './Timeline';
import { useFlightDebrief, FlightDebriefSummary } from './Debrief';
import './style.css';

type Leg = { id: string; origin: string; destination: string; estimatedOut: string; estimatedIn: string; departureDelayMinutes: number; arrivalDelayMinutes: number; completed: boolean };
type Rotation = { aircraftId: string; legs: Leg[]; phase?: string };
const utc = (date: string) => new Date(date).toISOString().slice(11, 16) + 'Z';
const fixtureLabel = (name: string) => name.replace(/\.jsonl$/, '').replace(/-/g, ' ').replace(/^./, c => c.toUpperCase());
function App() {
  const [view, setView] = useState('Simple');
  const [data, setData] = useState<Rotation | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [fixtures, setFixtures] = useState(['delayed-flight.jsonl']);
  const [fixture, setFixture] = useState('delayed-flight.jsonl');
  async function load(replay: boolean) {
    setBusy(true); setError('');
    try {
      const response = await fetch('/api/tenants/alpha6/' + (replay ? `replay?fixture=${encodeURIComponent(fixture)}` : 'rotation'));
      if (!response.ok) throw new Error(`API returned ${response.status}`);
      setData(await response.json());
    } catch { setError('Operations API unavailable. Start the local API, then try again.'); }
    finally { setBusy(false); }
  }
  useEffect(() => { void load(false); }, []);
  useEffect(() => {
    fetch('/api/tenants/alpha6/replay/fixtures').then(r => r.ok ? r.json() : null)
      .then(d => { if (d?.fixtures?.length) setFixtures(d.fixtures); }).catch(() => {});
  }, []);
  const next = data?.legs.find(leg => !leg.completed);
  const timeline = useFlightTimeline(fixture);
  const debrief = useFlightDebrief(fixture);
  return <main>
    <header><div className="brand">ALPHA 6 <strong>OPS</strong><small>ALPHA 6 DESIGNS</small></div><span className="tag">LOCAL DEMO · 02 SEP 2026</span></header>
    <section className="intro"><p className="eyebrow">YOUR AIRLINE, IN MOTION</p><h1>You fly the airplane.<br/><span>We'll run the airline.</span></h1><p>One aircraft. One connected day. Every minute matters.</p></section>
    <nav aria-label="Operations view">{['Simple', 'Advanced', 'OCC'].map(v => <button key={v} aria-pressed={view === v} onClick={() => setView(v)}>{v}</button>)}</nav>
    <section className="panel"><div className="row"><div><p className="eyebrow">ASSIGNED AIRCRAFT</p><h2>{data?.aircraftId ?? 'N600A6'}</h2></div><span className="tag">{data?.phase === 'Complete' ? 'REPLAY COMPLETE' : 'PLANNED ROTATION'}</span></div>
      <div className="next"><span>NEXT DEPARTURE</span><h2>{next ? `${next.origin} → ${next.destination}` : 'Loading rotation…'}</h2><p>{next ? `${next.id} · ${utc(next.estimatedOut)} · ${next.departureDelayMinutes ? '+' + next.departureDelayMinutes + ' min' : 'On time'}` : 'Connect to the local operations API.'}</p></div>
      <div className="actions">
        <select aria-label="Replay fixture" value={fixture} disabled={busy} onChange={e => setFixture(e.target.value)}>
          {fixtures.map(f => <option key={f} value={f}>{fixtureLabel(f)}</option>)}
        </select>
        <button className="primary" disabled={busy} onClick={() => void load(true)}>{busy ? 'Loading…' : `Run ${fixtureLabel(fixture)} replay`}</button>
        <button disabled={busy} onClick={() => void load(false)}>Reset preview</button>
      </div>
      <p className="muted">Simulator-independent preview. No live simulator connection or saved changes.</p>
      {error && <p role="alert">{error}</p>}
    </section>
    {view !== 'Simple' && <section className="panel"><h2>{view === 'OCC' ? 'Aircraft rotation board' : 'Today’s rotation'}</h2><div className="table"><table><thead><tr><th>Flight</th><th>Route</th><th>Out UTC</th><th>In UTC</th><th>Dep / arr delay</th><th>Status</th></tr></thead><tbody>{data?.legs.map(l => <tr key={l.id}><td>{l.id}</td><td>{l.origin} → {l.destination}</td><td>{utc(l.estimatedOut)}</td><td>{utc(l.estimatedIn)}</td><td>{l.departureDelayMinutes} / {l.arrivalDelayMinutes} min</td><td>{l.completed ? 'Actual' : 'Projected'}</td></tr>)}</tbody></table></div><p className="muted">35-minute minimum turn · planned block duration · no departures before schedule</p></section>}
    {view === 'OCC' && <aside>OCC foundation: aircraft timing only. Crew legality, passenger connections, maintenance and disruption actions are planned.</aside>}
    <section className="panel"><div className="row"><div><p className="eyebrow">FLIGHT REPLAY</p><h2>Timeline scrubber</h2></div>
      {!timeline.timeline && <button className="primary" disabled={timeline.busy} onClick={() => void timeline.load()}>{timeline.busy ? 'Loading…' : `Load ${fixtureLabel(fixture)} timeline`}</button>}</div>
      {timeline.error && <p role="alert">{timeline.error}</p>}
      {timeline.timeline && <FlightTimelineScrubber timeline={timeline.timeline} />}
    </section>
    <section className="panel"><div className="row"><div><p className="eyebrow">POST-FLIGHT</p><h2>Debrief</h2></div>
      {!debrief.debrief && <button className="primary" disabled={debrief.busy} onClick={() => void debrief.load()}>{debrief.busy ? 'Loading…' : `Load ${fixtureLabel(fixture)} debrief`}</button>}</div>
      {debrief.error && <p role="alert">{debrief.error}</p>}
      {debrief.debrief && <FlightDebriefSummary debrief={debrief.debrief} />}
    </section>
    <footer>ALPHA 6 OPS / FOUNDATION 0.1 · ALL TIMES UTC</footer>
  </main>;
}
createRoot(document.getElementById('root')!).render(<React.StrictMode><App /></React.StrictMode>);
