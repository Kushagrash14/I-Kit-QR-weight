using Microsoft.EntityFrameworkCore;
using WeightVerificationQR.Core.Interfaces;
using WeightVerificationQR.Core.Models;

namespace WeightVerificationQR.Data.Repositories;

public class WeighRecordRepository : IWeighRecordRepository
{
    private readonly AppDbContext _context;
    private static readonly SemaphoreSlim _sequenceLock = new(1, 1);

    public WeighRecordRepository(AppDbContext context) => _context = context;

    public async Task<WeighRecord> AddAsync(WeighRecord record)
    {
        if (record.GlobalRecordId == Guid.Empty)
            record.GlobalRecordId = Guid.NewGuid();
        record.SyncStatus = RecordSyncStatus.Pending;
        record.SyncedAt = null;
        _context.WeighRecords.Add(record);
        await _context.SaveChangesAsync();
        return record;
    }

    public async Task UpdateAsync(WeighRecord record)
    {
        record.SyncStatus = RecordSyncStatus.Pending;
        record.SyncedAt = null;
        _context.WeighRecords.Update(record);
        await _context.SaveChangesAsync();
    }

    public Task<WeighRecord?> GetByIdAsync(int id) =>
        _context.WeighRecords.FirstOrDefaultAsync(r => r.Id == id);

    public Task<WeighRecord?> GetByQrIdAsync(string qrId) =>
        _context.WeighRecords.FirstOrDefaultAsync(r => r.QrId == qrId);

    public async Task<List<WeighRecord>> SearchAsync(
        DateTime? fromDate = null,
        DateTime? toDate = null,
        string? productName = null,
        string? qrId = null,
        decimal? weightExact = null,
        WeighResult? result = null,
        string? operatorName = null)
    {
        var query = _context.WeighRecords.AsNoTracking().AsQueryable();

        if (fromDate.HasValue)
            query = query.Where(r => r.RecordDate >= fromDate.Value.Date);

        if (toDate.HasValue)
            query = query.Where(r => r.RecordDate < toDate.Value.Date.AddDays(1));

        if (!string.IsNullOrWhiteSpace(productName))
            query = query.Where(r => r.ProductName.Contains(productName));

        if (!string.IsNullOrWhiteSpace(qrId))
            query = query.Where(r => r.QrId.Contains(qrId));

        if (weightExact.HasValue)
            query = query.Where(r => r.WeightKg == weightExact.Value);

        if (result.HasValue)
            query = query.Where(r => r.Result == result.Value);

        if (!string.IsNullOrWhiteSpace(operatorName))
            query = query.Where(r => r.OperatorName.Contains(operatorName));

        return await query.OrderByDescending(r => r.RecordDate).ToListAsync();
    }

    /// <summary>
    /// Generates the next sequential kit number for today, e.g. KIT202607110001.
    /// Format: {prefix}{yyyyMMdd}{4-digit daily sequence}.
    /// Locked with a semaphore so two near-simultaneous PASS events never collide
    /// within a single process; the unique index on KitNumber is the final safety net
    /// across process/instance boundaries.
    /// </summary>
    public async Task<string> GenerateNextKitNumberAsync(string codePrefix)
    {
        await _sequenceLock.WaitAsync();
        try
        {
            var datePart = DateTime.Now.ToString("yyyyMMdd");
            var todayPrefix = $"{codePrefix}{datePart}";

            var lastToday = await _context.WeighRecords
                .Where(r => r.KitNumber.StartsWith(todayPrefix))
                .OrderByDescending(r => r.KitNumber)
                .Select(r => r.KitNumber)
                .FirstOrDefaultAsync();

            int nextSeq = 1;
            if (lastToday is not null && lastToday.Length >= todayPrefix.Length + 4)
            {
                var seqPart = lastToday[todayPrefix.Length..];
                if (int.TryParse(seqPart, out var parsed))
                    nextSeq = parsed + 1;
            }

            return $"{todayPrefix}{nextSeq:D4}";
        }
        finally
        {
            _sequenceLock.Release();
        }
    }

    public async Task<int> GetNextDailyLabelSerialAsync(
        int productId,
        DateTime productionDate)
    {
        await _sequenceLock.WaitAsync();
        try
        {
            var start = productionDate.Date;
            var end = start.AddDays(1);
            var lastSerial = await _context.WeighRecords
                .Where(r =>
                    r.RecordDate >= start &&
                    r.RecordDate < end &&
                    r.ProductId == productId &&
                    r.DailySerialNumber > 0)
                .MaxAsync(r => (int?)r.DailySerialNumber) ?? 0;

            if (lastSerial >= 999_999)
                throw new InvalidOperationException(
                    $"Daily serial capacity exhausted for product {productId} on {start:dd-MM-yyyy}.");

            return lastSerial + 1;
        }
        finally
        {
            _sequenceLock.Release();
        }
    }

    public async Task<(int passCount, int failCount)> GetTodayCountsAsync()
    {
        var today = DateTime.Now.Date;
        var pass = await _context.WeighRecords
            .CountAsync(r => r.RecordDate >= today && r.Result == WeighResult.Pass);
        var fail = await _context.WeighRecords
            .CountAsync(r => r.RecordDate >= today && r.Result == WeighResult.Fail);
        return (pass, fail);
    }

    public async Task<SerialNumberState> GetSerialNumberStateAsync()
    {
        var state = await _context.SerialNumberStates.FirstOrDefaultAsync(s => s.Id == 1);
        if (state is not null)
            return state;

        state = new SerialNumberState { Id = 1 };
        _context.SerialNumberStates.Add(state);
        await _context.SaveChangesAsync();
        return state;
    }

    public async Task UpdateSerialNumberStateAsync(SerialNumberState state)
    {
        state.UpdatedAt = DateTime.Now;
        _context.SerialNumberStates.Update(state);
        await _context.SaveChangesAsync();
    }

    public Task<List<WeighRecord>> GetPendingSyncAsync(int maxCount) =>
        _context.WeighRecords
            .Where(r => r.SyncStatus != RecordSyncStatus.Synced)
            .OrderBy(r => r.RecordDate)
            .Take(Math.Max(1, maxCount))
            .ToListAsync();

    public async Task MarkSyncedAsync(Guid globalRecordId, DateTime syncedAt)
    {
        var record = await _context.WeighRecords.FirstOrDefaultAsync(r => r.GlobalRecordId == globalRecordId);
        if (record is null) return;

        record.SyncStatus = RecordSyncStatus.Synced;
        record.SyncedAt = syncedAt;
        record.LastSyncError = string.Empty;
        await _context.SaveChangesAsync();
    }

    public async Task MarkSyncFailedAsync(Guid globalRecordId, string error)
    {
        var record = await _context.WeighRecords.FirstOrDefaultAsync(r => r.GlobalRecordId == globalRecordId);
        if (record is null) return;

        record.SyncStatus = RecordSyncStatus.Failed;
        record.SyncAttempts++;
        record.LastSyncError = error.Length <= 500 ? error : error[..500];
        await _context.SaveChangesAsync();
    }
}
