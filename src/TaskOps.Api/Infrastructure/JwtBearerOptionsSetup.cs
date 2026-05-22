using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using TaskOps.Infrastructure.Security;

namespace TaskOps.Api.Infrastructure;

public sealed class JwtBearerOptionsSetup(
    IOptions<JwtOptions> jwtOptions,
    IJwtSigningKeyProvider signingKeyProvider) : IConfigureNamedOptions<JwtBearerOptions>
{
    public void Configure(JwtBearerOptions options) => Configure(Options.DefaultName, options);

    public void Configure(string? name, JwtBearerOptions options)
    {
        if (name != JwtBearerDefaults.AuthenticationScheme)
        {
            return;
        }

        var configuredOptions = jwtOptions.Value;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = configuredOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = configuredOptions.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = signingKeyProvider.SigningKey,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    }
}
