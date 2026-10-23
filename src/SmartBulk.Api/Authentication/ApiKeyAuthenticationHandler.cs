using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace SmartBulk.Api.Authentication;

public class ApiKeyAuthenticationOptions : AuthenticationSchemeOptions
{
    public const string DefaultScheme = "ApiKey";
    public const string HeaderName = "X-Api-Key";
    public const string SecondaryHeaderName = "ApiKey";
}

public class ApiKeyAuthenticationHandler : AuthenticationHandler<ApiKeyAuthenticationOptions>
{
    private readonly IConfiguration _configuration;

    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<ApiKeyAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IConfiguration configuration)
        : base(options, logger, encoder)
    {
        _configuration = configuration;
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        string? providedKey = null;

        if (Request.Headers.TryGetValue(ApiKeyAuthenticationOptions.HeaderName, out var headerValue))
        {
            providedKey = headerValue.FirstOrDefault();
        }
        else if (Request.Headers.TryGetValue(ApiKeyAuthenticationOptions.SecondaryHeaderName, out var secondaryValue))
        {
            providedKey = secondaryValue.FirstOrDefault();
        }
        else if (Request.Query.TryGetValue("apiKey", out var queryValue))
        {
            providedKey = queryValue.FirstOrDefault();
        }

        if (string.IsNullOrWhiteSpace(providedKey))
        {
            return Task.FromResult(AuthenticateResult.Fail("Chave de API não fornecida."));
        }

        var configuredKey = _configuration.GetValue<string>("ApiKey")
            ?? _configuration.GetValue<string>("Authentication:ApiKey")
            ?? "smartbulk-secret-key";

        if (!string.Equals(providedKey, configuredKey, StringComparison.Ordinal))
        {
            return Task.FromResult(AuthenticateResult.Fail("Chave de API inválida."));
        }

        var identity = new ClaimsIdentity(
            new[] { new Claim(ClaimTypes.Name, "SmartBulkUser") },
            Scheme.Name);

        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
