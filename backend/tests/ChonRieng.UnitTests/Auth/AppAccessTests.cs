using ChonRieng.Domain.Auth;

namespace ChonRieng.UnitTests.Auth;

public class AppAccessTests
{
    [Theory]
    [InlineData(AuthClients.Admin, AppRoles.Admin, true)]
    [InlineData(AuthClients.Admin, AppRoles.BranchManager, true)]
    [InlineData(AuthClients.Admin, AppRoles.Staff, false)]
    [InlineData(AuthClients.Admin, AppRoles.Customer, false)]
    [InlineData(AuthClients.Pos, AppRoles.Staff, true)]
    [InlineData(AuthClients.Pos, AppRoles.Admin, false)]
    [InlineData(AuthClients.Customer, AppRoles.Customer, true)]
    [InlineData(AuthClients.Customer, AppRoles.Staff, false)]
    public void Allows_MatchesApprovedAppRoleMatrix(string client, string role, bool expected)
    {
        Assert.Equal(expected, AppAccess.Allows(client, role));
    }
}
