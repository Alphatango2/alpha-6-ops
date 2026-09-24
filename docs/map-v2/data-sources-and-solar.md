# Map v2 data sources and physical day/night

Research checkpoint: 7.21. Sources checked 2026-09-24. This is a design and provenance record, not a change to the production map or a blanket legal clearance. Provider statements below describe the linked terms as checked; implementation recommendations are identified separately. Recheck the selected dataset, package version and service agreement before distribution.

## Recommendation

Use the repository's Natural Earth land GeoJSON for the isolated, offline prototype. Bundle the renderer, style and required assets locally; render the actual geographic polygons over an ocean fill. Compute the Sun position locally from UTC. This gives real coastlines and physically anchored day/night without an account, API key, tile-server dependency or a large download.

Keep production data delivery interchangeable: a licensed hosted provider for online detail, or an independently hosted/distributed PMTiles package with its own verified data and asset licenses. Keep the Natural Earth map as the offline fallback. Do not make the public OSM tile service or a vendor's example key the production default. City lights can remain absent during this checkpoint.

## What is already in the repository

Read-only inspection of `src/Alpha6Ops.Desktop/Assets/Map/` found:

| Asset | Local facts | Suggested prototype use |
| --- | --- | --- |
| `ne_110m_land.geojson` | 138,160 bytes; GeoJSON FeatureCollection; 127 Polygon features | Small, low-detail offline fallback |
| `ne_50m_land.geojson` | 2,313,250 bytes; FeatureCollection; 1,420 Polygon/MultiPolygon features | Main offline land source; preserve polygon holes and multipart geometry |
| `ne_110m_land-mask.png` | 41,024 bytes; embedded by the desktop project | Existing raster mask; vector source is preferable for a newly derived prototype asset |
| `provenance.json` | Identifies Natural Earth 1:50m land, “3.3.0 GeoJSON distribution,” public domain, and the 50m physical-vector download page | Existing provenance claim; it is not an independently verified release checksum |

All three map assets are embedded in `Alpha6Ops.Desktop.csproj`. The current `FlightRouteMap.cs` uses the two GeoJSON resources; its `DrawTerminator` method draws a fixed screen-space gradient and glow without UTC or solar coordinates. It therefore does not establish physical day/night behavior for a future renderer.

Current SHA-256 fingerprints (local identification, not publisher authenticity verification):

```text
ne_110m_land.geojson
9E0729EE253CA7D7A5C4AE9395FB1902264C5377C52E224D13DD85010E2835D9
ne_50m_land.geojson
2107631799E2492BB27C0F977C195362A230C7654EA31EBE21589791B4D4C9EE
ne_110m_land-mask.png
C57EB32B0CC5D48DB5662E456C3D86477D32ED5960E03E5B13776E4E9D3908AB
```

Before a later release, expand the asset manifest to cover each source/version, download URL, original hash, transformation, output hash and license. The present manifest specifically describes the 50m source; it does not establish a complete acquisition/transformation history for the 110m files or PNG.

## Data, renderer and delivery rights are separate

### Natural Earth: suitable for the offline geography baseline

**Publisher terms:** Natural Earth places its published raster and vector map datasets in the public domain, permits personal and commercial use and modification, and says permission and attribution are unnecessary. It suggests “Made with Natural Earth” if credit is desired. The publisher also disclaims accuracy/content responsibility. [Natural Earth terms](https://www.naturalearthdata.com/about/terms-of-use/).

**Engineering recommendation:** retain the voluntary credit and the provenance manifest anyway. Land polygons provide geographic context but not street detail, satellite imagery, terrain, weather or navigational accuracy. The 50m dataset's scale is a reason to limit visual expectations at close zoom. Any later boundary/label layers should have their own source records; the existing land-only assets do not justify invented country boundaries or city positions.

### MapLibre: renderer, with no bundled production data entitlement

MapLibre GL JS's license permits source/binary redistribution subject to retained notices and non-endorsement conditions; the license file also contains notices for included third-party code. Preserve the complete license/notices shipped with the chosen release. [MapLibre GL JS license](https://github.com/maplibre/maplibre-gl-js/blob/main/LICENSE.txt).

MapLibre can read a PMTiles source using a registered protocol and caller-selected archive URL. There is no MapLibre provider account requirement inherent in this integration. **Inference:** a local style plus local data can run without a map-provider token; a remote style's tiles, fonts and sprites may still require one. A renderer's license never grants rights to arbitrary map content or a hosted API. [Official PMTiles/MapLibre integration](https://docs.protomaps.com/pmtiles/maplibre).

### MapTiler Cloud: credible online option; conditional offline rights

**Service terms:** MapTiler requires the customer's own API keys. Its Free Plan is limited to noncommercial use and research/development for commercial applications, within plan limits. Temporary personal caching for a single end user is permitted. Bulk downloads, export outside the service and proxy arrangements require a written custom agreement; server-side caching/redistribution is restricted. These are Cloud terms, not a conclusion about separately licensed MapTiler data/server products. [MapTiler Cloud terms](https://www.maptiler.com/terms/cloud/).

The API reference requires a MapTiler key. Attribution guidance requires linked MapTiler and OpenStreetMap text for the documented map setup; Free accounts additionally require the linked MapTiler logo. Other selected datasets may add credits. [API reference](https://docs.maptiler.com/cloud/api/reference/), [attribution guidance](https://docs.maptiler.com/guides/map-design/attribution/add-attribution/).

**Recommendation:** evaluate later as an online provider using an authorized key and suitable plan. Do not bundle Cloud tile downloads as the offline fallback. Keep key configuration outside checked-in assets, restrict usage appropriately, and verify desktop/WebView origin behavior before selecting restrictions. An embedded client key is not a server secret. [Key-protection guidance](https://docs.maptiler.com/guides/credentials/api-key/).

### Protomaps / PMTiles: credible self-hosted and offline path

PMTiles is a read-only single-file tile archive. Readers use HTTP range requests; the documentation describes serving local archives with a range-capable local HTTP server. Its CLI can instead expose archives as conventional Z/X/Y tiles for clients without a PMTiles reader. [PMTiles concepts](https://docs.protomaps.com/pmtiles/), [CLI serving options](https://docs.protomaps.com/pmtiles/cli).

The specification is public domain/CC0 where applicable; the reference implementation has a BSD-3 license, and example tilesets retain their own terms. The container format does not erase the content's license. [PMTiles license](https://github.com/protomaps/PMTiles/blob/main/LICENSE).

Protomaps describes its OSM basemap as an ODbL Produced Work requiring OSM attribution. Its current documentation describes approximately 120 GB for the full planet through zoom 15, discourages hotlinking download URLs, and supports regional or reduced-zoom extracts. [Basemap downloads](https://docs.protomaps.com/basemaps/downloads).

**Engineering inference:** a deliberately limited archive, local styles, fonts/glyphs, sprites and renderer can form a fully offline package. A WebView host needs a tested local asset/range-request mechanism; a normal `file://` URL alone is not an offline-integration proof. Self-hosting also brings storage, distribution, update and bandwidth costs. Choose a bounded region/zoom budget first, pin a build/hash, preserve attribution, and review any database modifications under ODbL before redistribution. OSM's own summary distinguishes its open-data license, attribution, and data-distribution obligations. [OSM copyright and license](https://www.openstreetmap.org/copyright).

### Public OSM tile endpoints: no unlimited production/offline assumption

The public raster endpoint `tile.openstreetmap.org` is capacity-limited and best-effort. Its policy requires visible attribution, application identification, appropriate browser referrers and HTTP-aware caching (at least seven days when headers cannot be interpreted). It prohibits bulk/prefetch downloading and offline-download features; access may be blocked without notice. Ordinary cache reuse is distinct from packaging maps for offline use. [Raster tile policy](https://operations.osmfoundation.org/policies/tiles/).

The separate `vector.openstreetmap.org` policy likewise requires attribution/identification and caching, prohibits bulk downloads including offline archives, and provides no SLA. Switching from raster to the public vector endpoint does not solve the production/offline problem. [Vector tile policy](https://operations.osmfoundation.org/policies/vector/).

**Recommendation:** use neither public endpoint in the isolated prototype. Production should use an agreed service or self-hosted data, with configurable sources and a local fallback.

## Physical day/night independent of the basemap

### Solar references and implementation rights

NOAA publishes a short fractional-year approximation for declination and equation of time, with east-positive longitude and explicit leap-year handling. These equations support an inexpensive offline visual terminator. [NOAA solar equations](https://gml.noaa.gov/grad/solcalc/solareqns.PDF).

NOAA's separate calculator details describe a Meeus-based method and date-range limitations; the calculator is no longer maintained. Do not transfer that calculator's sunrise-time accuracy statement to the simpler fractional-year approximation. [NOAA calculation details](https://gml.noaa.gov/grad/solcalc/calcdetails.html).

NOAA GML says its government information is generally public domain unless marked otherwise, requests acknowledgment, and prohibits implying endorsement or presenting modified material as official. Third-party exceptions remain relevant. **Recommendation:** implement the published mathematical method with clear NOAA provenance, retain applicable notices, and avoid copying unrelated website assets or third-party code. [NOAA GML disclaimer](https://gml.noaa.gov/about/disclaimer.html).

NREL's published SPA reference is useful for higher-accuracy comparison. The current official SPA page is served by NLR at `midcdmz.nlr.gov` and states a nominal ±0.0003° uncertainty over years −2000 to 6000. **Critical software distinction:** its distributed software notice restricts use to internal noncommercial purposes, prohibits redistribution, and directs commercial users to obtain a commercial license. Do not copy/bundle `spa.c`, or assume a third-party port eliminates those restrictions. No download form or license acceptance was submitted for this research. [Official SPA description and software notice](https://midcdmz.nlr.gov/spa/).

### Proposed computation and drawing contract

Use a single explicit UTC instant for an entire frame. In live mode obtain it from the app's clock; replay/preview supplies a frozen timestamp. Do not derive geography lighting from Windows local time, the selected airport's timezone, camera position or UI theme.

Using NOAA's fractional-year method, calculate declination `delta` in radians and equation of time `E` in minutes. Let `m` be UTC minutes since midnight, including seconds. Setting the solar hour angle to zero gives this derived subsolar position:

```text
subsolarLatitude  = delta
subsolarLongitude = wrap180(180 - (m + E) / 4)  // degrees, east positive
```

For geographic latitude `phi` and longitude `lambda` in radians, choose one consistent Earth-coordinate basis:

```text
n = (cos(phi)*cos(lambda), cos(phi)*sin(lambda), sin(phi))
s = (cos(delta)*cos(lambdaSun), cos(delta)*sin(lambdaSun), sin(delta))
d = clamp(dot(n, s), -1, 1)
solarElevation = asin(d)
```

These are the spherical-vector form of the solar-zenith relation. Geometric day has `d > 0`; the geometric terminator has `d = 0`. Rotating the camera changes projection, not which geographic point is in daylight. At the poles this avoids dividing by latitude-dependent terms. Transform both vectors consistently if the renderer uses a different axis convention.

Suggested presentation policy: blend daylight into night over solar elevations from approximately 0° to −6°, while retaining some base color and coastline contrast on the night side. Treat the blend as an artistic visualization of twilight. Civil, nautical and astronomical twilight use solar-center elevations −6°, −12° and −18° respectively; conventional apparent sunrise/sunset uses additional refraction/solar-disc assumptions, rather than the geometric zero-degree line. [US Naval Observatory definitions](https://aa.usno.navy.mil/faq/RST_defs).

Apply geographic shading beneath aircraft, route and essential labels so those remain readable. The same solar state should drive the globe and flat map. Prefer a world-space shader or geographic mask; if using a polygon, split at the antimeridian and handle polar caps explicitly. A screen-aligned linear gradient cannot satisfy this contract. The effect represents solar geometry, not current clouds, weather or terrain-shadow visibility.

Proposed verification before production integration:

1. Freeze a UTC timestamp, rotate/pan/zoom, and confirm a chosen location keeps the same calculated solar elevation.
2. Check equinox and both solstice cases, including expected north/south polar-day reversal.
3. Check antipodal positions, antimeridian continuity, leap day, and the transition across UTC midnight.
4. Compare several fixed coordinates/timestamps against an independent published solar reference, documenting an angular tolerance suitable for the chosen approximation; an invariant-only check is insufficient.
5. Capture the same route with day, twilight and night behind it and assess label/aircraft readability.

These are proposed acceptance checks; this research did not execute or certify a renderer implementation.

## Optional city lights

NASA Earth Observatory's Black Marble flat-map collection provides geographic JPEG/GeoTIFF products, including 2016 composites. They are historical, cloud-filtered composites, not live electricity or present-night observations. Use the geographic flat-map asset if later mapping a texture; a presentation image is not necessarily an equirectangular texture. [Black Marble flat maps](https://science.nasa.gov/earth/earth-observatory/earth-at-night/maps/).

NASA's general media guidance permits many informational/editorial uses, requests source acknowledgment, prohibits implied endorsement and preserves third-party rights. Check the exact asset's credit and restrictions rather than asserting all NASA imagery is unrestricted. For example, the SVS Black Marble 2016 item specifies NASA's Goddard Space Flight Center credit. [NASA media guidance](https://www.nasa.gov/nasa-brand-center/images-and-media/), [specific Black Marble item](https://svs.gsfc.nasa.gov/30876).

**Recommendation:** defer city lights until the base map and terminator pass review. A later approved texture should have a recorded source/date/license/hash, be faded in only on the night side using the same solar mask, and be described as a historical composite. No image was downloaded or added in this research.

## Provider-neutral production boundary

Keep these concerns separate in the eventual renderer contract:

- **View state:** camera, projection, route, telemetry, selection and UTC. Persist these independently of provider URLs.
- **Source descriptor:** dataset identity/version, style/schema compatibility, tile/asset locations, zoom range and bounds. Switching tile schemas may also require a compatible style, rather than only changing a URL.
- **Access and policy:** key reference, attribution text/links, permitted cache behavior, offline-package entitlement and expiry/update rules. Never infer permission from a successful download.
- **Local package manifest:** renderer and asset licenses, hashes, glyph/font/sprite assets and all URLs needed for a network-disabled run.
- **Failure behavior:** retain camera and operational overlays; show the bundled Natural Earth geography when the selected online source is unavailable, with a concise source/offline status.

The production selection remains open. Checkpoint 7.21 can prove geography, camera and physical lighting using existing local assets without making a provider purchase, accepting a new service agreement or committing the application to one data vendor.
