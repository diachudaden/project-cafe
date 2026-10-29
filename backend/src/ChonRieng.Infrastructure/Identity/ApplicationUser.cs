using Microsoft.AspNetCore.Identity;

namespace ChonRieng.Infrastructure.Identity;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    public required string FullName { get; set; }
    public required string Role { get; set; }
    public string BranchIdsJson { get; set; } = "[]";
}
