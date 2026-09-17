# Account and virtual-airline persistence

This library owns the production PostgreSQL account data and authorization rules. It accepts an `ActorIdentity` only after the hosting application has validated the OIDC token, issuer, audience, signature, lifetime, and MFA claims. It never accepts passwords and never connects to Auth0 directly.

## Hosting

After building the cloud solution, set `ConnectionStrings__Accounts` securely to the target database with migration credentials and run:

```powershell
dotnet run --project src/Alpha6Ops.Server --no-build -- --migrate-accounts
```

This explicit command applies pending migrations and exits without starting HTTP listeners or requiring Auth0. Normal server startup does not run migrations.

Register `AccountsDbContext` with `UseNpgsql(connectionString)` and register `AccountsService` as scoped. The host must protect every account endpoint with authentication. The optional `TimeProvider` supports deterministic clock-dependent checks.

Call `Database.MigrateAsync()` from the host's explicit migration command using a database migration identity. Normal web startup should not create databases, mutate schemas, or seed accounts. Runtime database credentials need normal access to account tables and PostgreSQL advisory locks; use a separate migration credential with DDL privileges. Protect audit history by granting the runtime identity SELECT/INSERT but no UPDATE/DELETE on `audit_event`.

The initial migration creates six tables and PostgreSQL constraints/triggers. `AccountsDbContextModelSnapshot` matches the EF model. The frozen `InitialAccountsModel` must remain unchanged; future migrations should replace the snapshot with their new model and maintain the additional SQL checks, deferrable ownership foreign key, and triggers. Do not use `EnsureCreated`: that bypasses the security constraints supplied by the migration.

The migration's `Down` method removes all account tables and data. It is intended only for disposable environments; production recovery must use backups or an independently reviewed forward migration.

## Identity and authorization

- External identity is the exact immutable `(Issuer, Subject)` pair. Matching email addresses never merge users. Display name and verified-email snapshots update from the validated identity.
- Active account status is checked on every request. New accounts receive Personal Free. Platform administrator access is not assigned or inferred by this service.
- Membership queries always include the airline ID and current active membership. Owner is derived exclusively from `VirtualAirline.OwnerUserId`; stored membership roles contain only Pilot, Dispatcher, and Administrator.
- A deferred composite foreign key requires the owner's membership in the same airline. Database triggers prevent an active airline's owner membership from being removed or suspended, and preserve its historical founder. There is exactly one current owner per airline.
- Management operations require an administrator/owner membership, verified email, and MFA confirmed within five minutes. A new administrator invitation also requires recent MFA when accepted. A missing or unknown role never grants access.
- Each active Community airline counts toward the one-owned-Community limit, including a Pro airline whose subscription expired or was canceled. Subscription state never removes membership or ownership. No paid capability beyond the free baseline is enabled yet; billing and the Premium/Pro feature catalog remain a later milestone.

`ActorIdentity.AuthenticatedAt` must be the trusted MFA confirmation timestamp, not an unverified request value or a generic last-login timestamp. The host is responsible for requiring provider-backed step-up authentication and passing the confirmed result.

## Invitations and audit history

Invitations contain 256 bits of cryptographic randomness, expire after seven days, and store only a SHA-256 token hash. Issuing a replacement revokes pending invitations for the same airline/email. Listings never contain raw tokens. The issuer receives the code once for manual sharing alongside the token-free join-page address; this service does not send email or place codes in URLs.

Acceptance requires an exact normalized verified email match. Repeating a successful acceptance is safe and does not restore subsequently removed roles. Suspended/removed memberships cannot be revived by an invitation. Existing active members can receive additive roles from a fresh invitation. Owner and unknown roles are rejected. Ownership transfers preserve the old owner's existing roles and add Administrator access.

Creation, invitations, role changes, and ownership transfers create transactionally committed audit records without raw tokens. The host should record denied-request security events using its correlation ID, without logging request bodies or invitation URLs.

## Concurrency and limits

All service operations acquire one PostgreSQL transaction-scoped advisory lock before provisioning users or reading authorization. This deliberately serializes account requests in the initial milestone and prevents duplicate signups, duplicate Community ownership, invitation double acceptance, and lost authorization updates across processes. Transactions release the lock on commit, rollback, or connection loss. Cancellation flows to all database calls.

This coarse lock limits throughput; before scaling, replace it with consistently ordered per-user/per-airline locks and rerun concurrency tests. All future account mutation services must follow the same locking contract. External administrative database writes bypass service authorization and must remain restricted.

Personal and airline subscription fields currently live on their respective account/airline rows. A future billing integration can split subscription history into dedicated tables without changing the bootstrap contracts. Brand asset storage, platform staff tooling, payments, cloud logbook sync, and membership suspension administration have no production endpoints in this milestone.

## Verification

```powershell
dotnet build tests/Alpha6Ops.Accounts.Tests/Alpha6Ops.Accounts.Tests.csproj
dotnet tests/Alpha6Ops.Accounts.Tests/bin/Debug/net10.0/Alpha6Ops.Accounts.Tests.dll
```

Without `ALPHA6_TEST_DATABASE`, the executable runs pure input/security rules and explicitly reports PostgreSQL integration as skipped. To run integration tests, set that environment variable to the local disposable database named `alpha6_identity_test`. The test guard rejects remote hosts and other database names. Each run creates a unique schema, applies the real migration twice, verifies snapshot parity, and preserves its schema for inspection; it never drops or truncates existing data.

Integration covers issuer/subject identity, concurrent provisioning, Community limits including expired Pro, invitation expiry/revocation/retries, role changes and immediate authorization revocation, same-tenant foreign keys, owner/founder invariants, account/membership/airline suspension, multi-airline switching, and token-free audit records. The separate host and desktop projects must verify OIDC/browser behavior and secure token storage.
