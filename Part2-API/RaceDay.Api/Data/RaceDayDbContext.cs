using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Models;

namespace RaceDay.Api.Data;

public class RaceDayDbContext : DbContext
{
    public RaceDayDbContext(DbContextOptions<RaceDayDbContext> options) : base(options) { }

    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<RaceEvent> Events => Set<RaceEvent>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Enrolment> Enrolments => Set<Enrolment>();
    public DbSet<Result> Results => Set<Result>();
    public DbSet<Payment> Payments => Set<Payment>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        // Mirrors the Part 1 SQL script: same tables, keys, constraints and defaults.
        b.Entity<AppUser>(e =>
        {
            e.ToTable("Users", t => t.HasCheckConstraint("CK_Users_Role", "[Role] IN ('Organiser','Participant','Admin')"));
            e.HasKey(x => x.UserId);
            e.Property(x => x.FullName).HasMaxLength(100).IsRequired();
            e.Property(x => x.Email).HasMaxLength(150).IsRequired();
            e.HasIndex(x => x.Email).IsUnique();
            e.Property(x => x.PasswordHash).HasMaxLength(256).IsRequired();
            e.Property(x => x.Role).HasConversion<string>().IsUnicode(false).HasMaxLength(20).IsRequired();
            e.Property(x => x.PhoneNumber).HasMaxLength(20);
            e.Property(x => x.ProfilePictureBlobName).HasMaxLength(200);
            e.Property(x => x.CreatedAt).HasDefaultValueSql("GETDATE()");
        });

        b.Entity<RaceEvent>(e =>
        {
            e.ToTable("Events", t =>
            {
                t.HasCheckConstraint("CK_Events_Status", "[Status] IN ('Planned','Open','Closed','Completed','Cancelled')");
                t.HasCheckConstraint("CK_Events_EventType", "[EventType] IN ('Run','Walk','Cycle')");
            });
            e.HasKey(x => x.EventId);
            e.Property(x => x.Name).HasMaxLength(150).IsRequired();
            e.Property(x => x.Description).HasMaxLength(1000);
            e.Property(x => x.BannerBlobName).HasMaxLength(200);
            e.Property(x => x.Location).HasMaxLength(200).IsRequired();
            e.Property(x => x.DistanceKm).HasPrecision(6, 2);
            e.Property(x => x.EventType).HasConversion<string>().IsUnicode(false).HasMaxLength(10).IsRequired();
            e.Property(x => x.Status).IsUnicode(false).HasMaxLength(20).IsRequired().HasDefaultValue("Planned");
            e.Property(x => x.CreatedAt).HasDefaultValueSql("GETDATE()");
            e.HasOne(x => x.Organiser).WithMany(u => u.OrganisedEvents)
                .HasForeignKey(x => x.OrganiserId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Category>(e =>
        {
            e.ToTable("Categories", t =>
            {
                t.HasCheckConstraint("CK_Categories_Distance", "[DistanceKm] > 0");
                t.HasCheckConstraint("CK_Categories_MaxParticipants", "[MaxParticipants] > 0");
            });
            e.HasKey(x => x.CategoryId);
            e.Property(x => x.Name).HasMaxLength(100).IsRequired();
            e.Property(x => x.DistanceKm).HasPrecision(6, 2);
            e.Property(x => x.MaxParticipants).HasDefaultValue(100);
            e.Property(x => x.Fee).HasPrecision(10, 2).HasDefaultValue(0m);
            e.HasOne(x => x.Event).WithMany(ev => ev.Categories)
                .HasForeignKey(x => x.EventId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Enrolment>(e =>
        {
            e.ToTable("Enrolments", t => t.HasCheckConstraint("CK_Enrolments_Status", "[Status] IN ('Pending','Confirmed','Cancelled')"));
            e.HasKey(x => x.EnrolmentId);
            e.Property(x => x.EnrolmentDate).HasDefaultValueSql("GETDATE()");
            e.Property(x => x.Status).IsUnicode(false).HasMaxLength(20).IsRequired().HasDefaultValue("Pending");
            e.HasIndex(x => new { x.ParticipantId, x.CategoryId }).IsUnique().HasDatabaseName("UQ_Enrolments_Participant_Category");
            e.HasOne(x => x.Participant).WithMany(u => u.Enrolments)
                .HasForeignKey(x => x.ParticipantId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Category).WithMany(c => c.Enrolments)
                .HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Result>(e =>
        {
            e.ToTable("Results", t => t.HasCheckConstraint("CK_Results_Status", "[Status] IN ('Not Started','Finished','DNF','DQ')"));
            e.HasKey(x => x.ResultId);
            e.Property(x => x.Status).IsUnicode(false).HasMaxLength(20).IsRequired().HasDefaultValue("Not Started");
            e.HasIndex(x => x.EnrolmentId).IsUnique();
            e.HasOne(x => x.Enrolment).WithOne(en => en.Result)
                .HasForeignKey<Result>(x => x.EnrolmentId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Payment>(e =>
        {
            e.ToTable("Payments", t => t.HasCheckConstraint("CK_Payments_Status", "[PaymentStatus] IN ('Unpaid','Paid','Refunded')"));
            e.HasKey(x => x.PaymentId);
            e.Property(x => x.Amount).HasPrecision(10, 2);
            e.Property(x => x.PaymentStatus).IsUnicode(false).HasMaxLength(20).IsRequired().HasDefaultValue("Unpaid");
            e.HasIndex(x => x.EnrolmentId).IsUnique();
            e.HasOne(x => x.Enrolment).WithOne(en => en.Payment)
                .HasForeignKey<Payment>(x => x.EnrolmentId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
