using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Alpha6Ops.Accounts.Migrations;

[DbContext(typeof(AccountsDbContext))]
[Migration("202609150001_InitialAccounts")]
public sealed class InitialAccounts : Migration
{
    protected override void BuildTargetModel(Microsoft.EntityFrameworkCore.ModelBuilder modelBuilder) => InitialAccountsModel.Build(modelBuilder);

    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        CREATE TABLE user_account (
            "Id" uuid CONSTRAINT "PK_user_account" PRIMARY KEY, "Issuer" varchar(255) NOT NULL, "Subject" varchar(255) NOT NULL,
            "DisplayName" varchar(150) NOT NULL, "Email" varchar(254) NOT NULL, "EmailVerified" boolean NOT NULL,
            "Status" integer NOT NULL CHECK ("Status" IN (0,1)), "Plan" integer NOT NULL CHECK ("Plan" IN (0,1)),
            "SubscriptionStatus" varchar(30) NOT NULL, "SubscriptionExpiresAt" timestamptz,
            "LastAirlineId" uuid, "CreatedAt" timestamptz NOT NULL);
        CREATE UNIQUE INDEX "IX_user_account_Issuer_Subject" ON user_account ("Issuer", "Subject");

        CREATE TABLE virtual_airline (
            "Id" uuid CONSTRAINT "PK_virtual_airline" PRIMARY KEY, "Slug" varchar(64) NOT NULL, "Name" varchar(100) NOT NULL, "Callsign" varchar(20) NOT NULL,
            "Status" varchar(30) NOT NULL, "FounderUserId" uuid NOT NULL CONSTRAINT "FK_virtual_airline_user_account_FounderUserId" REFERENCES user_account ("Id") ON DELETE RESTRICT,
            "OwnerUserId" uuid NOT NULL CONSTRAINT "FK_virtual_airline_user_account_OwnerUserId" REFERENCES user_account ("Id") ON DELETE RESTRICT,
            "Plan" integer NOT NULL CHECK ("Plan" IN (0,1)), "SubscriptionStatus" varchar(30) NOT NULL,
            "SubscriptionExpiresAt" timestamptz, "CreatedAt" timestamptz NOT NULL);
        CREATE UNIQUE INDEX "IX_virtual_airline_Slug" ON virtual_airline ("Slug");
        CREATE INDEX "IX_virtual_airline_FounderUserId" ON virtual_airline ("FounderUserId");
        CREATE INDEX "IX_virtual_airline_OwnerUserId" ON virtual_airline ("OwnerUserId");

        CREATE TABLE membership (
            "Id" uuid CONSTRAINT "PK_membership" PRIMARY KEY, "AirlineId" uuid NOT NULL CONSTRAINT "FK_membership_virtual_airline_AirlineId" REFERENCES virtual_airline ("Id") ON DELETE RESTRICT,
            "UserId" uuid NOT NULL CONSTRAINT "FK_membership_user_account_UserId" REFERENCES user_account ("Id") ON DELETE RESTRICT,
            "Status" integer NOT NULL CHECK ("Status" IN (0,1,2)), "JoinedAt" timestamptz NOT NULL,
            CONSTRAINT "AK_membership_Id_AirlineId" UNIQUE ("Id", "AirlineId"));
        CREATE UNIQUE INDEX "IX_membership_AirlineId_UserId" ON membership ("AirlineId", "UserId");
        CREATE INDEX "IX_membership_UserId" ON membership ("UserId");

        CREATE TABLE membership_role (
            "AirlineId" uuid NOT NULL, "MembershipId" uuid NOT NULL, "Role" integer NOT NULL CHECK ("Role" IN (0,1,2)),
            CONSTRAINT "PK_membership_role" PRIMARY KEY ("MembershipId", "AirlineId", "Role"),
            CONSTRAINT "FK_membership_role_membership_MembershipId_AirlineId" FOREIGN KEY ("MembershipId", "AirlineId") REFERENCES membership ("Id", "AirlineId") ON DELETE CASCADE);

        CREATE TABLE invitation (
            "Id" uuid CONSTRAINT "PK_invitation" PRIMARY KEY, "AirlineId" uuid NOT NULL CONSTRAINT "FK_invitation_virtual_airline_AirlineId" REFERENCES virtual_airline ("Id") ON DELETE RESTRICT,
            "IssuedByUserId" uuid NOT NULL CONSTRAINT "FK_invitation_user_account_IssuedByUserId" REFERENCES user_account ("Id") ON DELETE RESTRICT,
            "Email" varchar(254) NOT NULL, "Roles" integer[] NOT NULL CHECK (cardinality("Roles") BETWEEN 1 AND 3 AND "Roles" <@ ARRAY[0,1,2]),
            "TokenHash" varchar(64) NOT NULL, "ExpiresAt" timestamptz NOT NULL,
            "Status" varchar(30) NOT NULL CHECK ("Status" IN ('pending','accepted','revoked')),
            "AcceptedByUserId" uuid CONSTRAINT "FK_invitation_user_account_AcceptedByUserId" REFERENCES user_account ("Id") ON DELETE RESTRICT, "AcceptedAt" timestamptz,
            "CreatedAt" timestamptz NOT NULL);
        CREATE UNIQUE INDEX "IX_invitation_TokenHash" ON invitation ("TokenHash");
        CREATE INDEX "IX_invitation_AirlineId_Email" ON invitation ("AirlineId", "Email");
        CREATE INDEX "IX_invitation_IssuedByUserId" ON invitation ("IssuedByUserId");
        CREATE INDEX "IX_invitation_AcceptedByUserId" ON invitation ("AcceptedByUserId");

        CREATE TABLE audit_event (
            "Id" uuid CONSTRAINT "PK_audit_event" PRIMARY KEY, "ActorUserId" uuid NOT NULL CONSTRAINT "FK_audit_event_user_account_ActorUserId" REFERENCES user_account ("Id") ON DELETE RESTRICT,
            "AirlineId" uuid CONSTRAINT "FK_audit_event_virtual_airline_AirlineId" REFERENCES virtual_airline ("Id") ON DELETE RESTRICT,
            "Action" varchar(80) NOT NULL, "TargetId" uuid NOT NULL, "Details" varchar(2000) NOT NULL,
            "CreatedAt" timestamptz NOT NULL);
        CREATE INDEX "IX_audit_event_ActorUserId" ON audit_event ("ActorUserId");
        CREATE INDEX "IX_audit_event_AirlineId_CreatedAt" ON audit_event ("AirlineId", "CreatedAt");

        -- Deferred so creation can insert the airline and its initial membership in one transaction.
        ALTER TABLE virtual_airline ADD CONSTRAINT airline_owner_membership
            FOREIGN KEY ("Id", "OwnerUserId") REFERENCES membership ("AirlineId", "UserId") DEFERRABLE INITIALLY DEFERRED;

        CREATE FUNCTION alpha6_check_owner() RETURNS trigger LANGUAGE plpgsql AS $$
        BEGIN
            IF EXISTS (SELECT 1 FROM virtual_airline a
                LEFT JOIN membership m ON m."AirlineId" = a."Id" AND m."UserId" = a."OwnerUserId"
                WHERE a."Status" = 'active' AND (m."Id" IS NULL OR m."Status" <> 0)) THEN
                RAISE EXCEPTION 'Every active airline requires one active owner membership' USING ERRCODE = '23514';
            END IF;
            RETURN NULL;
        END $$;
        CREATE CONSTRAINT TRIGGER airline_owner_active AFTER INSERT OR UPDATE ON virtual_airline
            DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION alpha6_check_owner();
        CREATE CONSTRAINT TRIGGER membership_owner_active AFTER INSERT OR UPDATE OR DELETE ON membership
            DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION alpha6_check_owner();

        CREATE FUNCTION alpha6_preserve_founder() RETURNS trigger LANGUAGE plpgsql AS $$
        BEGIN
            IF NEW."FounderUserId" <> OLD."FounderUserId" THEN
                RAISE EXCEPTION 'The historical airline founder cannot change' USING ERRCODE = '23514';
            END IF;
            RETURN NEW;
        END $$;
        CREATE TRIGGER airline_founder_immutable BEFORE UPDATE ON virtual_airline
            FOR EACH ROW EXECUTE FUNCTION alpha6_preserve_founder();
        """);

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        DROP TRIGGER airline_founder_immutable ON virtual_airline;
        DROP FUNCTION alpha6_preserve_founder();
        DROP TRIGGER airline_owner_active ON virtual_airline;
        DROP TRIGGER membership_owner_active ON membership;
        DROP FUNCTION alpha6_check_owner();
        ALTER TABLE virtual_airline DROP CONSTRAINT airline_owner_membership;
        DROP TABLE audit_event, invitation, membership_role, membership, virtual_airline, user_account;
        """);
}
