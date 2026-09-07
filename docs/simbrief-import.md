# SimBrief import — 0.12

The active-flight assignment window can retrieve the latest generated SimBrief OFP using a Navigraph Alias/SimBrief username. It calls the official JSON form of the latest-OFP endpoint documented at https://developers.navigraph.com/docs/simbrief/fetching-ofp-data. OPS does not request or store a Navigraph password.

The importer maps airline and flight number, origin, destination, aircraft type, registration, scheduled-out, estimated-in, route, initial cruise altitude, ramp fuel, fuel units and briefing generation time. The operational tracker uses scheduled-out and estimated-in as its initial timing. Once actual block-out is observed, its existing delay-adjusted ETA rule applies.

The username is stored under `%LOCALAPPDATA%/Alpha6Designs/Alpha6OPS/SimBrief/username.txt`. The latest successful raw OFP is cached as `latest-ofp.json`; if SimBrief cannot be reached, OPS may reuse that cache only when the requested username matches the saved username. An import older than 24 hours is visibly flagged so the pilot can generate a current briefing before saving it.

SimBrief returns the latest generated plan. Changing a plan on the SimBrief website requires generating it again and clicking **Import** again in OPS. Internet access is required for a fresh import; live SimConnect tracking remains local.

## Gate assignment in 0.12.0

The importer adds departure and arrival gates to the active-flight record and dashboard. Resolution order:

1. Parse explicit gate entries from SimBrief custom/dispatch remarks, including clear forms such as `DEP GATE 52`, `DEPARTURE GATE A12`, `ARR GATE B7`, and `ARRIVAL GATE B7`.
2. If either gate is absent, look up candidate gates by airport ICAO and airline ICAO in the initial maintained Alpha 6 OPS gate catalog.
3. Choose deterministically from equally suitable candidates using the flight number and scheduled date, so importing the same OFP again produces the same suggestion.
4. Leave the gate unassigned when no reliable candidate exists. A guessed gate must never be presented as confirmed operational information.

Each result records whether it came from `SimBrief dispatch notes`, an `Airline gate catalog suggestion`, or a `Pilot entry`, along with its confidence. The dashboard exposes that source as the gate tooltip, and the active-flight editor allows correction before connecting to the simulator.

The gate catalog is deliberately conservative and currently covers selected stations for DAL, JBU, AAL, UAL, and SWA. Airports without a maintained match remain unassigned rather than receiving a fabricated gate.
