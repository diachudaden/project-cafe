using System.Text.Json;
using ChonRieng.Application.Features.Auth.Dtos;
using ChonRieng.Infrastructure.Identity;

namespace ChonRieng.Infrastructure.Auth;

public static class UserMapper
{
    public static AuthUserDto ToDto(ApplicationUser user)
    {
        var branchIds = JsonSerializer.Deserialize<List<Guid>>(user.BranchIdsJson) ?? [];
        return new AuthUserDto
        {
            Id = user.Id,
            Email = string.IsNullOrWhiteSpace(user.Email) ? null : user.Email,
            Phone = string.IsNullOrWhiteSpace(user.PhoneNumber) ? null : user.PhoneNumber,
            FullName = user.FullName,
            Role = user.Role,
            BranchIds = branchIds,
        };
    }
}
