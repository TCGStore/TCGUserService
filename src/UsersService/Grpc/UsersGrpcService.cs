using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using TcgStore.Users.Auth;
using TcgStore.Users.Data;

namespace TcgStore.Users.Grpc;

public sealed class UsersGrpcService(
    UsersDbContext db,
    TokenService tokens,
    TimeProvider clock,
    ILogger<UsersGrpcService> logger) : UsersService.UsersServiceBase
{
    // Verified against when the email is unknown, so a wrong email and a wrong password take the same time.
    private static readonly string DummyHash = BCrypt.Net.BCrypt.HashPassword("not-a-real-password");

    public override async Task<AuthResponse> Register(RegisterRequest request, ServerCallContext context)
    {
        var email = Validation.ValidEmail(request.Email);
        Validation.CheckPassword(request.Password);
        var name = Validation.ValidName(request.Name);

        if (await db.Users.AnyAsync(u => u.Email == email, context.CancellationToken))
            throw EmailTaken();

        var user = new UserEntity
        {
            Id = UserEntity.NewId(),
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Name = name,
            Role = Roles.Customer,
            CreatedAt = clock.GetUtcNow(),
        };
        db.Users.Add(user);

        try
        {
            await db.SaveChangesAsync(context.CancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Two registrations with the same email raced past the AnyAsync check.
            throw EmailTaken();
        }

        logger.LogInformation("Registered user {UserId}", user.Id);
        return Authenticated(user);
    }

    public override async Task<AuthResponse> Login(LoginRequest request, ServerCallContext context)
    {
        var email = Validation.NormalizeEmail(request.Email);
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Email == email, context.CancellationToken);

        var passwordOk = BCrypt.Net.BCrypt.Verify(request.Password, user?.PasswordHash ?? DummyHash);
        if (user is null || !passwordOk)
            throw new RpcException(new Status(StatusCode.Unauthenticated, "invalid email or password"));

        return Authenticated(user);
    }

    public override async Task<User> GetProfile(GetProfileRequest request, ServerCallContext context)
    {
        var user = await FindUser(request.UserId, context.CancellationToken);
        return UserMapper.ToProto(user);
    }

    // Partial update: an empty name or a missing address keeps the current value.
    public override async Task<User> UpdateProfile(UpdateProfileRequest request, ServerCallContext context)
    {
        var user = await FindUser(request.UserId, context.CancellationToken);

        if (!string.IsNullOrWhiteSpace(request.Name))
            user.Name = Validation.ValidName(request.Name);
        if (request.ShippingAddress is not null)
            user.ShippingAddress = Validation.ValidAddress(request.ShippingAddress);

        await db.SaveChangesAsync(context.CancellationToken);
        return UserMapper.ToProto(user);
    }

    private async Task<UserEntity> FindUser(string userId, CancellationToken ct)
    {
        var user = await db.Users.FindAsync([Validation.RequiredUserId(userId)], ct);
        return user ?? throw new RpcException(new Status(StatusCode.NotFound, $"user \"{userId}\" not found"));
    }

    private AuthResponse Authenticated(UserEntity user)
    {
        var (token, expiresAt) = tokens.Issue(user);
        return new AuthResponse
        {
            AccessToken = token,
            ExpiresAt = expiresAt.ToUnixTimeSeconds(),
            User = UserMapper.ToProto(user),
        };
    }

    private static RpcException EmailTaken() =>
        new(new Status(StatusCode.AlreadyExists, "email is already registered"));
}
