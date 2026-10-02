# Predetermined heartbeat characterization

Recorded before execution. Twelve fresh process runs; no retry-until-pass.
Order of additional tracing: OFF, ON, ON, OFF, OFF, ON, ON, OFF, OFF, ON, ON, OFF.
Each run measures a 30-second idle loaded composition renderer and 30 seconds
of one-aircraft map updates at 1 Hz (synthetic map-only context; not a native
flight or the complete Operations presenter). Context order alternates.
A fresh renderer then runs the unchanged 395-sample baseline and 395-sample
accelerated flight with the existing 1-second JavaScript stall and original
heartbeat assertion. This gives twelve original gate comparisons, six per
instrumentation condition, plus 24 context observations over 12 minutes.

All timers remain Background/20 ms. Preserve first ticks, complete raw intervals,
pending tail ages, individual original assertions and every failed run. Context
comparisons are characterization only, never substitutes for the original gate.
Relative threshold: stress <= max(150 ms, paired baseline max + 100 ms).
Absolute threshold: stress < 500 ms. Context normal compares with same-run idle
using the same formula, separately labeled; no new acceptance rule is enacted.

Tracing ON adds existing dispatcher/activity/GC/CPU observations plus passive
native-message and pending-input queries, and WebView message timestamps. No
CompositionTarget.Rendering subscriber, independent UI probe, polling callback,
forced collection, pointer injection or new production scheduling is added.
OFF records only existing interval lists plus diagnostic context interval lists.
Native queries use zero-time MsgWaitForMultipleObjectsEx INPUTAVAILABLE; they do
not consume messages. Their timing/observer impact is not assumed zero.

No concurrent build or additional UI performance test during the series. Keep
native simulator/application closed, no user interaction requested. Per-process
240-second existing diagnostic timeout unchanged. Stop only for safety/process
failure; preserve missing/partial observations as such. Do not refill failed or
missing slots. After collection, analyze all intervals, not just maxima.

No production correction is pre-authorized by this plan's hypotheses. Frozen
T2 and existing evidence remain immutable. No install or native T2 launch.
