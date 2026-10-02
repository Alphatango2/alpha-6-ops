# 7.24B — physical Map V2 input gate

Status: **FAIL — left-drag panning; native MSFS validation on hold.**

Recorded 25 September 2026. The user reports that, with MSFS still closed and Map V2 manually selected in the installed frozen validation build, left-click and drag produces no usable map movement. Right-click and drag changes map bearing/rotation instead of panning. The exact time of the physical gesture, Windows scaling and pointer event sequence have not been captured. This is user-observed physical evidence, not a result inferred from automation.

Validation build: `S4-7.24B-20260925-46a1b304c3d6`, version 0.22.0. The previously resolved installer failure remains a separate incident. Installation identity passed; that does not establish physical input success.

## Immediate disposition

- Part 6 mouse pan: **FAIL / input ineffective**.
- Right drag: rotation observed; not a successful pan check.
- Other physical controls and DPI/window checks: not yet established by this report.
- Native connection lifecycle, reconnect and flight: **not started; hold**.
- Classic remains default/fallback; Map V2 remains opt-in. No setting or frozen payload change is authorized by this record.

## Diagnosis — host routed-input/focus conflict

**The failure was reproduced and isolated to the WPF host's mouse-event bubbling and focus behavior around WebView2CompositionControl.** It is not disabled MapLibre panning or an absent left-button signal. This conclusion has physical A/B evidence in a minimal equivalent host; the installed application's gate remains failed until a separately frozen correction is installed and physically retested.

The user additionally confirmed that normal left-click on the map's `+` button and mouse-wheel zoom both work, while horizontal and vertical left-drag both fail. The Windows saved `SwapMouseButtons` value is `0`.

Relevant chain:

1. `OperationsWorkspace.xaml:10` places `OperationsMapHost` inside a WPF `ScrollViewer` with vertical panning. The map is at line 40.
2. The pinned WebView2 WPF SDK **1.0.4191.47** forwards mouse-down and move events to its composition controller. Inspection of that exact SDK shows it supplies left/right held-button flags, and does not mark `OnMouseDown` handled. Sierra's `GuardedCompositionControl` only guards template initialization; it adds no mouse handling.
3. An unhandled left-click continues into the parent ScrollViewer. Its `OnMouseLeftButtonDown` calls `Focus()`. The physical trace records the map gaining focus, immediately followed by the ScrollViewer taking focus.
4. The browser receives `mousedown` with **button 0 / buttons 1**, then loses focus. Subsequent physical move events still contain **buttons 1**, but the camera does not move. The bundled MapLibre handler stops interactions on window `blur`.
5. In the isolated probe only, marking the map's routed mouse-down handled after SDK forwarding prevents the parent taking focus. The same map assets, SDK, runtime and physical mouse then pan normally. Returning to the original routing reproduces failure.

The exact lower-level mapping hypothesis was tested rather than assumed: the browser's correct held-left-button values rule out missing left-button flags for these recorded gestures. The host must consume the map's handled gesture without forwarding duplicate input or globally disabling page scrolling.

### Physical A/B evidence

The separate diagnostic window used the **unchanged installed browser assets**, the same pinned SDK and runtime **153.0.4234.48**, a ScrollViewer parent, and an isolated browser cache under `work/`. It loaded no Alpha6OPS application/Core assemblies, simulator integration, flight data or profile files. Mode A retained normal bubbling; mode B contained mouse-down at the map boundary. MapLibre settings and browser assets were identical in both modes. Input was supplied physically by the user, not CDP, DOM dispatch, or synthesized Windows input.

| Mode | Left drags with at least 10 px pointer travel | Camera moved during drag | Focus lost during drag |
|---|---:|---:|---:|
| A — current bubbling | 4 | 0 | 4 |
| B — contained bubbling, probe only | 3 | 3 | 0 |

The run included A → B → A → B reversals, **355 trace records**, and correct held-left-button flags throughout all seven left drags. For example, the first B drag moved the camera from longitude 0 / latitude 20 to approximately 12.525 / 21.737 at mouse release, with bearing unchanged at 0. The user explicitly confirmed: **“Fails in A but works in B.”** The helper closed normally at **20:56:55 UTC**. Its build completed with 0 warnings and 0 errors. These are diagnostic results, not a new passing production regression gate.

### Right-button behavior is a separate interaction policy

The constructor in `MapV2/Web/map.mjs:196` does not override `dragPan`, `dragRotate`, `interactive` or cooperative gestures. The exact bundled MapLibre defaults enable pan and rotation. Its left-drag handler expects button 0 without Ctrl; right-drag rotation is expected under the current defaults. Thus right-drag rotation is **configuration behavior**, while ineffective left-drag is the **host focus defect**. Changing the right button to pan would conceal neither the defect nor its cause. If north-up-only interaction is desired for the flat map, explicitly disabling drag rotation is a separate small presentation-policy decision; it was not applied here.

The reduced map CSS also omits some standard touch/selection declarations. This did not explain the recorded mouse failure: B pans with the same CSS. Touch/pen behavior remains unvalidated.

### Why prior automation missed this

`MapPerformanceChecks.cs:315–322` sends `Input.dispatchMouseEvent` directly into Chromium. Its retained passing drag result establishes browser-level panning, but never exercises WPF bubbling into ScrollViewer. The existing test already explicitly disclaims physical evidence. No prior automated pass has been reclassified as physical validation.

## Recommended correction and validation boundary

**Correct this as a narrowly scoped 7.24B installation/physical-input gate remediation before native MSFS testing.** Do not proceed with the failed installed build and do not wait for the globe checkpoint.

- Preserve the original frozen build and this failed physical run unchanged.
- Contain map-originated left mouse-down at the host boundary after the existing SDK has processed it, using the same CompositionControl and MapLibre. Do not change rendering architecture, input button definitions, simulator integration, flight processing, or recovery.
- Verify real routed events retain browser focus, drag survives in both directions, clicks/selection and wheel zoom still work, and input outside the map still scrolls/navigates normally. Verify Escape and keyboard exit, overlays, capture/release, repeated workspace creation, and disposal. The small probe's B mode is not a finished production patch or proof of these additional behaviors.
- Re-run the required affected automated checks, package/identity checks and physical installed checks in a distinctly identified replacement validation build. Keep evidence for the old and new freezes separate. Recheck upgrade/data compatibility; do not overwrite the original frozen installer.
- Native lifecycle/reconnect and flight stay on hold until the replacement installed build passes physical input. Classic remains default; Map V2 remains session opt-in.

No production source, installed payload, default setting or frozen package was changed in this diagnosis. No MSFS process was launched. This is not a solution to the separate A-series composition-scheduling issue.

## Integrity, artifacts and references

Installed immutable payload recheck: **514/514 matched**. Reporter inventory: **26 before, 26 after, 0 new** at 20:50:20 UTC. The running installed process was still PID 9668; no MSFS process was observed in the process inventory. That inventory and the user's report establish this pre-native context, not a native connection test.

Final integrity check at 21:00 UTC matched **222/222 exact-byte source-manifest entries**, **514/514 installed payload files**, and both frozen installer/ZIP hashes. An initial Git diff comparison was unsuitable because the frozen snapshot contains diagnostic source files still untracked in the normal index; the retained comparison result records that limitation. The exact-byte manifest check includes those files and found no differences. The normal project package-source restriction was preserved when a proposed decompiler tool was unavailable; inspection used a dependency-free local reflection helper instead. Neither helper changes production source. This diagnostic trace is evidence of input/focus behavior, not a responsiveness benchmark.

Compact evidence, trace, helper source and analysis are in `docs/map-v2/evidence/7.24b-physical-input/`. The executable, private browser cache, exact SDK IL listing and full build output remain in `work/checkpoint-7.24b-physical-input/`. The helper does not ship with the application. Only documentation/evidence was changed in the tracked project areas.

Supporting primary references:

- [Microsoft's CompositionControl design](https://github.com/MicrosoftEdge/WebView2Feedback/blob/main/specs/WPF_WebView2CompositionControl.md): explains forwarding WPF mouse input into the composition controller.
- [WPF ScrollViewer source](https://github.com/dotnet/wpf/blob/main/src/Microsoft.DotNet.Wpf/src/PresentationFramework/System/Windows/Controls/ScrollViewer.cs): parent left-button handler focuses the ScrollViewer.
- [MapLibre map options](https://maplibre.org/maplibre-gl-js/docs/API/type-aliases/MapOptions/) and [map interaction handlers](https://maplibre.org/maplibre-gl-js/docs/API/classes/Map/): pan/rotation options and right-button rotation. The exact installed bundle was also inspected; the diagnosis does not depend solely on current online documentation.
