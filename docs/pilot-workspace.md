# Free Pilot workspace

## Existing dashboard, fewer features

Free uses the same MainWindow dashboard, dark aviation styling, aircraft hero image, photographic module tiles, yellow accents, typography, navigation rail, status cards and instruments as the full app. It does not use a separate simplified shell.

The pilot feature profile keeps:

- Dashboard with the pilot's active assignment and flight table. New accounts start empty; bundled demo flights are not shown.
- Pilot Logbook, with existing account-scoped completed and interrupted flight records.
- Dispatch through the embedded release workspace, including SimBrief import, review, acceptance, the OFP viewer, and manual flight setup. The Flight Deck drawer remains available for advanced tools and diagnostics.
- Flight Tracking with the original embedded map, flight progress and recorded events.
- Local weather through the existing header and observation detail dialog.
- Settings, account/workspace access, simulator connection, recovery, debrief, logs and journal export.

Airline alerts, network map, fleet reference, company messages, staff/airline navigation and watchlist controls are removed from the Free dashboard. Remaining panels expand into their space. The header, imagery and instrument design remain intact. The original reference surface scales with the window as before.

## Recommended Free boundary

Keep personal flight setup, import, local history, recovery, units and export in Free. Account security must remain available regardless of subscription.

Next Free priorities are departure/destination/alternate METAR and TAF lookup, portable logbook CSV export and search/filter. Current weather is approximate local city model weather, not airport or simulator weather. Current journal export is diagnostic export, not a complete logbook interchange format.

Reserve rosters, fleet administration, shared schedules, staff dispatch, invitations, organization reports and staff roles for airline workspaces. Cloud sync and advanced personal analysis could differentiate Premium later. A personal subscription must never grant airline administrator permissions.

## Extension boundary

`PilotFeatureProfile` defines the allowed personal modules and their photo tiles. `MainWindow.Account.cs` applies the profile to the existing UI; `MainWindow.Responsive.cs` reapplies the panel layout after responsive changes. Existing MainWindow actions, account-scoped stores and recording logic remain in use.

Future airline capability profiles can expose additional existing modules in the same interface. Server operations must still authorize membership and role; UI visibility is not authorization. Until shared airline operations are implemented, signed-in airline workspaces also expose only these local personal flight tools, scoped to the selected airline directory.

## Preview

The **Alpha 6 OPS - Login** Windows desktop shortcut runs `scripts/launch-desktop.ps1`. Each click builds the latest source into its own `work/desktop-launcher/` directory and opens the login preview, even if another instance is open. Choose **Preview your workspaces → Flying as a Pilot → Open workspace** to enter the original dashboard with the pilot feature profile. The preview does not authenticate an account; sample airline cards cannot open an airline workspace. Each launch has isolated flight data. Build errors are saved in that launch directory's `build.log` and surfaced in a dialog. Recreate the shortcut with `scripts/install-desktop-launcher.ps1` if the repository moves.

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "C:\Users\Reece Czarnecki\dev\alpha-6-ops\scripts\preview-pilot.ps1"
```

The script builds into a new folder and launches `--preview-pilot`. It creates fresh isolated local data, loads no credentials and starts without flight records or an assignment. Weather requests are disabled. Normal startup authentication is unchanged. It can run alongside the installed app.

## Verification

The WPF smoke suite verifies the original dashboard remains visible, restricted modules and airline panels are hidden, only the four personal tiles remain, account history is isolated, the hero restores the saved assignment, dispatch uses the embedded release workflow, and clearing an assignment preserves recorded history. SimBrief usernames, OFP text/PDF caches and viewer preferences are checked for workspace isolation. It captures full and compact dashboard, logbook, dispatch and tracker screenshots. Existing recorder, SimBrief parsing, recovery and desktop regressions run in the same suite.

Live MSFS connection and a real Auth0 sign-in require external configuration and are not established by these local UI checks.

Verified locally on 16 September 2026: build passed with zero warnings/errors; 18 desktop, 436 dashboard and 26 account/pilot UI checks passed. Inspected screenshots and reports are in `work/pilot-dashboard-verified/`.
