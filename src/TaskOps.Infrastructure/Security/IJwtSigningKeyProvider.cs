using Microsoft.IdentityModel.Tokens;

namespace TaskOps.Infrastructure.Security;

public interface IJwtSigningKeyProvider
{
    SymmetricSecurityKey SigningKey { get; }

    SigningCredentials CreateSigningCredentials();
}
