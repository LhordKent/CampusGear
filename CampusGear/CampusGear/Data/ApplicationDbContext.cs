using CampusGear.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace CampusGear.Data;

public sealed class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    public DbSet<BorrowerProfile> BorrowerProfiles => Set<BorrowerProfile>();
    public DbSet<EquipmentCategory> EquipmentCategories => Set<EquipmentCategory>();
    public DbSet<EquipmentItem> EquipmentItems => Set<EquipmentItem>();
    public DbSet<Reservation> Reservations => Set<Reservation>();
    public DbSet<Loan> Loans => Set<Loan>();
    public DbSet<MaintenanceCase> MaintenanceCases => Set<MaintenanceCase>();
    public DbSet<EmailChallenge> EmailChallenges => Set<EmailChallenge>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ApplicationUser>(entity =>
        {
            entity.Property(x => x.FullName).HasMaxLength(160).IsRequired();
            entity.HasIndex(x => x.NormalizedEmail)
                .IsUnique()
                .HasFilter("[NormalizedEmail] IS NOT NULL");
        });

        modelBuilder.Entity<BorrowerProfile>(entity =>
        {
            entity.ToTable("BorrowerProfiles");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.StudentNumber).HasMaxLength(64);
            entity.Property(x => x.Department).HasMaxLength(160);
            entity.Property(x => x.ContactNumber).HasMaxLength(40);
            entity.Property(x => x.EligibilityNotes).HasMaxLength(1000);
            entity.Property(x => x.RowVersion).IsRowVersion();
            entity.HasIndex(x => x.UserId).IsUnique();
            entity.HasIndex(x => x.StudentNumber)
                .IsUnique()
                .HasFilter("[StudentNumber] IS NOT NULL");
            entity.HasOne(x => x.User)
                .WithOne(x => x.BorrowerProfile)
                .HasForeignKey<BorrowerProfile>(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<EquipmentCategory>(entity =>
        {
            entity.ToTable("EquipmentCategories");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(120).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(1000);
            entity.Property(x => x.RowVersion).IsRowVersion();
            entity.HasIndex(x => x.Name).IsUnique();
        });

        modelBuilder.Entity<EquipmentItem>(entity =>
        {
            entity.ToTable("EquipmentItems");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.AssetCode).HasMaxLength(64).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(160).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(2000);
            entity.Property(x => x.SerialNumber).HasMaxLength(120);
            entity.Property(x => x.Location).HasMaxLength(160);
            entity.Property(x => x.Condition).HasConversion<string>().HasMaxLength(32);
            entity.Property(x => x.RowVersion).IsRowVersion();
            entity.HasIndex(x => x.AssetCode).IsUnique();
            entity.HasIndex(x => x.SerialNumber)
                .IsUnique()
                .HasFilter("[SerialNumber] IS NOT NULL");
            entity.HasIndex(x => new { x.CategoryId, x.IsActive, x.IsMaintenanceHold });
            entity.HasOne(x => x.Category)
                .WithMany(x => x.Items)
                .HasForeignKey(x => x.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Reservation>(entity =>
        {
            entity.ToTable("Reservations", table =>
                table.HasCheckConstraint("CK_Reservations_TimeRange", "[StartAtUtc] < [EndAtUtc]"));
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
            entity.Property(x => x.Purpose).HasMaxLength(500).IsRequired();
            entity.Property(x => x.Notes).HasMaxLength(2000);
            entity.Property(x => x.DecisionReason).HasMaxLength(1000);
            entity.Property(x => x.RowVersion).IsRowVersion();
            entity.HasIndex(x => x.RequestId).IsUnique();
            entity.HasIndex(x => new { x.EquipmentItemId, x.Status, x.StartAtUtc, x.EndAtUtc });
            entity.HasIndex(x => new { x.BorrowerProfileId, x.CreatedAtUtc });
            entity.HasIndex(x => new { x.Status, x.HoldExpiresAtUtc });
            entity.HasOne(x => x.BorrowerProfile)
                .WithMany(x => x.Reservations)
                .HasForeignKey(x => x.BorrowerProfileId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.EquipmentItem)
                .WithMany(x => x.Reservations)
                .HasForeignKey(x => x.EquipmentItemId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.DecidedByUser)
                .WithMany()
                .HasForeignKey(x => x.DecidedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Loan>(entity =>
        {
            entity.ToTable("Loans", table =>
            {
                table.HasCheckConstraint("CK_Loans_DueAfterRelease", "[ReleasedAtUtc] < [DueAtUtc]");
                table.HasCheckConstraint("CK_Loans_ReturnAfterRelease",
                    "[ReturnedAtUtc] IS NULL OR [ReturnedAtUtc] >= [ReleasedAtUtc]");
            });
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ConditionAtRelease).HasConversion<string>().HasMaxLength(32);
            entity.Property(x => x.ConditionAtReturn).HasConversion<string>().HasMaxLength(32);
            entity.Property(x => x.ReleaseNotes).HasMaxLength(2000);
            entity.Property(x => x.ReturnNotes).HasMaxLength(2000);
            entity.Property(x => x.RowVersion).IsRowVersion();
            entity.HasIndex(x => x.ReservationId).IsUnique();
            entity.HasIndex(x => new { x.ReturnedAtUtc, x.DueAtUtc });
            entity.HasOne(x => x.Reservation)
                .WithOne(x => x.Loan)
                .HasForeignKey<Loan>(x => x.ReservationId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.ReleasedByUser)
                .WithMany()
                .HasForeignKey(x => x.ReleasedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.ReceivedByUser)
                .WithMany()
                .HasForeignKey(x => x.ReceivedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<MaintenanceCase>(entity =>
        {
            entity.ToTable("MaintenanceCases", table =>
                table.HasCheckConstraint("CK_MaintenanceCases_ClosedAfterOpened",
                    "[ClosedAtUtc] IS NULL OR [ClosedAtUtc] >= [OpenedAtUtc]"));
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
            entity.Property(x => x.Description).HasMaxLength(2000).IsRequired();
            entity.Property(x => x.Resolution).HasMaxLength(2000);
            entity.Property(x => x.RowVersion).IsRowVersion();
            entity.HasIndex(x => new { x.EquipmentItemId, x.Status });
            entity.HasOne(x => x.EquipmentItem)
                .WithMany(x => x.MaintenanceCases)
                .HasForeignKey(x => x.EquipmentItemId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Loan)
                .WithMany(x => x.MaintenanceCases)
                .HasForeignKey(x => x.LoanId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.OpenedByUser)
                .WithMany()
                .HasForeignKey(x => x.OpenedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.ClosedByUser)
                .WithMany()
                .HasForeignKey(x => x.ClosedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<EmailChallenge>(entity =>
        {
            entity.ToTable("EmailChallenges", table =>
                table.HasCheckConstraint("CK_EmailChallenges_Expiry", "[CreatedAtUtc] < [ExpiresAtUtc]"));
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Purpose).HasConversion<string>().HasMaxLength(40);
            entity.Property(x => x.Email).HasMaxLength(256).IsRequired();
            entity.Property(x => x.CodeHash).HasMaxLength(256).IsRequired();
            entity.Property(x => x.RowVersion).IsRowVersion();
            entity.HasIndex(x => new { x.UserId, x.Purpose, x.ConsumedAtUtc, x.ExpiresAtUtc });
            entity.HasOne(x => x.User)
                .WithMany(x => x.EmailChallenges)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AuditEvent>(entity =>
        {
            entity.ToTable("AuditEvents");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Action).HasMaxLength(120).IsRequired();
            entity.Property(x => x.EntityType).HasMaxLength(120).IsRequired();
            entity.Property(x => x.EntityId).HasMaxLength(120).IsRequired();
            entity.Property(x => x.Outcome).HasMaxLength(80).IsRequired();
            entity.Property(x => x.IpAddress).HasMaxLength(64);
            entity.Property(x => x.CorrelationId).HasMaxLength(120);
            entity.HasIndex(x => new { x.EntityType, x.EntityId, x.CreatedAtUtc });
            entity.HasIndex(x => new { x.ActorUserId, x.CreatedAtUtc });
            entity.HasOne(x => x.ActorUser)
                .WithMany()
                .HasForeignKey(x => x.ActorUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
