using Microsoft.EntityFrameworkCore;
using WeightVerificationQR.Core.Models;

namespace WeightVerificationQR.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Product> Products => Set<Product>();
    public DbSet<WeighRecord> WeighRecords => Set<WeighRecord>();
    public DbSet<User> Users => Set<User>();

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
        });

        modelBuilder.Entity<User>(e =>
        {
            e.HasIndex(u => u.Username).IsUnique();
        });

        // Seed the two products defined in the spec.
        modelBuilder.Entity<Product>().HasData(
            new Product
            {
                Id = 1,
                ProductName = "I Kit 12 mm & 6 mm EPE",
                Quantity = "100 Nos",
                MinWeightKg = 1.000m,
                MaxWeightKg = 1.051m,
                CodePrefix = "KIT",
                IsActive = true,
                CreatedAt = new DateTime(2026, 1, 1)
            },
            new Product
            {
                Id = 2,
                ProductName = "12.7 mm & 6.35 mm EPE Gray",
                Quantity = "100 Nos",
                MinWeightKg = 1.050m,
                MaxWeightKg = 1.080m,
                CodePrefix = "KIT",
                IsActive = true,
                CreatedAt = new DateTime(2026, 1, 1)
            }
        );

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
