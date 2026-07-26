using WeightVerificationQR.Core.Models;

namespace WeightVerificationQR.App;

/// <summary>Singleton holding the logged-in user's identity for the current app session.</summary>
public class SessionContext
{
    public User? CurrentUser { get; set; }

    /// <summary>Highest privilege level. Can manage Admin accounts too.</summary>
    public bool IsSuperAdmin => CurrentUser?.Role == UserRole.SuperAdmin;

    /// <summary>Admin-level access (SuperAdmin implicitly qualifies).</summary>
    public bool IsAdmin => CurrentUser?.Role is UserRole.Admin or UserRole.SuperAdmin;

    public bool IsSupervisorOrAbove =>
        CurrentUser?.Role is UserRole.Admin or UserRole.SuperAdmin or UserRole.Supervisor;

    /// <summary>Numeric rank used to compare two users' authority (higher = more powerful).</summary>
    public static int RankOf(UserRole role) => role switch
    {
        UserRole.SuperAdmin => 4,
        UserRole.Admin => 3,
        UserRole.Supervisor => 2,
        _ => 1
    };

    public int CurrentRank => CurrentUser is null ? 0 : RankOf(CurrentUser.Role);
}
