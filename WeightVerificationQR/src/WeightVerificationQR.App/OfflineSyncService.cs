using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WeightVerificationQR.Core.Interfaces;
using WeightVerificationQR.Core.Models;

namespace WeightVerificationQR.App;

public sealed class OfflineSyncService : IOfflineSyncService, IDisposable
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ICentralSyncStore _centralStore;
    private readonly CentralSyncSettings _settings;
    private readonly ILogger<OfflineSyncService> _logger;
    private readonly CancellationTokenSource _shutdown = new();
    private Task? _worker;

    public OfflineSyncService(
        IServiceScopeFactory scopeFactory,
        ICentralSyncStore centralStore,
        CentralSyncSettings settings,
        ILogger<OfflineSyncService> logger)
    {
        _scopeFactory = scopeFactory;
        _centralStore = centralStore;
        _settings = settings;
        _logger = logger;
    }

    public string StatusText { get; private set; } = "Local only";

    public void Start()
    {
        if (_worker is not null || !_centralStore.IsEnabled)
            return;

        _worker = RunAsync(_shutdown.Token);
    }

    public async Task StopAsync()
    {
        _shutdown.Cancel();
        if (_worker is null) return;

        try
        {
            await _worker;
        }
        catch (OperationCanceledException)
        {
        }
    }

    public async Task SyncNowAsync(CancellationToken cancellationToken = default)
    {
        if (!_centralStore.IsEnabled)
        {
            StatusText = "Local only";
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IWeighRecordRepository>();
        var records = await repository.GetPendingSyncAsync(Math.Max(1, _settings.BatchSize));

        foreach (var record in records)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await _centralStore.UpsertWeighRecordAsync(record, cancellationToken);
                await repository.MarkSyncedAsync(record.GlobalRecordId, DateTime.Now);
            }
            catch (Exception ex)
            {
                await repository.MarkSyncFailedAsync(record.GlobalRecordId, ex.Message);
                StatusText = $"Offline - {records.Count} record(s) pending";
                _logger.LogWarning(
                    ex,
                    "Central sync failed for record {GlobalRecordId}.",
                    record.GlobalRecordId);
                return;
            }
        }

        StatusText = records.Count == 0
            ? "Online - synchronized"
            : $"Online - synchronized {records.Count} record(s)";
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _centralStore.InitializeAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            StatusText = "Offline - central database unavailable";
            _logger.LogWarning(ex, "Central database initialization unavailable; continuing locally.");
        }

        using var timer = new PeriodicTimer(
            TimeSpan.FromSeconds(Math.Max(5, _settings.SyncIntervalSeconds)));

        do
        {
            try
            {
                await SyncNowAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                StatusText = "Offline - sync retry scheduled";
                _logger.LogWarning(ex, "Background synchronization cycle failed.");
            }
        }
        while (await timer.WaitForNextTickAsync(cancellationToken));
    }

    public void Dispose()
    {
        _shutdown.Cancel();
        _shutdown.Dispose();
    }
}
