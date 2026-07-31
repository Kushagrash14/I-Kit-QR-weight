using Microsoft.Extensions.Logging.Abstractions;
using WeightVerificationQR.Core.Interfaces;
using WeightVerificationQR.Core.Models;
using WeightVerificationQR.Services;
using Xunit;

namespace WeightVerificationQR.Services.Tests;

public class SerialNumberServiceTests
{
    [Fact]
    public void BuildKitNumber_UsesConfiguredCodesDateActualWeightAndDailySerial()
    {
        var service = CreateService(
            new FakeRepository(),
            new FakeCentralStore(),
            new StationSettings
            {
                QrPrefix = "p",
                SiteCode = "site 1",
                LineCode = "line-2",
                MachineCode = "wm_03",
                SerialDigits = 8
            });

        var qr = service.BuildKitNumber(
            "p",
            "odu 2",
            2,
            1.064m,
            new DateTime(2026, 7, 29, 10, 30, 0));

        Assert.Equal("P-ODU2-290726-1064-000002", qr);
    }

    [Fact]
    public void BuildQrPayload_IncludesModelAndIndividualKitWeight()
    {
        var service = CreateService(
            new FakeRepository(),
            new FakeCentralStore(),
            new StationSettings());
        var record = new WeighRecord
        {
            KitNumber = "P-I-290726-1051-000001",
            CommandCode = "P",
            LineCode = "I",
            ModelCode = "IKIT-A",
            ProductName = "I Kit Model A",
            QrText = "INSTA KIT (5/8 & 3/8) EPE",
            LabelSizeText = "5/8\" & 3/8\"",
            LabelLengthText = "3 Meter",
            LabelMaterialText = "EPE",
            WeightKg = 1.051m,
            DailySerialNumber = 1,
            RecordDate = new DateTime(2026, 7, 29)
        };

        var payload = service.BuildQrPayload(record);

        Assert.Equal(
            "INSTA KIT (5/8 & 3/8) EPE P-I-290726-1051-000001",
            payload);
    }

    [Fact]
    public async Task GetNextAsync_ConcurrentRequestsUseUniqueCentralSerials()
    {
        var repository = new FakeRepository();
        var central = new FakeCentralStore(new SerialNumberBlock(1, 1000));
        var service = CreateService(repository, central, new StationSettings());

        var allocations = await Task.WhenAll(
            Enumerable.Range(0, 50).Select(_ => service.GetNextAsync()));

        Assert.Equal(50, allocations.Select(a => a.Value).Distinct().Count());
        Assert.Equal(Enumerable.Range(1, 50).Select(i => (long)i), allocations.Select(a => a.Value).Order());
        Assert.All(allocations, allocation => Assert.True(allocation.FromCentralBlock));
        Assert.Equal(1, central.AllocationCalls);
    }

    [Fact]
    public async Task GetNextAsync_WhenCentralUnavailableUsesPersistedEmergencyRange()
    {
        var repository = new FakeRepository();
        var service = CreateService(
            repository,
            new FakeCentralStore(),
            new StationSettings { EmergencySerialStart = 90_123_001 });

        var first = await service.GetNextAsync();
        var second = await service.GetNextAsync();

        Assert.Equal(90_123_001, first.Value);
        Assert.Equal(90_123_002, second.Value);
        Assert.False(first.FromCentralBlock);
        Assert.False(second.FromCentralBlock);
    }

    private static SerialNumberService CreateService(
        IWeighRecordRepository repository,
        ICentralSyncStore central,
        StationSettings stationSettings) =>
        new(
            repository,
            central,
            new CentralSyncSettings { SerialBlockSize = 1000 },
            stationSettings,
            NullLogger<SerialNumberService>.Instance);

    private sealed class FakeCentralStore : ICentralSyncStore
    {
        private readonly SerialNumberBlock? _block;

        public FakeCentralStore(SerialNumberBlock? block = null) => _block = block;

        public bool IsEnabled => _block is not null;
        public int AllocationCalls { get; private set; }

        public Task InitializeAsync(CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<SerialNumberBlock?> TryAllocateSerialBlockAsync(
            int blockSize,
            CancellationToken cancellationToken = default)
        {
            AllocationCalls++;
            return Task.FromResult(_block);
        }

        public Task UpsertWeighRecordAsync(
            WeighRecord record,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class FakeRepository : IWeighRecordRepository
    {
        private readonly SerialNumberState _state = new();

        public Task<SerialNumberState> GetSerialNumberStateAsync() => Task.FromResult(_state);
        public Task UpdateSerialNumberStateAsync(SerialNumberState state) => Task.CompletedTask;

        public Task<WeighRecord> AddAsync(WeighRecord record) => Task.FromResult(record);
        public Task UpdateAsync(WeighRecord record) => Task.CompletedTask;
        public Task<WeighRecord?> GetByIdAsync(int id) => Task.FromResult<WeighRecord?>(null);
        public Task<WeighRecord?> GetByQrIdAsync(string qrId) => Task.FromResult<WeighRecord?>(null);
        public Task<string> GenerateNextKitNumberAsync(string codePrefix) => Task.FromResult(string.Empty);
        public Task<int> GetNextDailyLabelSerialAsync(
            int productId,
            DateTime productionDate) => Task.FromResult(1);
        public Task<(int passCount, int failCount)> GetTodayCountsAsync() => Task.FromResult((0, 0));
        public Task<List<WeighRecord>> GetPendingSyncAsync(int maxCount) => Task.FromResult(new List<WeighRecord>());
        public Task MarkSyncedAsync(Guid globalRecordId, DateTime syncedAt) => Task.CompletedTask;
        public Task MarkSyncFailedAsync(Guid globalRecordId, string error) => Task.CompletedTask;

        public Task<List<WeighRecord>> SearchAsync(
            DateTime? fromDate = null,
            DateTime? toDate = null,
            string? productName = null,
            string? qrId = null,
            decimal? weightExact = null,
            WeighResult? result = null,
            string? operatorName = null) =>
            Task.FromResult(new List<WeighRecord>());
    }
}
