using WeightVerificationQR.Core.Models;

namespace WeightVerificationQR.Core.Interfaces;

public interface IProductRepository
{
    Task<List<Product>> GetAllAsync(bool activeOnly = true);
    Task<Product?> GetByIdAsync(int id);
    Task<Product> AddAsync(Product product);
    Task UpdateAsync(Product product);
    Task DeleteAsync(int id);
}

public interface IWeighRecordRepository
{
    Task<WeighRecord> AddAsync(WeighRecord record);
    Task UpdateAsync(WeighRecord record);
    Task<WeighRecord?> GetByIdAsync(int id);
    Task<WeighRecord?> GetByQrIdAsync(string qrId);
    Task<List<WeighRecord>> SearchAsync(
        DateTime? fromDate = null,
        DateTime? toDate = null,
        string? productName = null,
        string? qrId = null,
        decimal? weightExact = null,
        WeighResult? result = null,
        string? operatorName = null);
    Task<string> GenerateNextKitNumberAsync(string codePrefix);
    Task<(int passCount, int failCount)> GetTodayCountsAsync();
    Task<SerialNumberState> GetSerialNumberStateAsync();
    Task UpdateSerialNumberStateAsync(SerialNumberState state);
    Task<List<WeighRecord>> GetPendingSyncAsync(int maxCount);
    Task MarkSyncedAsync(Guid globalRecordId, DateTime syncedAt);
    Task MarkSyncFailedAsync(Guid globalRecordId, string error);
}

public interface IUserRepository
{
    Task<List<User>> GetAllAsync();
    Task<User?> GetByIdAsync(int id);
    Task<User?> GetByUsernameAsync(string username);
    Task<User> AddAsync(User user);
    Task UpdateAsync(User user);
    Task DeleteAsync(int id);
}
