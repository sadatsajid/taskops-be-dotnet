using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace TaskOps.Infrastructure.Security;

public sealed class JwtSigningKeyProvider(IOptions<JwtOptions> options) : IJwtSigningKeyProvider
{
    private readonly SymmetricSecurityKey _signingKey = new(Encoding.UTF8.GetBytes(options.Value.SigningKey));

    public SymmetricSecurityKey SigningKey => _signingKey;

    public SigningCredentials CreateSigningCredentials() =>
        new(_signingKey, SecurityAlgorithms.HmacSha256);
}
