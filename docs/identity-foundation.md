# Accounts and virtual airlines

## Product decisions

One Alpha 6 account can fly personally and belong to multiple virtual airlines. The personal
workspace is free and contains the logbook, active-flight dispatch, tracking, weather, settings,
and flight import. Each airline has independent membership and permissions.

Personal Free/Premium and airline Community/Pro are independent subscription dimensions. They
do not grant administration. Founder is historical provenance; Owner is the current authority.
An active airline has one owner. Administrators cannot promote themselves to owner through a
role edit. Ownership transfer preserves the founder and retains the former owner as an administrator.

A verified user with recent MFA can create one Community airline. Premium/Pro payment collection,
cloud logbook upload, operational dispatch ingestion, public airline discovery, and automated
invitation email delivery are subsequent work. Invitation codes can be shared manually.

## Components

| Component | Responsibility |
|---|---|
| `Alpha6Ops.Identity` | Shared account, workspace, capability and request contracts; no external dependencies |
| `Alpha6Ops.Accounts` | PostgreSQL persistence, tenant authorization, account provisioning, invitations, ownership and audits |
| `Alpha6Ops.Server` | Razor Pages account portal, Auth0 OIDC cookie sessions, desktop bearer API |
| Desktop identity integration | System-browser login, Windows-protected session cache, account/workspace selection |
| Existing `Alpha6Ops.Api` | Local synthetic replay API; deliberately remains Development-only and loopback-only |

The cloud projects have a separate `Alpha6Ops.Cloud.slnx`. The existing desktop solution retains
the simulator/domain build path. NuGet.org is enabled for the maintained OIDC and PostgreSQL
dependencies required by this feature. The domain/replay library remains package-free.

## Security boundary

Auth0 verifies credentials. Alpha 6 does not collect or store passwords, password hashes,
passkey private keys, recovery codes, or authenticator seeds. Enable email/password, Microsoft,
Google, passkeys, TOTP, recovery, and security notifications in the identity provider; these
capabilities require real tenant configuration before they can be verified end to end.

The server accepts identity from validated issuer and subject claims. Email is mutable profile
data, never an account key and never a basis for automatically merging a social identity.
Namespaced, provider-signed claims communicate email verification and completed MFA. The server
checks MFA freshness for privileged operations; an enrollment flag alone is insufficient.

The API accepts bearer access tokens for its own issuer/audience. Website cookies cannot
authorize API requests. Razor mutations require antiforgery validation. Airline ID parameters
select resources only after the account's current database membership is checked. Client-visible
capabilities are presentation hints, never an authorization source.

Invitation secrets are high-entropy, single-use, expire after seven days, and are hashed in the
database. Acceptance requires the intended verified email. Raw codes belong in request bodies,
not URLs or request logs. Successful repeated acceptance by the same account is idempotent.

The first account service uses a PostgreSQL transaction-scoped advisory lock to serialize account
mutations and guarantee correct concurrent provisioning/ownership limits. This deliberately favors
simple correctness for a small initial deployment; contention should be measured before launch at
larger scale and replaced with consistent row-level locking when justified.

## Desktop data and offline use

Authenticated data belongs to the immutable account UUID and selected workspace. Legacy local
history is preserved separately and must not be silently attached to the first person who signs
in. Simulator settings and diagnostic stores must not expose another account's active assignment.

The trusted offline period is 30 days from the last successful server bootstrap. It allows local
pilot activity only and never authorizes cloud requests or administration. Offline launch must
not extend the expiry. Reconnection rechecks account and membership status. A known authorization
denial invalidates cached access instead of falling back to offline mode.

Windows DPAPI protects cached credentials for the current Windows user. This is not a boundary
against an administrator or malicious software already running as that same Windows user.
Separate Windows accounts are needed for operating-system isolation on a shared PC. Account
switching in Alpha 6 must clear in-memory data and never relabel an in-progress flight.

## Local build

```powershell
dotnet build Alpha6Ops.slnx
dotnet run --project tests/Alpha6Ops.Tests --no-build
dotnet build Alpha6Ops.Cloud.slnx
```

See the [server setup guide](../src/Alpha6Ops.Server/README.md) for exact Auth0 claims, public identifiers, environment settings,
PostgreSQL migration commands, and portal launch instructions. Database tests require a
disposable database through `ALPHA6_TEST_DATABASE`; never point them at a live account store.

Unconfigured Development may render public pages with account services unavailable. Production
must reject incomplete identity configuration. No mock account, hardcoded administrator, or
development bypass may authorize a production API call.

### Connect the desktop

To inspect the desktop login screen before configuring Auth0, run a current build with
`Alpha6OPS.exe --preview-login`. This opens the same welcome view in a clearly labeled preview.
Continue opens a second screen with selectable personal and sample airline cards. Back and
Sign out return to the welcome view. Opening a sample workspace explains the preview boundary;
it never loads credentials, contacts an identity provider, creates a session, or enters an
authenticated workspace. It can run
alongside the normal desktop app. Password, MFA and passkey forms belong to Auth0 Universal
Login and require a configured tenant.

The redesigned account portal uses aircraft imagery, dark navy/graphite surfaces and warm
yellow accents across sign-in and workspace selection. The Razor website carries the same
theme through its account, download, support and airline-management forms. A configured
desktop build sends the entered email as an OIDC `login_hint` only;
the provider still verifies identity. Other sign-in options open the same secure browser flow.
The workspace screen uses live membership/role/plan data, retains offline airline restrictions,
and requires an explicit selection followed by Open workspace. Long membership lists scroll
while the selection and Open workspace action remain visible.

To build and open the latest interactive design without closing the installed app:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/preview-account.ps1
```

The launcher builds to an isolated timestamped folder under `work/account-previews` so an open
app cannot lock its output. Use Preview your workspaces, select either sample airline or
Flying as a Pilot, and use Back to return to sign-in. Live browser authentication remains a
separate step requiring Auth0 setup.

Dark redesign verification passed 12 account UI checks plus the existing 18 desktop and 436
dashboard checks. Screenshots at 1240×820 and 960×680 are in `work/account-dark-review`.
Portal/API integration checks passed. Public website pages were browser-checked at desktop
and mobile sizes, including navigation, no horizontal overflow at 390 px, and browser errors.

The build/publish output includes `alpha6-identity.example.json`. Copy it to
`alpha6-identity.json` beside `Alpha6OPS.exe` and replace the public placeholders with your
Auth0 issuer, Native Application client ID, API audience, and HTTPS API/portal addresses.
Do not add a client secret. Register the exact native callback
`http://127.0.0.1:42879/callback/` in Auth0; if changing `CallbackPort`, change that registration
as well. The listener binds only to loopback and reserves the port before opening the browser.

Without that file, the existing desktop remains explicitly labeled Local Preview. A malformed
file fails startup. With valid configuration, normal startup requires sign-in or an unexpired
trusted offline session. Local preview cannot authorize hosted airline actions.

The personal home brings the flight tools together with reduced navigation. The logbook and
tracker occupy the main window; existing weather/settings/import dialogs remain in use.
Airline workspaces currently use isolated local flight tools and website management; shared
cloud dispatch and flight uploads are not implemented.

### Repeat local verification

On this prepared Windows workspace, close OPS using **Exit OPS** and run:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/test-identity-local.ps1
```

This wrapper starts the existing isolated PostgreSQL 17 test cluster when needed, runs the full
verification, opens the desktop, checks for its window, and saves console output under
`work/identity-runs/<run>/console.txt`. It stops only a database instance it started. Use
`-LaunchOnly` to open the existing desktop build without running tests. It does not create an
Auth0 tenant or enable live sign-in. Missing prerequisites and startup failures are reported
in the saved log.

Use disposable local PostgreSQL databases named `alpha6_identity_test` and `alpha6_server_test`.
Set `ALPHA6_TEST_DATABASE` and `ALPHA6_SERVER_TEST_DATABASE` to their connection strings,
then run `powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/verify-identity.ps1`.
The script builds both solutions and runs domain, database, hosted API, desktop-session,
Auth0 Action unit checks, and desktop UI checks. It fails if database configuration is missing.
Tests preserve their uniquely named account/schema data and UI artifacts under `work/` for review.

## Production activation prerequisites

The source implementation and local tests cannot establish a live Auth0 session or publish a
signed Windows installer. Activation requires:

1. An organization-controlled public domain and Auth0 custom authentication domain, plus separate
   native and regular web applications per environment. Register exact redirect/logout URLs.
2. A PostgreSQL database and a least-privilege application account; deploy migrations separately
   from normal startup with a migration role. Test backup restoration before inviting real users.
3. An Azure subscription and region. Run the .NET portal on App Service, connect PostgreSQL over
   private networking, and store secrets in Key Vault using managed identity references.
4. A persistent, protected ASP.NET Data Protection key ring shared across production instances.
   The server currently requires a persistent directory plus an encryption certificate. Native
   Blob Storage/Key Vault key wrapping remains a deployment improvement; retain shared keys
   and decryption certificates across instance replacement and deployment swaps.
5. Explicit trusted proxy configuration at the HTTPS boundary. Never accept arbitrary forwarded
   host/scheme headers or wildcard Auth0 callbacks.
6. A code-signing identity, timestamp service, verified signed installer, SHA-256 digest and release
   URL. A configured download URL is not itself proof that its binary was signed.
7. Provider security policies, MFA recovery, factor enrollment/removal, account notification and
   security-event monitoring validated in staging. Avoid weakening MFA to make development easy.

No production cloud resources are provisioned by merely building the repository. See Microsoft's
[managed identity guidance](https://learn.microsoft.com/en-us/azure/app-service/overview-managed-identity)
and [Data Protection configuration](https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/configuration/overview?view=aspnetcore-10.0)
for the deployment integrations.

## Release acceptance

Local verification on 15 September 2026 passed both solution builds with zero warnings/errors,
84 domain checks, 64 account checks against PostgreSQL, 30 portal/API checks, 27 desktop-session
checks, and Auth0 Action unit checks. Desktop verification passed 18 existing checks,
436 dashboard checks, seven identity UI checks, and single-instance activation. The explicit
database migration command also completed successfully. Screenshots/reports from the combined
run are in `work/dashboard-review/20260915-171431-d867cf22` (local, ignored by Git).

The tests use a local disposable PostgreSQL instance and a test-only JWT signer; they do not
establish a real Auth0 session or verify provider enrollment, recovery, or passkey behavior.

Local automated checks must exercise real PostgreSQL constraints and concurrent operations,
expired/reused/mismatched invitations, cross-airline denials, role/owner invariants, subscription
fallback, invalid bearer tokens, cookie-only API rejection, CSRF, and desktop session expiry.
Preserve the existing simulator/recovery regression and desktop smoke suite.

Staging must additionally demonstrate real email/password, social and passkey login, TOTP,
recovery, browser cancellation/callback errors, refresh revocation, 30-day offline boundaries,
multiple accounts and airlines, safe logout during a flight, signed download and upgrade, and
database restoration. A successful build does not substitute for these external checks.
