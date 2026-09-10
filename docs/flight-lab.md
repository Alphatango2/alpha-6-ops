# Alpha 6 Flight Lab

Alpha 6 Flight Lab is a local WPF companion used to exercise the real Alpha 6 OPS live-telemetry path without starting MSFS 2024. It sends versioned JSON frames over the local Windows named pipe `Alpha6OPS.FlightLab.v3`; data never leaves the PC.

## Basic use

1. Open **Alpha 6 Flight Lab** from the Start menu.
2. Leave the virtual aircraft at **At gate**.
3. Open Alpha 6 OPS, open the Flight Deck, and select **Connect to Flight Lab**.
4. Advance phases manually or select **Run quick flight**.
5. Review the live timeline, debrief, flight history, and JSON journal exactly as with a real connection.

Manual controls cover gate, engine start, pushback, taxi, takeoff, climb, cruise, descent, approach, landing, taxi-in, shutdown, and go-around. The first automated scenario runs those states in sequence. Simulation UTC can advance at 1×, 10×, or 60× while the telemetry link continues to publish once per real second.

Flight Lab v3 publishes normalized route progress plus altitude, indicated airspeed, vertical speed, landing-gear position, and altitude above ground. Its manual phases and automatic flight move the live aircraft marker along the SimBrief route and exercise the expanded operational-event detector through the same boundary used by native SimConnect. Fault controls freeze telemetry, drop the pipe connection, jump simulator UTC by ten minutes, change aircraft identity, take the virtual simulator link offline, and restart it. Diversion is recorded as a Flight Lab scenario event; route reassignment remains future work.

Flight Lab validates Alpha 6 OPS phase detection, reconnect behavior, timeline generation, journaling, history, and recovery. It does not emulate Microsoft's native SimConnect protocol or prove compatibility with a particular MSFS aircraft.
