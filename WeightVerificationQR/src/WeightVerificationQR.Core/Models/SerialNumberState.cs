using System.ComponentModel.DataAnnotations;

namespace WeightVerificationQR.Core.Models;

public class SerialNumberState
{
    [Key]
    public int Id { get; set; } = 1;

    public long NextSerial { get; set; }
    public long BlockEndSerial { get; set; }
    public long EmergencyNextSerial { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}

public sealed record SerialNumberAllocation(long Value, bool FromCentralBlock);

public sealed record SerialNumberBlock(long Start, long End);
