# Honest SimConnect boundary

There is no functioning SimConnect adapter in this foundation and no proprietary DLL included. The console `--simconnect` option deliberately exits with an explanatory error. `ISimulatorTelemetry` is the shared boundary and `JsonReplay` is its only implemented provider.

Use the MSFS 2024 SDK installed through the simulator developer tools. Follow the SDK's managed-client documentation and samples for the exact SDK release; do not assume an old FSX/MSFS 2020 DLL or arbitrary NuGet wrapper supports the required 2024 behavior.

Official references:
- [SimConnect SDK overview](https://docs.flightsimulator.com/msfs2024/retail/programming-apis/simconnect/simconnect-sdk/)
- [Managed-code guidance](https://docs.flightsimulator.com/msfs2024/flighting/programming-apis/simconnect/programming-simconnect-clients-using-managed-code/)
- [API reference](https://docs.flightsimulator.com/msfs2024/html/6_Programming_APIs/SimConnect/SimConnect_API_Reference.htm)

The managed assembly is supplied under the SDK's `SimConnect SDK/lib/managed` folder. Native/managed assembly deployment and supported target framework must be verified against the installed SDK. A .NET 10 domain library does not prove direct managed wrapper compatibility. First build/run the official sample on Windows x64, then decide whether to load the adapter directly or bridge a compatible Windows process over named pipes. Document required native runtime dependencies and redistribution rights before packaging.

Implementation checklist: own a message pump, register receive callbacks and data definitions, normalize on-ground / ground velocity in knots / parking-brake / engine-running / pause / slew signals, obtain a consistent simulator UTC timestamp, subscribe near 1 Hz, handle simulator quit/disconnect and dispose connections. Consult the installed simulation-variable reference for exact names, units and per-aircraft behavior. Do not substitute host wall time for simulator time during accelerated simulation.

Real-world validation matrix: cold-and-dark departure, pushback without engines, taxi hold, touchdown bounce, go-around, parked with engine/APU running, pause, slew, clock reversal, accelerated time, simulator restart and late attachment. Reconnect must not fabricate airborne or block events. The current detector cannot restore an in-progress flight and treats block-in conservatively as engines off. Add explicit airport/aircraft matching and pilot reconciliation before trusting actuals in live operations.
