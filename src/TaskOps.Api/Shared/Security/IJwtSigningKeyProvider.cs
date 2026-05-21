using Microsoft.IdentityModel.Tokens;

namespace TaskOps.Api.Shared.Security;

public interface IJwtSigningKeyProvider
{
    SymmetricSecurityKey SigningKey { get; }

    SigningCredentials CreateSigningCredentials();
}
