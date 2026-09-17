# Alpha 6 account portal and API

ASP.NET Core 10 Razor Pages portal, Auth0 Authorization Code + PKCE sign-in, JWT-only desktop API, and PostgreSQL-backed account/airline authorization. The existing local replay API is a separate application.

## Configure and run

Restore/build from the repo root:

```powershell
dotnet build Alpha6Ops.Cloud.slnx --configfile src/Alpha6Ops.Server/NuGet.Config
```

Set configuration through environment variables, a deployment secret store, or .NET user-secrets for `Alpha6Ops.Server`. Do not put credentials in tracked appsettings files:

| Environment variable | Value |
| --- | --- |
| `Auth0__Authority` | Exact HTTPS issuer, e.g. `https://YOUR-TENANT.us.auth0.com/` |
| `Auth0__Audience` | Auth0 API identifier; same audience used by desktop |
| `Auth0__ClientId` | Regular Web Application client ID |
| `Auth0__ClientSecret` | Regular Web Application client secret; server only |
| `ConnectionStrings__Accounts` | PostgreSQL connection string; production uses certificate-verified TLS (`SSL Mode=VerifyFull`) |
| `AllowedHosts` | Exact portal host names separated by semicolons |
| `DataProtection__KeyDirectory` | Persistent writable key-ring directory, shared by replicas |
| `DataProtection__CertificatePath` | Readable mounted PFX with its private key |
| `DataProtection__CertificatePassword` | PFX password supplied as a secret |
| `Release__DownloadUrl` | HTTPS URL of the actual signed installer |
| `Release__Version` | Matching release version |
| `Release__Sha256` | Matching 64-character SHA-256 checksum |

Run migrations using the Accounts project's documented migration procedure before starting the server. The server does not migrate the database on startup. Use a restricted runtime database principal after applying migrations with a separate migration principal.

For development, use an HTTPS development certificate and `ASPNETCORE_ENVIRONMENT=Development`. Run `dotnet run --project src/Alpha6Ops.Server --no-build --urls https://localhost:7246`. Without configured Auth0 and PostgreSQL, public pages render but account/login/API routes return 503. There is no fake login or production bypass. Outside Development, missing identity/database or encrypted persistent key-ring configuration prevents startup.

The key-ring certificate must remain available to every instance. Back up the key ring and certificate separately with restricted access; do not delete old decryption certificates during rotation. This implementation uses a filesystem key ring encrypted with a mounted certificate. Native Azure Blob/Key Vault data-protection integration is a future deployment improvement, not implemented here. Key Vault can supply the mounted secret through the hosting environment. Restrict file permissions to the server identity.

Use HTTPS end to end. No untrusted forwarded headers are accepted. If an Azure proxy terminates TLS, configure explicitly trusted proxy/network forwarding before launch; do not globally enable forwarded-header trust. Keep access/request-body logging disabled for authentication, account, and API routes, including edge telemetry. Do not collect cookies, Authorization headers, claims, invitation codes, or request/response bodies. Built-in application logging defaults to Warning.

## Auth0 setup

Create separate tenant/app configurations for development, staging, and production:

1. Create an Auth0 API using RS256, the configured audience, and a short access-token lifetime (10 minutes recommended).
2. Create a **Regular Web Application** for the portal. Allow the exact callback `https://HOST/signin-oidc` and exact logout callback `https://HOST/signout-callback-oidc`. Local equivalents use `https://localhost:7246`. Use OIDC-conformant logout and configure the end-session endpoint advertised in discovery. Website logout is an antiforgery-protected POST and ends both the encrypted cookie session and Auth0 session; Google/Microsoft upstream sessions remain independent.
3. Create a separate **Native Application** for the desktop, without a client secret. Allow the exact loopback redirect `http://127.0.0.1:42879/callback/`. Do not use wildcard callbacks. Enable Authorization Code and Refresh Token grants, offline access for the API, refresh-token rotation/reuse detection, and an absolute refresh lifetime consistent with product policy.
4. Configure Universal Login branding and the intended database, Microsoft, and Google connections. Enable email verification, breached-password protection, brute-force protection, passkeys with user verification, TOTP, recovery codes, and the chosen WebAuthn factors. Availability depends on the tenant plan/settings. Auth0 owns password hashes, security-factor secrets, recovery codes, factor removal, and recovery; none is stored by Alpha 6.
5. Enable **Customize MFA Factors using Actions** and use **Post Login v3**. Deploy [01-security-challenge.js](auth0/01-security-challenge.js), followed by [02-account-claims.js](auth0/02-account-claims.js). Match allowed factors in the first Action to the factors enabled in your tenant.
6. Test a new account, verified email, first MFA enrollment, repeat MFA challenge, passkey login, social login, refresh, password recovery, factor recovery, and logout against a real tenant before public use. Credentials/tenant identifiers have not been supplied to this checkout, so real-provider flows have not been verified.

The first Action requests a real challenge/enrollment when the client asks for the multi-factor ACR policy. The second Action inspects Auth0's completed authentication methods. `mfa_at` uses the original method timestamp, **never token issue time or refresh time**. Only explicit `mfa` or `passkey` methods are accepted; generic `pwdless`, federation, email OTP, enrolled-factor lists, user metadata, and client parameters do not prove MFA. If a tenant does not expose the passkey method with user verification, keep passkey as login only and require a separate explicit MFA challenge until that tenant's behavior is verified.

Required signed claims in **both ID tokens and access tokens**:

| Claim | Type / meaning |
| --- | --- |
| `iss`, `sub` | Standard exact issuer and immutable Auth0 subject |
| `https://alpha6ops.com/claims/email` | Provider-asserted email string |
| `https://alpha6ops.com/claims/email_verified` | Boolean provider verification status |
| `https://alpha6ops.com/claims/display_name` | Provider profile display name |
| `https://alpha6ops.com/claims/mfa` | Boolean completed MFA/passkey proof |
| `https://alpha6ops.com/claims/mfa_at` | Unix seconds of completed proof, required only when `mfa=true` |

The API rejects missing/malformed claims; future proof timestamps beyond 30 seconds are rejected. AccountsService enforces its current MFA freshness limit for privileged operations. Users may use Personal Pilot without MFA; airline creation and administration require verification. `/auth/step-up` requests a fresh sign-in/check. Security-factor inventory/removal and email-delivery integrations are not implemented in this portal; do not present them as available self-service functions.

## Routes and invitation safety

Public routes: `/`, `/Download`, `/Support`. Account portal: `/Account`, `/Account/Create`, `/Account/Join`, `/Account/Airline/{id}`. Every state-changing browser form uses Razor antiforgery protection.

The `/api/v1` group accepts only RS256 JWT bearer access tokens for the configured issuer and audience. Website cookies cannot authorize desktop API requests. Membership and subscription authorization comes from PostgreSQL on every operation. Errors use problem details with stable `code` values. Cross-tenant requests are denied.

API routes include `GET /me/bootstrap`, `PUT /me/workspace`, `POST /virtual-airlines`, airline detail/member/invitation routes, member role updates, invitation revocation, and ownership transfer.

**Invitation acceptance uses `POST /api/v1/invitations/accept` with JSON `{ "token": "..." }`.** This deliberately corrects the original token-in-URL plan: invitation secrets must not appear in URL paths, query strings, access logs, browser history, or referrers. The portal shows the issued code once in the response body, never TempData/cookies. The administrator shares the `/Account/Join` address plus code, and the recipient pastes it into a POST form. Invitation email delivery is not connected. Resending means revoking the old invitation and issuing another; the raw previous code cannot be recovered.

No paid billing provider, payment collection, cloud flight synchronization, support impersonation, or platform-admin console is wired. Subscription/founder/role data and capability checks exist in the Accounts layer. The download button appears only when URL, version, and checksum are configured; the build does not assert that arbitrary configured installers are signed. Verify Authenticode publisher/timestamp and checksum as part of publishing. Public release additionally requires operator-approved privacy/terms, support contact, backups, monitoring, and identity-recovery runbooks.

## Integration checks

```powershell
$env:ALPHA6_SERVER_TEST_DATABASE = 'Host=127.0.0.1;Port=5432;Username=...;Password=...;Database=alpha6_server_test'
dotnet run --project tests/Alpha6Ops.Server.Tests --no-build
node tests/Alpha6Ops.Server.Tests/auth0-actions.test.cjs
```

Use a dedicated disposable PostgreSQL database. The executable applies migrations and creates uniquely named test accounts/airlines. It uses an in-process test server and a test-only RSA signer and does not contact Auth0. Checks include configuration failures, trusted claims, bad JWT issuer/audience/signature/expiry, cookie/API separation, CSRF, real database membership isolation, and invitation acceptance without secrets in URLs. These are not substitutes for the real-provider acceptance tests above.

References: [Auth0 MFA Actions](https://auth0.com/docs/secure/multi-factor-authentication/customize-mfa/customize-mfa-selection-universal-login), [Auth0 completed MFA methods](https://support.auth0.com/center/s/article/using-actions-mfa-authentication-method-is-missing-on-first-login), [Auth0 passkey method](https://support.auth0.com/center/s/article/detecting-passkey-usage-in-auth0-post-login-actions), [native application security](https://datatracker.ietf.org/doc/html/rfc8252).
