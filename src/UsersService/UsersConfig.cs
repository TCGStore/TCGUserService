using System.Text;

namespace TcgStore.Users;

public sealed record UsersConfig(
    int GrpcPort,
    string PostgresConnection,
    string JwtSecret,
    TimeSpan TokenLifetime,
    string? AdminEmail,
    string? AdminPassword)
{
    // HS256 rejects keys shorter than 256 bits.
    private const int MinJwtSecretBytes = 32;

    public static UsersConfig FromEnvironment()
    {
        var postgres = Environment.GetEnvironmentVariable("POSTGRES_CONNECTION");
        if (string.IsNullOrWhiteSpace(postgres))
        {
            throw new InvalidOperationException(
                "POSTGRES_CONNECTION is required, e.g. Host=localhost;Database=users;Username=tcg;Password=tcg");
        }

        var jwtSecret = Environment.GetEnvironmentVariable("JWT_SECRET") ?? "";
        if (Encoding.UTF8.GetByteCount(jwtSecret) < MinJwtSecretBytes)
        {
            throw new InvalidOperationException(
                $"JWT_SECRET must be at least {MinJwtSecretBytes} bytes; generate one with: openssl rand -base64 48");
        }

        return new UsersConfig(
            GrpcPort: int.Parse(GetEnv("GRPC_PORT", "50054")),
            PostgresConnection: postgres,
            JwtSecret: jwtSecret,
            TokenLifetime: TimeSpan.FromHours(24),
            AdminEmail: Environment.GetEnvironmentVariable("ADMIN_EMAIL"),
            AdminPassword: Environment.GetEnvironmentVariable("ADMIN_PASSWORD"));
    }

    private static string GetEnv(string key, string fallback) =>
        Environment.GetEnvironmentVariable(key) is { Length: > 0 } value ? value : fallback;
}
