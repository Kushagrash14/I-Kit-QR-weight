using WeightVerificationQR.Core.Interfaces;
using WeightVerificationQR.Core.Models;
using WeightVerificationQR.Services;
using Xunit;

namespace WeightVerificationQR.Services.Tests;

public class SerialPortServiceTests
{
    private static SerialPortSettings TestSettings() => new()
    {
        StableReadingCount = 3,
        StabilityToleranceKg = 0.002m,
        ResetWeightThresholdKg = 0.050m
    };

    [Fact]
    public void SplitFrame_IsBufferedUntilLineIsComplete()
    {
        using var service = new SerialPortService(TestSettings());
        var readings = new List<WeightReadingEventArgs>();
        service.WeightReceived += (_, reading) => readings.Add(reading);

        service.ProcessIncomingData("ST,GS,+001.");
        Assert.Empty(readings);

        service.ProcessIncomingData("030kg\r\n");

        var reading = Assert.Single(readings);
        Assert.Equal(1.030m, reading.WeightKg);
        Assert.False(reading.IsStable);
    }

    [Fact]
    public void StableWeight_FiresOnlyOnceUntilScaleIsUnloaded()
    {
        using var service = new SerialPortService(TestSettings());
        var stableCount = 0;
        service.WeightReceived += (_, reading) =>
        {
            if (reading.IsStable) stableCount++;
        };

        service.ProcessIncomingData(
            "ST,GS,+001.030kg\r\nST,GS,+001.031kg\r\nST,GS,+001.030kg\r\n");
        service.ProcessIncomingData(
            "ST,GS,+001.030kg\r\nST,GS,+001.030kg\r\nST,GS,+001.030kg\r\n");

        Assert.Equal(1, stableCount);

        service.ProcessIncomingData("ST,GS,+000.000kg\r\n");
        service.ProcessIncomingData(
            "ST,GS,+001.030kg\r\nST,GS,+001.030kg\r\nST,GS,+001.030kg\r\n");

        Assert.Equal(2, stableCount);
    }

    [Fact]
    public void IndicatorUnstableFrames_NeverBecomeStable()
    {
        using var service = new SerialPortService(TestSettings());
        var readings = new List<WeightReadingEventArgs>();
        service.WeightReceived += (_, reading) => readings.Add(reading);

        service.ProcessIncomingData(
            "US,GS,+001.030kg\r\nUS,GS,+001.030kg\r\nUS,GS,+001.030kg\r\n");

        Assert.Equal(3, readings.Count);
        Assert.All(readings, reading => Assert.False(reading.IsStable));
    }

    [Fact]
    public void EmptyScaleReadings_NeverCreateAStableWeighing()
    {
        using var service = new SerialPortService(TestSettings());
        var readings = new List<WeightReadingEventArgs>();
        service.WeightReceived += (_, reading) => readings.Add(reading);

        service.ProcessIncomingData(
            "ST,GS,+000.000kg\r\nST,GS,+000.000kg\r\nST,GS,+000.000kg\r\n");

        Assert.Equal(3, readings.Count);
        Assert.All(readings, reading => Assert.False(reading.IsStable));
    }
}
