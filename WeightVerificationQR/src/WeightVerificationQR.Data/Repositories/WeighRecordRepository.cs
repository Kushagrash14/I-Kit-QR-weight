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
        _context.WeighRecords.Add(record);
        await _context.SaveChangesAsync();
        return record;
    }

    public async Task UpdateAsync(WeighRecord record)
    {
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

    public async Task<(int passCount, int failCount)> GetTodayCountsAsync()
    {
        var today = DateTime.Now.Date;
        var pass = await _context.WeighRecords
            .CountAsync(r => r.RecordDate >= today && r.Result == WeighResult.Pass);
        var fail = await _context.WeighRecords
            .CountAsync(r => r.RecordDate >= today && r.Result == WeighResult.Fail);
        return (pass, fail);
    }
}
