using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using TcgStore.Users.Grpc;

namespace TcgStore.Users.Data;

public static class DatabaseSetup
{
    public static async Task InitializeAsync(IServiceProvider services, CancellationToken ct = default)
    {
        await using var scope = services.CreateAsyncScope();
        var provider = scope.ServiceProvider;
        var db = provider.GetRequiredService<UsersDbContext>();

        // Creates the database and tables if missing, but never alters an existing schema (no migrations yet).
        await db.Database.EnsureCreatedAsync(ct);

        await SeedAdminAsync(
            db,
            provider.GetRequiredService<UsersConfig>(),
            provider.GetRequiredService<TimeProvider>(),
            provider.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(DatabaseSetup)),
            ct);
    }

    // Register only creates customers, so the first admin comes from ADMIN_EMAIL / ADMIN_PASSWORD.
    private static async Task SeedAdminAsync(
        UsersDbContext db, UsersConfig config, TimeProvider clock, ILogger logger, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(config.AdminEmail) || string.IsNullOrEmpty(config.AdminPassword))
            return;

        string email;
        try
        {
            email = Validation.ValidEmail(config.AdminEmail);
            Validation.CheckPassword(config.AdminPassword);
        }
        catch (RpcException ex)
        {
            throw new InvalidOperationException($"ADMIN_EMAIL / ADMIN_PASSWORD: {ex.Status.Detail}");
        }

        if (await db.Users.AnyAsync(u => u.Email == email, ct))
            return;

        db.Users.Add(new UserEntity
        {
            Id = UserEntity.NewId(),
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(config.AdminPassword),
            Name = "Admin",
            Role = Roles.Admin,
            CreatedAt = clock.GetUtcNow(),
        });
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Created admin user {Email}", email);
    }
}
