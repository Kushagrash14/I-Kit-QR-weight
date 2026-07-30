using Microsoft.EntityFrameworkCore;
using WeightVerificationQR.Core.Models;

namespace WeightVerificationQR.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Product> Products => Set<Product>();
    public DbSet<WeighRecord> WeighRecords => Set<WeighRecord>();
    public DbSet<User> Users => Set<User>();
    public DbSet<SerialNumberState> SerialNumberStates => Set<SerialNumberState>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Product>(e =>
        {
            e.HasIndex(p => p.ProductName);
        });

        modelBuilder.Entity<WeighRecord>(e =>
        {
            e.HasIndex(r => r.KitNumber).IsUnique();
            e.HasIndex(r => r.RecordDate);
            e.HasIndex(r => r.QrId);
            e.HasIndex(r => r.GlobalRecordId).IsUnique();
            e.HasIndex(r => r.SyncStatus);
        });

        modelBuilder.Entity<SerialNumberState>(e =>
        {
            e.HasData(new SerialNumberState
            {
                Id = 1,
                NextSerial = 0,
                BlockEndSerial = 0,
                EmergencyNextSerial = 0,
                UpdatedAt = new DateTime(2026, 1, 1)
            });
        });

        modelBuilder.Entity<User>(e =>
        {
            e.HasIndex(u => u.Username).IsUnique();
        });

        // Seed a default admin user. Username: admin / Password: Admin@123
        // Hash below is generated with PasswordHasher (PBKDF2, 100k iterations) - see Docs/SEED_CREDENTIALS.md
        modelBuilder.Entity<User>().HasData(
            new User
            {
                Id = 1,
                FullName = "System Administrator",
                Username = "admin",
                PasswordHash = "REPLACE_ON_FIRST_RUN",
                PasswordSalt = "REPLACE_ON_FIRST_RUN",
                Role = UserRole.Admin,
                IsActive = true,
                CreatedAt = new DateTime(2026, 1, 1)
            }
        );
    }
}
