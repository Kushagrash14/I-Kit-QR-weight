using Microsoft.EntityFrameworkCore;
using System.Data.Common;
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
        await ApplySqliteCompatibilityUpgradesAsync(context);

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

    private static async Task ApplySqliteCompatibilityUpgradesAsync(AppDbContext context)
    {
        if (!context.Database.IsSqlite())
            return;

        await context.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS "SerialNumberStates" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_SerialNumberStates" PRIMARY KEY,
                "NextSerial" INTEGER NOT NULL DEFAULT 0,
                "BlockEndSerial" INTEGER NOT NULL DEFAULT 0,
                "EmergencyNextSerial" INTEGER NOT NULL DEFAULT 0,
                "UpdatedAt" TEXT NOT NULL
            );
            INSERT OR IGNORE INTO "SerialNumberStates"
                ("Id", "NextSerial", "BlockEndSerial", "EmergencyNextSerial", "UpdatedAt")
            VALUES (1, 0, 0, 0, CURRENT_TIMESTAMP);
            """);

        var additions = new Dictionary<string, string>
        {
            ["GlobalRecordId"] = "TEXT NOT NULL DEFAULT ''",
            ["SiteCode"] = "TEXT NOT NULL DEFAULT ''",
            ["LineCode"] = "TEXT NOT NULL DEFAULT ''",
            ["MachineCode"] = "TEXT NOT NULL DEFAULT ''",
            ["SerialNumber"] = "INTEGER NOT NULL DEFAULT 0",
            ["SyncStatus"] = "INTEGER NOT NULL DEFAULT 0",
            ["SyncAttempts"] = "INTEGER NOT NULL DEFAULT 0",
            ["LastSyncError"] = "TEXT NOT NULL DEFAULT ''",
            ["SyncedAt"] = "TEXT NULL"
        };

        var existingColumns = await GetColumnNamesAsync(context, "WeighRecords");
        foreach (var (name, sqlType) in additions)
        {
            if (existingColumns.Contains(name))
                continue;

            var alterSql = string.Concat(
                "ALTER TABLE \"WeighRecords\" ADD COLUMN \"",
                name,
                "\" ",
                sqlType,
                ";");
            await context.Database.ExecuteSqlRawAsync(alterSql);
        }

        await context.Database.ExecuteSqlRawAsync(
            """
            UPDATE "WeighRecords"
            SET "GlobalRecordId" = lower(hex(randomblob(4))) || '-' ||
                                   lower(hex(randomblob(2))) || '-4' ||
                                   substr(lower(hex(randomblob(2))),2) || '-' ||
                                   substr('89ab',abs(random()) % 4 + 1,1) ||
                                   substr(lower(hex(randomblob(2))),2) || '-' ||
                                   lower(hex(randomblob(6)))
            WHERE "GlobalRecordId" = '';
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_WeighRecords_GlobalRecordId"
                ON "WeighRecords" ("GlobalRecordId");
            CREATE INDEX IF NOT EXISTS "IX_WeighRecords_SyncStatus"
                ON "WeighRecords" ("SyncStatus");
            """);
    }

    private static async Task<HashSet<string>> GetColumnNamesAsync(
        AppDbContext context,
        string tableName)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        DbConnection connection = context.Database.GetDbConnection();
        var shouldClose = connection.State != System.Data.ConnectionState.Open;
        if (shouldClose)
            await connection.OpenAsync();

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = $"PRAGMA table_info(\"{tableName}\");";
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                result.Add(reader.GetString(1));
        }
        finally
        {
            if (shouldClose)
                await connection.CloseAsync();
        }

        return result;
    }
}
