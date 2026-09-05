# SimConnect test build 0.3

The installed MSFS 2024 SDK was found at M:/, version 1.7.3. Its native SimConnect.dll is used through a read-only P/Invoke adapter. The older C:/MSFS SDK is not used. This avoids depending on the managed wrapper's target framework. Native API signatures, enum values and packed receive offsets were checked against the installed SimConnect.h.

One background worker owns the connection and drains GetNextDispatch, including while the WPF window is hidden. Open forces a local connection. The green badge is set only on the received OPEN acknowledgement. Failure is red; connecting/disconnecting amber; disconnected gray. Text accompanies each color. Missing telemetry times out after 30 seconds. Reconnection is explicit, not automatic, and begins a new monitor session.

Requested once per simulator second: ABSOLUTE TIME, SIM ON GROUND, GROUND VELOCITY in knots, BRAKE PARKING POSITION, GENERAL ENG COMBUSTION indices 1–4, IS SLEW ACTIVE, and TITLE. Pause_EX1 suppresses phase tracking during all reported pause modes; tracking stays suppressed until the initial pause state arrives. Simulator time is interpreted as seconds since 0001-01-01 UTC as documented. No controls, aircraft state, files inside MSFS, or server configuration are written.

Live monitoring arms only when stationary on ground with parking brake set, unpaused and not slewing. The Core phase detector produces live milestones. Aircraft-title changes or clock reversal invalidate the session and require reconnecting at the gate. On arming, the active flight assignment becomes a single-leg `AircraftRotation`; each confirmed milestone updates its actuals through `RotationPlanner.ApplyMilestone`/`Project`, the same path the fixture-replay session uses, so a live departure or arrival changes the tracker's delay/ETA the same way a replayed one does. The demo rotation used by the replay button remains separate: airport/aircraft assignment matching against a real schedule and live schedule anchoring beyond the one manually-entered leg are not implemented. Tracking all engines beyond four, aircraft-specific parking procedures, clock jumps forward and reconnect recovery need additional work before operational use.

The application loads the full native DLL path from ALPHA6_SIMCONNECT_DLL or simconnect-sdk-path.txt beside the executable. This development package's configuration points to M:/SimConnect SDK/lib/SimConnect.dll. No SDK binary is redistributed. Release packaging must review the installed SDK licensing and native dependencies before including approved redistributables. No full SDK should be required in the final pilot product.

Validation: build passed; the installed DLL loads and SimConnect_Open executes. MSFS was not running, so the probe returned 0x80004005 and zero samples. This validates the native loading/error path only; it does not prove a successful handshake, correct live variable delivery, or a complete flight. The offline WPF regression path is also retained. Next manual gate: load a flight in MSFS 2024, connect, verify green badge and plausible telemetry, then perform a short flight.

Official references:
- https://docs.flightsimulator.com/msfs2024/retail/programming-apis/simconnect/api-reference/general/simconnect_open/
- https://docs.flightsimulator.com/msfs2024/flighting/programming-apis/simconnect/api-reference/general/simconnect_subscribetosystemevent/
- https://docs.flightsimulator.com/msfs2024/html/6_Programming_APIs/Environment_Variables.htm
