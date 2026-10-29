namespace ChonRieng.Application.Features.Auth.Dtos;

public sealed class LoginRequest
{
    public string? Identifier { get; set; }
    public string? Password { get; set; }
    public string? Client { get; set; }
}
