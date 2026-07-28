namespace FunApp.Api.Auth;

public sealed class OAuthProviderException : Exception
{
    public OAuthProviderException(string message)
        : base(message)
    {
    }
}
