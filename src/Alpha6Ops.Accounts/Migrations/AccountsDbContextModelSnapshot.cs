using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Alpha6Ops.Accounts.Migrations;

[DbContext(typeof(AccountsDbContext))]
public sealed class AccountsDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder) => InitialAccountsModel.Build(modelBuilder);
}

// Frozen initial EF model. The migration's PostgreSQL constraints and triggers are maintained in SQL.
// Future migrations must replace the snapshot rather than modifying this initial model.
internal static class InitialAccountsModel
{
    internal static void Build(ModelBuilder model)
    {
        model.HasAnnotation("ProductVersion", "10.0.4").HasAnnotation("Relational:MaxIdentifierLength", 63);
        NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(model);
        model.Entity("Alpha6Ops.Accounts.UserAccount", b =>
        {
            b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
            b.Property<DateTimeOffset>("CreatedAt").HasColumnType("timestamp with time zone");
            b.Property<string>("DisplayName").IsRequired().HasMaxLength(150).HasColumnType("character varying(150)");
            b.Property<string>("Email").IsRequired().HasMaxLength(254).HasColumnType("character varying(254)");
            b.Property<bool>("EmailVerified").HasColumnType("boolean");
            b.Property<string>("Issuer").IsRequired().HasMaxLength(255).HasColumnType("character varying(255)");
            b.Property<Guid?>("LastAirlineId").HasColumnType("uuid");
            b.Property<int>("Plan").HasColumnType("integer");
            b.Property<int>("Status").HasColumnType("integer");
            b.Property<string>("Subject").IsRequired().HasMaxLength(255).HasColumnType("character varying(255)");
            b.Property<DateTimeOffset?>("SubscriptionExpiresAt").HasColumnType("timestamp with time zone");
            b.Property<string>("SubscriptionStatus").IsRequired().HasMaxLength(30).HasColumnType("character varying(30)");
            b.HasKey("Id"); b.HasIndex("Issuer", "Subject").IsUnique(); b.ToTable("user_account");
        });
        model.Entity("Alpha6Ops.Accounts.VirtualAirline", b =>
        {
            b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
            b.Property<string>("Callsign").IsRequired().HasMaxLength(20).HasColumnType("character varying(20)");
            b.Property<DateTimeOffset>("CreatedAt").HasColumnType("timestamp with time zone");
            b.Property<Guid>("FounderUserId").HasColumnType("uuid");
            b.Property<string>("Name").IsRequired().HasMaxLength(100).HasColumnType("character varying(100)");
            b.Property<Guid>("OwnerUserId").HasColumnType("uuid");
            b.Property<int>("Plan").HasColumnType("integer");
            b.Property<string>("Slug").IsRequired().HasMaxLength(64).HasColumnType("character varying(64)");
            b.Property<string>("Status").IsRequired().HasMaxLength(30).HasColumnType("character varying(30)");
            b.Property<DateTimeOffset?>("SubscriptionExpiresAt").HasColumnType("timestamp with time zone");
            b.Property<string>("SubscriptionStatus").IsRequired().HasMaxLength(30).HasColumnType("character varying(30)");
            b.HasKey("Id"); b.HasIndex("FounderUserId"); b.HasIndex("OwnerUserId"); b.HasIndex("Slug").IsUnique();
            b.ToTable("virtual_airline");
        });
        model.Entity("Alpha6Ops.Accounts.Membership", b =>
        {
            b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
            b.Property<Guid>("AirlineId").HasColumnType("uuid");
            b.Property<DateTimeOffset>("JoinedAt").HasColumnType("timestamp with time zone");
            b.Property<int>("Status").HasColumnType("integer");
            b.Property<Guid>("UserId").HasColumnType("uuid");
            b.HasKey("Id"); b.HasAlternateKey("Id", "AirlineId"); b.HasIndex("AirlineId", "UserId").IsUnique(); b.HasIndex("UserId");
            b.ToTable("membership");
        });
        model.Entity("Alpha6Ops.Accounts.MembershipRole", b =>
        {
            b.Property<Guid>("AirlineId").HasColumnType("uuid");
            b.Property<Guid>("MembershipId").HasColumnType("uuid");
            b.Property<int>("Role").HasColumnType("integer");
            b.HasKey("MembershipId", "AirlineId", "Role"); b.ToTable("membership_role");
        });
        model.Entity("Alpha6Ops.Accounts.Invitation", b =>
        {
            b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
            b.Property<DateTimeOffset?>("AcceptedAt").HasColumnType("timestamp with time zone");
            b.Property<Guid?>("AcceptedByUserId").HasColumnType("uuid");
            b.Property<Guid>("AirlineId").HasColumnType("uuid");
            b.Property<DateTimeOffset>("CreatedAt").HasColumnType("timestamp with time zone");
            b.Property<string>("Email").IsRequired().HasMaxLength(254).HasColumnType("character varying(254)");
            b.Property<DateTimeOffset>("ExpiresAt").HasColumnType("timestamp with time zone");
            b.Property<Guid>("IssuedByUserId").HasColumnType("uuid");
            b.Property<int[]>("Roles").IsRequired().HasColumnType("integer[]");
            b.Property<string>("Status").IsRequired().HasMaxLength(30).HasColumnType("character varying(30)");
            b.Property<string>("TokenHash").IsRequired().HasMaxLength(64).HasColumnType("character varying(64)");
            b.HasKey("Id"); b.HasIndex("AcceptedByUserId"); b.HasIndex("AirlineId", "Email"); b.HasIndex("IssuedByUserId"); b.HasIndex("TokenHash").IsUnique();
            b.ToTable("invitation");
        });
        model.Entity("Alpha6Ops.Accounts.AuditEvent", b =>
        {
            b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
            b.Property<string>("Action").IsRequired().HasMaxLength(80).HasColumnType("character varying(80)");
            b.Property<Guid>("ActorUserId").HasColumnType("uuid");
            b.Property<Guid?>("AirlineId").HasColumnType("uuid");
            b.Property<DateTimeOffset>("CreatedAt").HasColumnType("timestamp with time zone");
            b.Property<string>("Details").IsRequired().HasMaxLength(2000).HasColumnType("character varying(2000)");
            b.Property<Guid>("TargetId").HasColumnType("uuid");
            b.HasKey("Id"); b.HasIndex("ActorUserId"); b.HasIndex("AirlineId", "CreatedAt"); b.ToTable("audit_event");
        });
        model.Entity("Alpha6Ops.Accounts.VirtualAirline", b =>
        {
            b.HasOne("Alpha6Ops.Accounts.UserAccount", null).WithMany().HasForeignKey("FounderUserId").OnDelete(DeleteBehavior.Restrict).IsRequired();
            b.HasOne("Alpha6Ops.Accounts.UserAccount", null).WithMany().HasForeignKey("OwnerUserId").OnDelete(DeleteBehavior.Restrict).IsRequired();
        });
        model.Entity("Alpha6Ops.Accounts.Membership", b =>
        {
            b.HasOne("Alpha6Ops.Accounts.VirtualAirline", null).WithMany().HasForeignKey("AirlineId").OnDelete(DeleteBehavior.Restrict).IsRequired();
            b.HasOne("Alpha6Ops.Accounts.UserAccount", null).WithMany().HasForeignKey("UserId").OnDelete(DeleteBehavior.Restrict).IsRequired();
        });
        model.Entity("Alpha6Ops.Accounts.MembershipRole", b =>
            b.HasOne("Alpha6Ops.Accounts.Membership", null).WithMany("Roles").HasForeignKey("MembershipId", "AirlineId")
                .HasPrincipalKey("Id", "AirlineId").OnDelete(DeleteBehavior.Cascade).IsRequired());
        model.Entity("Alpha6Ops.Accounts.Invitation", b =>
        {
            b.HasOne("Alpha6Ops.Accounts.UserAccount", null).WithMany().HasForeignKey("AcceptedByUserId").OnDelete(DeleteBehavior.Restrict);
            b.HasOne("Alpha6Ops.Accounts.UserAccount", null).WithMany().HasForeignKey("IssuedByUserId").OnDelete(DeleteBehavior.Restrict).IsRequired();
            b.HasOne("Alpha6Ops.Accounts.VirtualAirline", null).WithMany().HasForeignKey("AirlineId").OnDelete(DeleteBehavior.Restrict).IsRequired();
        });
        model.Entity("Alpha6Ops.Accounts.AuditEvent", b =>
        {
            b.HasOne("Alpha6Ops.Accounts.UserAccount", null).WithMany().HasForeignKey("ActorUserId").OnDelete(DeleteBehavior.Restrict).IsRequired();
            b.HasOne("Alpha6Ops.Accounts.VirtualAirline", null).WithMany().HasForeignKey("AirlineId").OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity("Alpha6Ops.Accounts.Membership", b => b.Navigation("Roles"));
    }
}
