namespace FunApp.Api.Auth;

public sealed class OAuthConfigurationException : Exception
{
    public OAuthConfigurationException(string message)
        : base(message)
    {
    }
}
