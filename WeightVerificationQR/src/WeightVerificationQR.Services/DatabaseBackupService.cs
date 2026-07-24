using WeightVerificationQR.Core.Interfaces;
using WeightVerificationQR.Core.Models;

namespace WeightVerificationQR.Services;

/// <summary>
/// For SQLite: copies the .db file to the backup folder with a timestamp.
/// For SQL Server deployments, replace the body with a call to BACKUP DATABASE ... TO DISK,
/// or invoke sqlcmd/SMO - the interface and calling code do not need to change.
/// </summary>
public class DatabaseBackupService : IDatabaseBackupService
{
    private readonly DatabaseSettings _settings;

    public DatabaseBackupService(DatabaseSettings settings) => _settings = settings;

    public string DatabaseStatusText { get; private set; } = "Unknown";

    public Task<string> BackupNowAsync()
    {
        try
        {
            Directory.CreateDirectory(_settings.BackupFolderPath);

            // "Data Source=WeightVerificationQR.db" -> extract file path
            var dbPath = _settings.ConnectionString
                .Split(';')
                .Select(p => p.Trim())
                .FirstOrDefault(p => p.StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase))
                ?.Substring("Data Source=".Length) ?? "WeightVerificationQR.db";

            if (!File.Exists(dbPath))
            {
                DatabaseStatusText = "Source database file not found";
                return Task.FromResult(string.Empty);
            }

            var backupFile = Path.Combine(
                _settings.BackupFolderPath,
                $"WeightVerificationQR_{DateTime.Now:yyyyMMdd_HHmmss}.db");

            File.Copy(dbPath, backupFile, overwrite: false);
            DatabaseStatusText = "Connected";
            return Task.FromResult(backupFile);
        }
        catch (Exception ex)
        {
            DatabaseStatusText = $"Backup error: {ex.Message}";
            return Task.FromResult(string.Empty);
        }
    }
}
