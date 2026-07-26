namespace WeightVerificationQR.Core.Models;

public enum WeighResult
{
    Pending = 0,
    Pass = 1,
    Fail = 2
}

public enum FailReason
{
    None = 0,
    WeightBelowLimit = 1,
    WeightAboveLimit = 2,
    Unstable = 3,
    NoProductSelected = 4
}

public enum UserRole
{
    Admin = 1,
    Supervisor = 2,
    Operator = 3,
    /// <summary>Highest privilege level - above Admin. Can manage Admin accounts.</summary>
    SuperAdmin = 4
}

public enum ConnectionStatus
{
    Disconnected = 0,
    Connecting = 1,
    Connected = 2,
    Error = 3
}

public enum PrinterType
{
    Zebra = 1,
    TSC = 2,
    Godex = 3
}

public enum PrinterConnectionMode
{
    Network = 1,
    UsbSerial = 2,
    LocalWindowsPrintQueue = 3,
    BarTender = 4
}
