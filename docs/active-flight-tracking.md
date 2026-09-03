# Active flight tracking — 0.9

Use **Set active flight** before connecting to enter the flight number, optional registration, origin and destination ICAO codes, and planned departure and arrival in simulator UTC. The assignment is saved locally in `%LOCALAPPDATA%/Alpha6Designs/Alpha6OPS/active-flight.json` and included in the flight journal when a connection starts.

The tracking bar displays the SimConnect aircraft title alongside the assigned registration, route, planned block duration, actual block-out, estimated arrival, elapsed block time, current phase and progress. Before block-out, estimated arrival is the planned arrival. After block-out, OPS preserves the planned block duration and shifts the estimate by the actual departure delay. Block-in replaces the estimate with the actual completion time.

Progress uses confirmed operational phases: gate 0%, taxi-out 8%, airborne 10–92% according to elapsed time against the estimated arrival, taxi-in 95%, and block-in 100%. If OPS attaches after departure, it shows the directly observed Taxiing or Airborne condition, but leaves an unobserved departure time blank. It does not invent a departure timestamp.

The Fenix aircraft title and basic state come from SimConnect. Airline schedule data is entered by the user because third-party aircraft may not publish their operational flight plan through the standard simulator variables.
