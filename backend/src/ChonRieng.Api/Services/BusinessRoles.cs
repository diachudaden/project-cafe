namespace ChonRieng.Api.Services;

public static class BusinessRoles
{
    public const string Admin = "Admin";
    public const string BranchManager = "BranchManager";
    public const string Staff = "Staff";
    public const string Customer = "Customer";
    public const string BranchIdClaim = "branch_id";

    public static IReadOnlySet<string> All { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        Admin,
        BranchManager,
        Staff,
        Customer,
    };
}
