using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WeightVerificationQR.Core.Models;

/// <summary>
/// One row per weighing event. This is the audit trail for every kit weighed,
/// regardless of PASS or FAIL.
/// </summary>
public class WeighRecord
{
    [Key]
    public int Id { get; set; }

    /// <summary>Human readable unique kit number, also encoded in the QR, e.g. KIT202607110001</summary>
    [Required, MaxLength(40)]
    public string KitNumber { get; set; } = string.Empty;

    public int ProductId { get; set; }

    [MaxLength(200)]
    public string ProductName { get; set; } = string.Empty;

    [MaxLength(50)]
    public string Quantity { get; set; } = string.Empty;

    [Column(TypeName = "decimal(10,3)")]
    public decimal WeightKg { get; set; }

    public WeighResult Result { get; set; }

    public FailReason FailReason { get; set; }

    public DateTime RecordDate { get; set; } = DateTime.Now;

    [MaxLength(100)]
    public string OperatorName { get; set; } = string.Empty;

    /// <summary>Same as KitNumber, kept separately in case QR payload format changes later.</summary>
    [MaxLength(40)]
    public string QrId { get; set; } = string.Empty;

    public bool QrGenerated { get; set; }

    public bool PrintedSuccessfully { get; set; }

    [MaxLength(50)]
    public string PrinterStatus { get; set; } = "N/A";

    public int ReprintCount { get; set; }

    [MaxLength(500)]
    public string Remarks { get; set; } = string.Empty;

    // Convenience read-only properties for UI binding / reports
    [NotMapped]
    public string DateText => RecordDate.ToString("dd-MM-yyyy");

    [NotMapped]
    public string TimeText => RecordDate.ToString("HH:mm:ss");
}
