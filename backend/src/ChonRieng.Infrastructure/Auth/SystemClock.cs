using ChonRieng.Application.Common;

namespace ChonRieng.Infrastructure.Auth;

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
