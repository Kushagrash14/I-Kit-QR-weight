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
    [Required, MaxLength(120)]
    public string KitNumber { get; set; } = string.Empty;

    public Guid GlobalRecordId { get; set; } = Guid.NewGuid();

    [MaxLength(20)]
    public string SiteCode { get; set; } = string.Empty;

    [MaxLength(20)]
    public string LineCode { get; set; } = string.Empty;

    [MaxLength(20)]
    public string MachineCode { get; set; } = string.Empty;

    [MaxLength(10)]
    public string CommandCode { get; set; } = string.Empty;

    [MaxLength(50)]
    public string ModelCode { get; set; } = string.Empty;

    [MaxLength(100)]
    public string LabelSizeText { get; set; } = string.Empty;

    [MaxLength(50)]
    public string LabelLengthText { get; set; } = string.Empty;

    [MaxLength(50)]
    public string LabelMaterialText { get; set; } = string.Empty;

    /// <summary>Six-digit sequence reset per production date and configured line code.</summary>
    public int DailySerialNumber { get; set; }

    /// <summary>Globally allocated backend serial used for multi-station synchronization.</summary>
    public long SerialNumber { get; set; }

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
    [MaxLength(120)]
    public string QrId { get; set; } = string.Empty;

    /// <summary>Structured model and weighing data encoded inside the printed QR.</summary>
    [MaxLength(500)]
    public string QrPayload { get; set; } = string.Empty;

    public bool QrGenerated { get; set; }

    public bool PrintedSuccessfully { get; set; }

    [MaxLength(50)]
    public string PrinterStatus { get; set; } = "N/A";

    public int ReprintCount { get; set; }

    [MaxLength(500)]
    public string Remarks { get; set; } = string.Empty;

    public RecordSyncStatus SyncStatus { get; set; } = RecordSyncStatus.Pending;

    public int SyncAttempts { get; set; }

    [MaxLength(500)]
    public string LastSyncError { get; set; } = string.Empty;

    public DateTime? SyncedAt { get; set; }

    // Convenience read-only properties for UI binding / reports
    [NotMapped]
    public string DateText => RecordDate.ToString("dd-MM-yyyy");

    [NotMapped]
    public string TimeText => RecordDate.ToString("HH:mm:ss");
}
