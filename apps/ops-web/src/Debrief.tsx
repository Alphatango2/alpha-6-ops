import { useEffect, useState } from 'react';

type TimelineEvent = { phase: string; at: string };
type Segment = { phase: string; startedAt: string; endedAt: string };
type Leg = { id: string; origin: string; destination: string; departureDelayMinutes: number; arrivalDelayMinutes: number; completed: boolean };
type Debrief = { tenantId: string; aircraftId: string; phase: string; events: TimelineEvent[]; segments: Segment[]; legs: Leg[] };
const utc = (at: string) => new Date(at).toISOString().slice(11, 19) + 'Z';
const minutes = (start: string, end: string) => Math.round((new Date(end).getTime() - new Date(start).getTime()) / 60000);

export function useFlightDebrief(fixture: string) {
  const [debrief, setDebrief] = useState<Debrief | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  useEffect(() => { setDebrief(null); setError(''); }, [fixture]);
  async function load() {
    setBusy(true); setError('');
    try {
      const response = await fetch(`/api/tenants/alpha6/replay/debrief?fixture=${encodeURIComponent(fixture)}`);
      if (!response.ok) throw new Error(`API returned ${response.status}`);
      setDebrief(await response.json());
    } catch { setError('Operations API unavailable. Start the local API, then try again.'); }
    finally { setBusy(false); }
  }
  return { debrief, busy, error, load };
}

export function FlightDebriefSummary({ debrief }: { debrief: Debrief }) {
  const leg = debrief.legs[0];
  const blockMinutes = debrief.segments.length > 0
    ? minutes(debrief.segments[0].startedAt, debrief.segments[debrief.segments.length - 1].endedAt)
    : null;
  return <div className="debrief">
    <div className="row">
      <div><p className="eyebrow">RESULT</p><h2>{debrief.phase}</h2></div>
      {leg && <span className="tag">{leg.departureDelayMinutes ? `+${leg.departureDelayMinutes} min out` : 'On-time out'} · {leg.arrivalDelayMinutes ? `+${leg.arrivalDelayMinutes} min in` : 'On-time in'}</span>}
    </div>
    {blockMinutes !== null && <p className="muted">Block time (taxi-out to block-in): {blockMinutes} min</p>}
    <div className="table"><table>
      <thead><tr><th>Phase</th><th>Started</th><th>Ended</th><th>Duration</th></tr></thead>
      <tbody>{debrief.segments.map((s, i) => <tr key={i}><td>{s.phase}</td><td>{utc(s.startedAt)}</td><td>{utc(s.endedAt)}</td><td>{minutes(s.startedAt, s.endedAt)} min</td></tr>)}</tbody>
    </table></div>
    <p className="eyebrow">MILESTONES</p>
    <ul className="events">{debrief.events.map((e, i) => <li key={i}>{e.phase} · {utc(e.at)}</li>)}</ul>
  </div>;
}
