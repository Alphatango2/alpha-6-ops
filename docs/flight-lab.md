# Alpha 6 Flight Lab

Alpha 6 Flight Lab is a local WPF companion used to exercise the real Alpha 6 OPS live-telemetry path without starting MSFS 2024. It sends versioned JSON frames over the local Windows named pipe `Alpha6OPS.FlightLab.v1`; data never leaves the PC.

## Basic use

1. Open **Alpha 6 Flight Lab** from the Start menu.
2. Leave the virtual aircraft at **At gate**.
3. Open Alpha 6 OPS, open the Flight Deck, and select **Connect to Flight Lab**.
4. Advance phases manually or select **Run quick flight**.
5. Review the live timeline, debrief, and JSON journal through the same observation path as a real connection.

The dashboard explicitly shows **LAB FLIGHT** and **FLIGHT LAB**. Lab v1 has no position, registration, or route evidence, so it records phases without linking a saved MSFS/SimBrief assignment or completing that assignment. The MSFS launch action is only used by the MSFS connection; connecting to Flight Lab never launches MSFS.

Manual controls cover gate, engine start, pushback, taxi, takeoff, climb, cruise, descent, approach, landing, taxi-in, shutdown, and go-around. The first automated scenario runs those states in sequence. Simulation UTC can advance at 1×, 10×, or 60× while the telemetry link continues to publish once per real second.

Fault controls freeze telemetry, drop the pipe connection, jump simulator UTC by ten minutes, change aircraft identity, take the virtual simulator link offline, and restart it. Frozen data is marked as last observed, and aircraft or clock discontinuities begin a fresh observed session. Diversion is recorded as a Flight Lab scenario event in the Alpha 6 OPS journal; route reassignment is not implemented because Lab protocol v1 does not carry position or destination.

Flight Lab exercises Alpha 6 OPS phase detection, reconnect behavior, timeline generation, journaling, and recovery. It does not emulate Microsoft's native SimConnect protocol or prove compatibility with a particular MSFS aircraft. The flight-history database currently records replay sessions; live sessions remain available in diagnostic journals and exports.
