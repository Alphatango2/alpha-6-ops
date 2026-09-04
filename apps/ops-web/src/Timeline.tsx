import { useEffect, useState } from 'react';

type Telemetry = { at: string; onGround: boolean; groundSpeedKnots: number; parkingBrake: boolean; enginesRunning: boolean };
type Snapshot = { index: number; sample: Telemetry; phase: string; eventsFiredCount: number };
type TimelineEvent = { phase: string; at: string };
type Timeline = { tenantId: string; aircraftId: string; phase: string; snapshots: Snapshot[]; events: TimelineEvent[] };
const utc = (at: string) => new Date(at).toISOString().slice(11, 19) + 'Z';

export function useFlightTimeline() {
  const [timeline, setTimeline] = useState<Timeline | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  async function load() {
    setBusy(true); setError('');
    try {
      const response = await fetch('/api/tenants/alpha6/replay/timeline');
      if (!response.ok) throw new Error(`API returned ${response.status}`);
      setTimeline(await response.json());
    } catch { setError('Operations API unavailable. Start the local API, then try again.'); }
    finally { setBusy(false); }
  }
  return { timeline, busy, error, load };
}

export function FlightTimelineScrubber({ timeline }: { timeline: Timeline }) {
  const [index, setIndex] = useState(0);
  useEffect(() => { setIndex(0); }, [timeline]);
  const snapshot = timeline.snapshots[index];
  const firedEvents = timeline.events.slice(0, snapshot.eventsFiredCount);
  return <div className="scrubber">
    <input type="range" min={0} max={timeline.snapshots.length - 1} value={index}
      onChange={e => setIndex(Number(e.target.value))} aria-label="Flight timeline position" />
    <div className="row">
      <div><p className="eyebrow">PHASE</p><h2>{snapshot.phase}</h2></div>
      <span className="tag">{utc(snapshot.sample.at)} · sample {index + 1} / {timeline.snapshots.length}</span>
    </div>
    <div className="table"><table><tbody>
      <tr><th>Ground speed</th><td>{snapshot.sample.groundSpeedKnots} kt</td></tr>
      <tr><th>On ground</th><td>{snapshot.sample.onGround ? 'Yes' : 'No'}</td></tr>
      <tr><th>Parking brake</th><td>{snapshot.sample.parkingBrake ? 'Set' : 'Released'}</td></tr>
      <tr><th>Engines</th><td>{snapshot.sample.enginesRunning ? 'Running' : 'Off'}</td></tr>
    </tbody></table></div>
    <p className="eyebrow">EVENTS FIRED SO FAR</p>
    {firedEvents.length === 0
      ? <p className="muted">None yet.</p>
      : <ul className="events">{firedEvents.map((e, i) => <li key={i}>{e.phase} · {utc(e.at)}</li>)}</ul>}
  </div>;
}
