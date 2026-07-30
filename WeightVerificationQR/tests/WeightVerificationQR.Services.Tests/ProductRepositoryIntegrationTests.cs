using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using WeightVerificationQR.Core.Models;
using WeightVerificationQR.Data;
using WeightVerificationQR.Data.Repositories;
using WeightVerificationQR.Services;
using Xunit;

namespace WeightVerificationQR.Services.Tests;

public class ProductRepositoryIntegrationTests
{
    [Fact]
    public async Task InitializeAsync_ImportsCatalogOnlyWhenProductTableIsEmpty()
    {
        var seedFilePath = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(
                seedFilePath,
                """
                [
                  {
                    "productName": "Catalog Product",
                    "quantity": "25 Nos",
                    "minWeightKg": 1.0,
                    "maxWeightKg": 1.1,
                    "codePrefix": "KIT",
                    "commandCode": "P",
                    "labelLineCode": "A1",
                    "modelCode": "MODEL-A1",
                    "labelSizeText": "12 mm",
                    "labelLengthText": "3 Meter",
                    "labelMaterialText": "EPE",
                    "isActive": true
                  }
                ]
                """);

            await using var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection)
                .Options;

            await using (var firstContext = new AppDbContext(options))
            {
                await DbInitializer.InitializeAsync(
                    firstContext,
                    new PasswordHasher(),
                    seedFilePath);
                Assert.Equal("Catalog Product", (await firstContext.Products.SingleAsync()).ProductName);
            }

            await File.WriteAllTextAsync(seedFilePath, "[]");
            await using var secondContext = new AppDbContext(options);
            await DbInitializer.InitializeAsync(secondContext, new PasswordHasher(), seedFilePath);
            Assert.Equal("Catalog Product", (await secondContext.Products.SingleAsync()).ProductName);
        }
        finally
        {
            File.Delete(seedFilePath);
        }
    }

    [Fact]
    public async Task AddAsync_SavesAllLabelConfigurationFields()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var context = new AppDbContext(options);
        await DbInitializer.InitializeAsync(context, new PasswordHasher());

        var repository = new ProductRepository(context);
        var product = new Product
        {
            ProductName = "O Kit 5/8 & 3/8",
            CommandCode = "P",
            LabelLineCode = "O",
            ModelCode = "ODU-58-38",
            LabelSizeText = "5/8\" & 3/8\"",
            LabelLengthText = "3 Meter",
            LabelMaterialText = "EPE",
            Quantity = "2",
            MinWeightKg = 1.000m,
            MaxWeightKg = 1.080m,
            CodePrefix = "KIT",
            IsActive = true
        };

        await repository.AddAsync(product);

        var saved = await context.Products.SingleAsync(p => p.Id == product.Id);
        Assert.Equal("P", saved.CommandCode);
        Assert.Equal("O", saved.LabelLineCode);
        Assert.Equal("ODU-58-38", saved.ModelCode);
        Assert.Equal("5/8\" & 3/8\"", saved.LabelSizeText);
        Assert.Equal("3 Meter", saved.LabelLengthText);
        Assert.Equal("EPE", saved.LabelMaterialText);
        Assert.Equal(1.000m, saved.MinWeightKg);
        Assert.Equal(1.080m, saved.MaxWeightKg);
    }
}
