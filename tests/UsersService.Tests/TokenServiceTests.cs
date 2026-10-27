using System.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using TcgStore.Users.Auth;
using TcgStore.Users.Data;

namespace TcgStore.Users.Tests;

public class TokenServiceTests
{
    private const string Secret = "test-secret-with-at-least-32-bytes!!";
    private static readonly DateTimeOffset Now = new(2026, 10, 27, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Issue_SignsTokenTheBffCanVerifyWithTheSharedSecret()
    {
        var config = new UsersConfig(50054, "unused", Secret, TimeSpan.FromHours(24), null, null);
        var service = new TokenService(config, new FixedTimeProvider(Now));
        var user = new UserEntity { Id = "usr_42", Email = "ash@example.com", Role = Roles.Admin };

        var (token, expiresAt) = service.Issue(user);

        var result = await new JsonWebTokenHandler().ValidateTokenAsync(token, new TokenValidationParameters
        {
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Secret)),
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = false,
        });

        Assert.True(result.IsValid, result.Exception?.Message);
        var jwt = (JsonWebToken)result.SecurityToken;
        Assert.Equal("usr_42", jwt.Subject);
        Assert.Equal("ash@example.com", jwt.GetClaim("email").Value);
        Assert.Equal("admin", jwt.GetClaim("role").Value);
        Assert.Equal(Now.AddHours(24), expiresAt);
        Assert.Equal(expiresAt.ToUnixTimeSeconds().ToString(), jwt.GetClaim("exp").Value);
        Assert.Equal(Now.ToUnixTimeSeconds().ToString(), jwt.GetClaim("iat").Value);
    }
}
