using Microsoft.AspNetCore.Identity;

namespace ChonRieng.Domain.Entities;

public sealed class User : IdentityUser<Guid>
{
    public User()
    {
        Id = Guid.CreateVersion7();
    }

    public string FullName { get; set; } = "";
    public Employee? Employee { get; set; }
    public Customer? Customer { get; set; }
}
