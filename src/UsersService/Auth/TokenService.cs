using System.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using TcgStore.Users.Data;

namespace TcgStore.Users.Auth;

public sealed class TokenService(UsersConfig config, TimeProvider clock)
{
    private readonly SigningCredentials _credentials = new(
        new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config.JwtSecret)),
        SecurityAlgorithms.HmacSha256);

    private readonly JsonWebTokenHandler _handler = new();

    public (string Token, DateTimeOffset ExpiresAt) Issue(UserEntity user)
    {
        // Whole seconds, so expires_at in the response equals the token's exp claim.
        var now = DateTimeOffset.FromUnixTimeSeconds(clock.GetUtcNow().ToUnixTimeSeconds());
        var expiresAt = now + config.TokenLifetime;

        var token = _handler.CreateToken(new SecurityTokenDescriptor
        {
            Claims = new Dictionary<string, object>
            {
                [JwtRegisteredClaimNames.Sub] = user.Id,
                [JwtRegisteredClaimNames.Email] = user.Email,
                ["role"] = user.Role,
            },
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expiresAt.UtcDateTime,
            SigningCredentials = _credentials,
        });

        return (token, expiresAt);
    }
}
