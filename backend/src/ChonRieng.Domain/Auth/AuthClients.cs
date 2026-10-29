namespace ChonRieng.Domain.Auth;

public static class AuthClients
{
    public const string Admin = "admin";
    public const string Pos = "pos";
    public const string Customer = "customer";

    public static bool IsKnown(string? client) =>
        client is Admin or Pos or Customer;
}

public static class AppAccess
{
    public static bool Allows(string client, string role) => client switch
    {
        AuthClients.Admin => role is AppRoles.Admin or AppRoles.BranchManager,
        AuthClients.Pos => role is AppRoles.Staff,
        AuthClients.Customer => role is AppRoles.Customer,
        _ => false,
    };
}
