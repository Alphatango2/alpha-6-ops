# SimBrief import — 0.10

The active-flight assignment window can retrieve the latest generated SimBrief OFP using a Navigraph Alias/SimBrief username. It calls the official JSON form of the latest-OFP endpoint documented at https://developers.navigraph.com/docs/simbrief/fetching-ofp-data. OPS does not request or store a Navigraph password.

The importer maps airline and flight number, origin, destination, aircraft type, registration, scheduled-out, estimated-in, route, initial cruise altitude, ramp fuel, fuel units and briefing generation time. The operational tracker uses scheduled-out and estimated-in as its initial timing. Once actual block-out is observed, its existing delay-adjusted ETA rule applies.

The username is stored under `%LOCALAPPDATA%/Alpha6Designs/Alpha6OPS/SimBrief/username.txt`. The latest successful raw OFP is cached as `latest-ofp.json`; if SimBrief cannot be reached, OPS may reuse that cache only when the requested username matches the saved username. An import older than 24 hours is visibly flagged so the pilot can generate a current briefing before saving it.

SimBrief returns the latest generated plan. Changing a plan on the SimBrief website requires generating it again and clicking **Import** again in OPS. Internet access is required for a fresh import; live SimConnect tracking remains local.

## Planned gate assignment for 0.12.0

The importer will add departure and arrival gates to the active-flight record and dashboard. Resolution order:

1. Parse explicit gate entries from SimBrief custom/dispatch remarks, including clear forms such as `DEP GATE 52`, `DEPARTURE GATE A12`, `ARR GATE B7`, and `ARRIVAL GATE B7`.
2. If either gate is absent, look up candidate gates by airport ICAO and airline ICAO in a maintained Alpha 6 OPS gate catalog. Terminal, domestic/international use, aircraft size, and stand restrictions narrow the candidates when those attributes are available.
3. Choose deterministically from equally suitable candidates using the flight number and scheduled date, so importing the same OFP again produces the same suggestion.
4. Leave the gate unassigned when no reliable candidate exists. A guessed gate must never be presented as confirmed operational information.

Each result records whether it came from `SimBrief remarks`, the `OPS gate catalog`, or a `pilot edit`. The interface labels catalog results as suggested, shows the source, and allows correction before connecting to the simulator. A pilot correction has priority over later automatic refreshes unless the OFP contains a new explicit gate.

The official SimBrief interface supports custom remarks through the `manualrmk` dispatch option, but the current OPS importer does not yet retain remarks or gate fields. The 0.12.0 work therefore includes extending the cached OFP parser and the `ActiveFlightPlan` persistence model before gate values are shown on the dashboard.
