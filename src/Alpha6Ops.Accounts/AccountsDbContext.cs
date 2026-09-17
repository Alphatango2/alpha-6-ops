using Microsoft.EntityFrameworkCore;

namespace Alpha6Ops.Accounts;

public sealed class AccountsDbContext(DbContextOptions<AccountsDbContext> options) : DbContext(options)
{
    public DbSet<UserAccount> Users => Set<UserAccount>();
    public DbSet<VirtualAirline> Airlines => Set<VirtualAirline>();
    public DbSet<Membership> Memberships => Set<Membership>();
    public DbSet<MembershipRole> MembershipRoles => Set<MembershipRole>();
    public DbSet<Invitation> Invitations => Set<Invitation>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        var user = model.Entity<UserAccount>();
        user.ToTable("user_account"); user.HasKey(x => x.Id);
        user.HasIndex(x => new { x.Issuer, x.Subject }).IsUnique();
        user.Property(x => x.Issuer).HasMaxLength(255); user.Property(x => x.Subject).HasMaxLength(255);
        user.Property(x => x.DisplayName).HasMaxLength(150); user.Property(x => x.Email).HasMaxLength(254);
        user.Property(x => x.SubscriptionStatus).HasMaxLength(30);

        var airline = model.Entity<VirtualAirline>();
        airline.ToTable("virtual_airline"); airline.HasKey(x => x.Id); airline.HasIndex(x => x.Slug).IsUnique();
        airline.Property(x => x.Slug).HasMaxLength(64); airline.Property(x => x.Name).HasMaxLength(100);
        airline.Property(x => x.Callsign).HasMaxLength(20); airline.Property(x => x.Status).HasMaxLength(30);
        airline.Property(x => x.SubscriptionStatus).HasMaxLength(30);
        airline.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.FounderUserId).OnDelete(DeleteBehavior.Restrict);
        airline.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.OwnerUserId).OnDelete(DeleteBehavior.Restrict);

        var member = model.Entity<Membership>();
        member.ToTable("membership"); member.HasKey(x => x.Id);
        member.HasAlternateKey(x => new { x.Id, x.AirlineId });
        member.HasIndex(x => new { x.AirlineId, x.UserId }).IsUnique();
        member.HasOne<VirtualAirline>().WithMany().HasForeignKey(x => x.AirlineId).OnDelete(DeleteBehavior.Restrict);
        member.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        member.HasMany(x => x.Roles).WithOne().HasForeignKey(x => new { x.MembershipId, x.AirlineId })
            .HasPrincipalKey(x => new { x.Id, x.AirlineId }).OnDelete(DeleteBehavior.Cascade);
        var role = model.Entity<MembershipRole>();
        role.ToTable("membership_role"); role.HasKey(x => new { x.MembershipId, x.AirlineId, x.Role });

        var invitation = model.Entity<Invitation>();
        invitation.ToTable("invitation"); invitation.HasKey(x => x.Id);
        invitation.HasIndex(x => x.TokenHash).IsUnique(); invitation.HasIndex(x => new { x.AirlineId, x.Email });
        invitation.Property(x => x.Email).HasMaxLength(254); invitation.Property(x => x.TokenHash).HasMaxLength(64);
        invitation.Property(x => x.Status).HasMaxLength(30);
        invitation.HasOne<VirtualAirline>().WithMany().HasForeignKey(x => x.AirlineId).OnDelete(DeleteBehavior.Restrict);
        invitation.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.IssuedByUserId).OnDelete(DeleteBehavior.Restrict);
        invitation.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.AcceptedByUserId).OnDelete(DeleteBehavior.Restrict);

        var audit = model.Entity<AuditEvent>(); audit.ToTable("audit_event"); audit.HasKey(x => x.Id);
        audit.Property(x => x.Action).HasMaxLength(80); audit.Property(x => x.Details).HasMaxLength(2000);
        audit.HasIndex(x => new { x.AirlineId, x.CreatedAt });
        audit.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.ActorUserId).OnDelete(DeleteBehavior.Restrict);
        audit.HasOne<VirtualAirline>().WithMany().HasForeignKey(x => x.AirlineId).OnDelete(DeleteBehavior.Restrict);
    }
}
