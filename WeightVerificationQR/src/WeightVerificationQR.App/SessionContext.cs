using WeightVerificationQR.Core.Models;

namespace WeightVerificationQR.App;

/// <summary>Singleton holding the logged-in user's identity for the current app session.</summary>
public class SessionContext
{
    public User? CurrentUser { get; set; }

    public bool IsAdmin => CurrentUser?.Role == UserRole.Admin;
    public bool IsSupervisorOrAbove => CurrentUser?.Role is UserRole.Admin or UserRole.Supervisor;
}
