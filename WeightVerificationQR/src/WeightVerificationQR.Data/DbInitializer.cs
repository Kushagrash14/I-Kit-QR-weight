using Microsoft.EntityFrameworkCore;
using WeightVerificationQR.Core.Interfaces;
using WeightVerificationQR.Core.Models;

namespace WeightVerificationQR.Data;

public static class DbInitializer
{
    /// <summary>
    /// Creates the database schema directly from the current EF model (no migration files
    /// required) and, on a fresh database, replaces the placeholder admin password hash
    /// with a real PBKDF2 hash for the default password "Admin@123".
    ///
    /// This uses EnsureCreatedAsync rather than MigrateAsync/dotnet-ef migrations because:
    ///  - this is a single-tenant desktop app (one SQLite file per install), so there is no
    ///    need to version/roll forward schema changes across many independently-deployed databases
    ///  - it works without the `dotnet-ef` tool being installed, which keeps first-run friction low
    ///
    /// If you outgrow this (e.g. move to a shared SQL Server instance serving multiple lines/
    /// plants and need controlled schema upgrades), switch to EF Core Migrations:
    ///   dotnet tool install --global dotnet-ef
    ///   cd src/WeightVerificationQR.Data
    ///   dotnet ef migrations add InitialCreate --startup-project ../WeightVerificationQR.App
    /// then replace EnsureCreatedAsync() below with context.Database.MigrateAsync().
    /// </summary>
    public static async Task InitializeAsync(AppDbContext context, IPasswordHasher hasher)
    {
        await context.Database.EnsureCreatedAsync();

        var admin = await context.Users.FirstOrDefaultAsync(u => u.Username == "admin");
        if (admin is not null && admin.PasswordHash == "REPLACE_ON_FIRST_RUN")
        {
            var (hash, salt) = hasher.HashPassword("Admin@123");
            admin.PasswordHash = hash;
            admin.PasswordSalt = salt;
            await context.SaveChangesAsync();
        }

        // Ensure a SuperAdmin account exists (created programmatically, not via HasData,
        // so it also appears on databases created before the SuperAdmin role existed).
        var superAdmin = await context.Users.FirstOrDefaultAsync(u => u.Username == "superadmin");
        if (superAdmin is null)
        {
            var (hash, salt) = hasher.HashPassword("Super@123");
            context.Users.Add(new User
            {
                FullName = "Super Administrator",
                Username = "superadmin",
                PasswordHash = hash,
                PasswordSalt = salt,
                Role = UserRole.SuperAdmin,
                IsActive = true,
                CreatedAt = DateTime.Now
            });
            await context.SaveChangesAsync();
        }
    }
}
