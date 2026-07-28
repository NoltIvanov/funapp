using FunApp.Api.Auth;
using FunApp.Api.Contracts;
using FunApp.Api.Data;
using FunApp.Api.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<GoogleOAuthOptions>(
    builder.Configuration.GetSection(GoogleOAuthOptions.SectionName));
builder.Services.Configure<AuthSessionOptions>(
    builder.Configuration.GetSection(AuthSessionOptions.SectionName));

builder.Services.AddHttpClient<GoogleOAuthClient>();
builder.Services.AddSingleton<IDbConnectionFactory, SqliteConnectionFactory>();
builder.Services.AddSingleton<DatabaseInitializer>();
builder.Services.AddScoped<UserRepository>();
builder.Services.AddScoped<SessionRepository>();

var app = builder.Build();

await app.Services.GetRequiredService<DatabaseInitializer>().InitializeAsync();

app.MapGet("/health", () => Results.Ok(new HealthResponse("ok", DateTimeOffset.UtcNow)))
    .WithName("Health");

app.MapPost("/auth/google/exchange", ExchangeGoogleCodeAsync)
    .WithName("ExchangeGoogleCode");

app.MapGet("/me", GetCurrentUserAsync)
    .WithName("GetCurrentUser");

app.Run();

static async Task<IResult> ExchangeGoogleCodeAsync(
    GoogleAuthorizationCodeExchangeRequest request,
    GoogleOAuthClient googleOAuthClient,
    UserRepository users,
    SessionRepository sessions,
    CancellationToken cancellationToken)
{
    var validationError = ValidateExchangeRequest(request);
    if (validationError is not null)
        return validationError;

    try
    {
        var profile = await googleOAuthClient.ExchangeCodeForProfileAsync(request, cancellationToken);
        var user = await users.UpsertAsync(profile, cancellationToken);
        var session = await sessions.CreateAsync(user.AppUserId, cancellationToken);

        return Results.Ok(new AuthResponse(session.Token, session.ExpiresAtUtc, user.ToResponse()));
    }
    catch (OAuthConfigurationException ex)
    {
        return Results.Problem(
            title: "Google OAuth is not configured",
            detail: ex.Message,
            statusCode: StatusCodes.Status500InternalServerError);
    }
    catch (OAuthProviderException ex)
    {
        return Results.Problem(
            title: "Google token exchange failed",
            detail: ex.Message,
            statusCode: StatusCodes.Status400BadRequest);
    }
}

static async Task<IResult> GetCurrentUserAsync(
    HttpContext context,
    SessionRepository sessions,
    CancellationToken cancellationToken)
{
    var token = ReadBearerToken(context.Request.Headers.Authorization.ToString());
    if (string.IsNullOrWhiteSpace(token))
        return Results.Unauthorized();

    var user = await sessions.FindUserByTokenAsync(token, cancellationToken);
    return user is null
        ? Results.Unauthorized()
        : Results.Ok(user.ToResponse());
}

static IResult? ValidateExchangeRequest(GoogleAuthorizationCodeExchangeRequest request)
{
    var errors = new Dictionary<string, string[]>();

    AddRequiredError(errors, nameof(request.AuthorizationCode), request.AuthorizationCode);
    AddRequiredError(errors, nameof(request.CodeVerifier), request.CodeVerifier);
    AddRequiredError(errors, nameof(request.RedirectUri), request.RedirectUri);
    AddRequiredError(errors, nameof(request.ClientId), request.ClientId);

    if (!string.IsNullOrWhiteSpace(request.RedirectUri) &&
        !Uri.TryCreate(request.RedirectUri, UriKind.Absolute, out _))
    {
        errors[nameof(request.RedirectUri)] = ["RedirectUri must be an absolute URI."];
    }

    return errors.Count == 0 ? null : Results.ValidationProblem(errors);
}

static void AddRequiredError(Dictionary<string, string[]> errors, string field, string value)
{
    if (string.IsNullOrWhiteSpace(value))
        errors[field] = [$"{field} is required."];
}

static string? ReadBearerToken(string authorizationHeader)
{
    const string bearerPrefix = "Bearer ";

    return authorizationHeader.StartsWith(bearerPrefix, StringComparison.OrdinalIgnoreCase)
        ? authorizationHeader[bearerPrefix.Length..].Trim()
        : null;
}
