using WeightVerificationQR.Core.Models;
using Xunit;

namespace WeightVerificationQR.Core.Tests;

public class ProductEvaluateTests
{
    private static Product Product1() => new()
    {
        ProductName = "I Kit 12 mm & 6 mm EPE",
        Quantity = "100 Nos",
        MinWeightKg = 1.000m,
        MaxWeightKg = 1.051m
    };

    private static Product Product2() => new()
    {
        ProductName = "12.7 mm & 6.35 mm EPE Gray",
        Quantity = "100 Nos",
        MinWeightKg = 1.050m,
        MaxWeightKg = 1.080m
    };

    // ---- Product 1: 1.000 - 1.051 kg ----

    [Theory]
    [InlineData(1.000)]  // exact lower boundary - inclusive
    [InlineData(1.025)]  // mid-range
    [InlineData(1.051)]  // exact upper boundary - inclusive
    public void Product1_WithinRange_Passes(double weight)
    {
        var (result, reason) = Product1().Evaluate((decimal)weight);
        Assert.Equal(WeighResult.Pass, result);
        Assert.Equal(FailReason.None, reason);
    }

    [Fact]
    public void Product1_BelowMinimum_FailsWithBelowLimitReason()
    {
        var (result, reason) = Product1().Evaluate(0.999m);
        Assert.Equal(WeighResult.Fail, result);
        Assert.Equal(FailReason.WeightBelowLimit, reason);
    }

    [Fact]
    public void Product1_AboveMaximum_FailsWithAboveLimitReason()
    {
        var (result, reason) = Product1().Evaluate(1.052m);
        Assert.Equal(WeighResult.Fail, result);
        Assert.Equal(FailReason.WeightAboveLimit, reason);
    }

    // ---- Product 2: 1.050 - 1.080 kg ----

    [Theory]
    [InlineData(1.050)]
    [InlineData(1.065)]
    [InlineData(1.080)]
    public void Product2_WithinRange_Passes(double weight)
    {
        var (result, reason) = Product2().Evaluate((decimal)weight);
        Assert.Equal(WeighResult.Pass, result);
        Assert.Equal(FailReason.None, reason);
    }

    [Fact]
    public void Product2_BelowMinimum_Fails()
    {
        var (result, reason) = Product2().Evaluate(1.049m);
        Assert.Equal(WeighResult.Fail, result);
        Assert.Equal(FailReason.WeightBelowLimit, reason);
    }

    [Fact]
    public void Product2_AboveMaximum_Fails()
    {
        var (result, reason) = Product2().Evaluate(1.081m);
        Assert.Equal(WeighResult.Fail, result);
        Assert.Equal(FailReason.WeightAboveLimit, reason);
    }

    [Fact]
    public void ZeroWeight_AlwaysFailsBelowLimit()
    {
        var (result, reason) = Product1().Evaluate(0m);
        Assert.Equal(WeighResult.Fail, result);
        Assert.Equal(FailReason.WeightBelowLimit, reason);
    }
}
