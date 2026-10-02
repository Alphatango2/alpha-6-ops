# 7.25A — Native airframe substitution review

Decision: the PMDG 777-200ER may substitute for the live iniBuilds A350 flight
when its recorded transition reproduces the required shallow-climb telemetry
profile.

The corrected observed-cruise tracker does not inspect aircraft title, family,
engine type or add-on provider. It consumes simulator UTC, airborne state,
pressure/indicated altitude, vertical speed, pause and slew state. The preserved
DAL40 journal supplies 54,061 native iniBuilds A350 samples through those same
standard SimConnect fields. Its exact production replay proves the corrected
FL350→FL370→FL390→FL410 sequence and TOD from FL410; 19 native-style profile
checks prove qualification, invalidation, recovery and provenance boundaries.

The remaining native uncertainty is therefore the live connection and
presentation integration around that aircraft-independent contract: fresh
sample timing, intent/event ordering, state persistence, more than 2,048 source
observations, hidden-workspace accumulation and renderer recreation.

A qualifying live transition must contain all of the following:

- an already established observed cruise;
- at least 30 valid observed seconds at or above +500 ft/min while at least 100
  feet above and departing that level;
- valid vertical-speed samples below +500 ft/min before reaching +750 feet
  displacement;
- eventual displacement of at least +750 feet;
- at least 60 valid observed seconds at the stable new cruise level; and
- exactly one sequential observed-cruise commitment with unchanged filed cruise.

A PMDG transition that stays at or above +500 ft/min until after +750 feet does
not reproduce the fixed defect and does not close the step-climb gate. The same
flight may still pass the flown-track and generic compatibility portions.

KLAX→PHNL is suitable because its normal duration comfortably exceeds 2,048
one-second observations and the 40-minute continuous-telemetry floor, while
allowing a six-minute hidden-workspace interval and classic→Map V2 renderer
recreation. A usable step-climb window must still be planned before descent.

No PMDG-specific code or shutdown adapter is authorized. The PMDG 777 uses the
default ground profile and limited terminal-shutdown support, with guarded manual
Block In after valid ON, destination, Taxi In, stationary, parking-brake,
session-valid and freshness conditions. If its generic engine evidence remains
operating or invalid, manual confirmation may correctly remain unavailable; stop
and preserve that evidence rather than bypassing the guard.

The frozen candidate and its packaged files remain unchanged.
