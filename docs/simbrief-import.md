# SimBrief import — 0.10

The active-flight assignment window can retrieve the latest generated SimBrief OFP using a Navigraph Alias/SimBrief username. It calls the official JSON form of the latest-OFP endpoint documented at https://developers.navigraph.com/docs/simbrief/fetching-ofp-data. OPS does not request or store a Navigraph password.

The importer maps airline and flight number, origin, destination, aircraft type, registration, scheduled-out, estimated-in, route, initial cruise altitude, ramp fuel, fuel units and briefing generation time. The operational tracker uses scheduled-out and estimated-in as its initial timing. Once actual block-out is observed, its existing delay-adjusted ETA rule applies.

The username is stored under `%LOCALAPPDATA%/Alpha6Designs/Alpha6OPS/SimBrief/username.txt`. The latest successful raw OFP is cached as `latest-ofp.json`; if SimBrief cannot be reached, OPS may reuse that cache only when the requested username matches the saved username. An import older than 24 hours is visibly flagged so the pilot can generate a current briefing before saving it.

SimBrief returns the latest generated plan. Changing a plan on the SimBrief website requires generating it again and clicking **Import** again in OPS. Internet access is required for a fresh import; live SimConnect tracking remains local.
