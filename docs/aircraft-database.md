# Aircraft reference database — 0.4

Delta Air Lines mainline current fleet was captured from the visible Airfleets.net tables on 2026-09-02. All 34 pages across ten families were read. The 1,006 unique registrations reconcile to the summary's 999 active plus seven parked aircraft. Stored/scrapped, historical, on-order and Delta Connection operators are outside this snapshot.

Source: https://www.airfleets.net/flottecie/Delta%20Air%20Lines.htm

| Family | Current registrations |
| --- | ---: |
| A319 | 57 |
| A320 | 44 |
| A321 | 229 |
| A330 | 81 |
| A350 | 41 |
| 717 | 78 |
| 737 NG / Max | 239 |
| 757 | 90 |
| 767 | 57 |
| A220 | 90 |

Airfleets' parked category refers to aircraft not flown for 20 days without information that they have left the operator. Parked is preserved separately from Active. Status is the site's observation, not a technical airworthiness or dispatch-availability decision. Delivery dates are retained verbatim (usually day/month/year) and may represent entry into the operator's fleet, including merger/re-registration events; do not use them to infer manufacture date or age. Historical notes, photographs, logos and descriptions are not copied.

## Files and schema

- `datasets/delta-airfleets-2026-09-02.json`: reviewed factual snapshot, aircraft-level source URLs, source date, scope and reconciliation counts.
- `assets/fleet/aircraft.sqlite`: real SQLite reference database, bundled under `fleet/` beside the EXE.
- `scripts/build_fleet_database.py`: standard-library importer and database builder; no web requests.
- `outputs/Delta-Air-Lines-Fleet-2026-09-02.csv`: portable factual export.

`source_snapshot` holds provenance, observation date, operator DAL, scope and expected count. `aircraft_reference` is keyed by snapshot and registration; family/manufacturer-serial identity is also unique. Records contain registration, exact model, family, manufacturer serial, optional production line number, Active/Parked status, delivery-date text and page URL. Database constraints and importer validation reject duplicate identities, count mismatches, unknown status, unexpected URLs and unrecognized date formats. A staging database is committed, closed and then replaces the previous generated catalog. Existing catalog remains intact on validation failure.

The WPF viewer opens Windows' SQLite library in read-only mode, then filters the reference rows in memory. User search text is not interpolated into SQL. It validates external source links to the Airfleets HTTPS hostname. Missing/corrupt catalogs show an error rather than generating replacement records.

## Refresh and operational separation

Rebuild the current snapshot using `python scripts/build_fleet_database.py`, then run `packaging/build-desktop.ps1`. For a new dated snapshot, capture the visible table rows, update the import's date/identity and published reconciliation counts, normalize using `--capture <file> --snapshot <file>`, review the changes, then rebuild. The capture format is a list of pages with `url` and `rows`; each row has a `cells` array matching the published fleet table. Boeing tables include an extra production line-number column. The current importer intentionally pins this reviewed snapshot's totals; it is not an unattended scraper. Future refreshes require review, including any source format changes. No refresh automation is installed.

This database is public aircraft reference data and contains no pilot or tenant records. Future tenant-owned aircraft need separate `(tenant_id, aircraft_id)` identities referencing catalog entries, with their own virtual availability, rotation, maintenance and assignments. Replacing the reference catalog must never overwrite tenant operations. This change does not auto-match a simulator TITLE string to an assigned aircraft or alter the demo rotation.

N414DZ was verified in the A330 table as model 330-941N, manufacturer serial 1996, Active. Its in-sim Headwind livery is not proof that a corresponding virtual-airline assignment exists.

Validation completed: six importer tests (valid snapshot, incomplete capture, duplicate registration, invalid status, unexpected source and date precision); SQLite integrity and unique keys; actual WPF read of the packaged database; N414DZ identity; case-insensitive search, empty results and seven-aircraft Parked filter; existing desktop replay/tray regression checks. Fleet window rendered and visually inspected after correcting its family-column width. Interactive setup/uninstall on a clean PC remains unverified, as for earlier previews.

Before broader commercial redistribution or recurring automated access, establish the appropriate data-use arrangement with Airfleets. This task made a dated local development reference snapshot, not an ongoing data feed or a claim of partnership.
