using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WeightVerificationQR.Core.Models;

/// <summary>
/// Represents a product/kit type with its acceptable weight range.
/// Configured by Admin under Product Master.
/// </summary>
public class Product
{
    [Key]
    public int Id { get; set; }

    [Required, MaxLength(200)]
    public string ProductName { get; set; } = string.Empty;

    /// <summary>Quantity of pieces per kit, e.g. "100 Nos"</summary>
    [MaxLength(50)]
    public string Quantity { get; set; } = string.Empty;

    [Column(TypeName = "decimal(10,3)")]
    public decimal MinWeightKg { get; set; }

    [Column(TypeName = "decimal(10,3)")]
    public decimal MaxWeightKg { get; set; }

    /// <summary>Short code used as a prefix inside the generated QR/Kit Number, e.g. "KIT"</summary>
    [MaxLength(10)]
    public string CodePrefix { get; set; } = "KIT";

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// Core PASS/FAIL evaluation logic. Weight must be inclusive of Min and Max.
    /// </summary>
    public (WeighResult Result, FailReason Reason) Evaluate(decimal weightKg)
    {
        if (weightKg < MinWeightKg)
            return (WeighResult.Fail, FailReason.WeightBelowLimit);

        if (weightKg > MaxWeightKg)
            return (WeighResult.Fail, FailReason.WeightAboveLimit);

        return (WeighResult.Pass, FailReason.None);
    }
}
