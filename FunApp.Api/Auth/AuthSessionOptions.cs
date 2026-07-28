namespace FunApp.Api.Auth;

public sealed class AuthSessionOptions
{
    public const string SectionName = "AuthSessions";

    public int LifetimeDays { get; init; } = 30;
}
