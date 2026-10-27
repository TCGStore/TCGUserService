using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using TcgStore.Users.Auth;
using TcgStore.Users.Data;
using TcgStore.Users.Grpc;

namespace TcgStore.Users.Tests;

// EF Core in-memory: fast and needs no PostgreSQL, but doesn't enforce the unique email index.
public class UsersGrpcServiceTests
{
    private readonly UsersDbContext _db;
    private readonly UsersGrpcService _service;
    private readonly TestServerCallContext _ctx = new();

    public UsersGrpcServiceTests()
    {
        _db = new UsersDbContext(new DbContextOptionsBuilder<UsersDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

        var clock = new FixedTimeProvider(new DateTimeOffset(2026, 10, 27, 12, 0, 0, TimeSpan.Zero));
        var config = new UsersConfig(50054, "unused", "test-secret-with-at-least-32-bytes!!", TimeSpan.FromHours(24), null, null);
        _service = new UsersGrpcService(_db, new TokenService(config, clock), clock, NullLogger<UsersGrpcService>.Instance);
    }

    private Task<AuthResponse> RegisterAsh() => _service.Register(
        new RegisterRequest { Email = " Ash@Example.com ", Password = "pikachu123", Name = "Ash Ketchum" }, _ctx);

    [Fact]
    public async Task Register_CreatesCustomerWithHashedPassword()
    {
        var response = await RegisterAsh();

        Assert.NotEmpty(response.AccessToken);
        Assert.StartsWith("usr_", response.User.Id);
        Assert.Equal("ash@example.com", response.User.Email);
        Assert.Equal(Role.Customer, response.User.Role);
        Assert.Null(response.User.ShippingAddress);
        Assert.Equal("2026-10-27T12:00:00Z", response.User.CreatedAt);

        var stored = await _db.Users.SingleAsync();
        Assert.NotEqual("pikachu123", stored.PasswordHash);
        Assert.True(BCrypt.Net.BCrypt.Verify("pikachu123", stored.PasswordHash));
    }

    [Fact]
    public async Task Register_RejectsTakenEmailIgnoringCase()
    {
        await RegisterAsh();

        var ex = await Assert.ThrowsAsync<RpcException>(() => _service.Register(
            new RegisterRequest { Email = "ASH@example.com", Password = "another123", Name = "Other Ash" }, _ctx));

        Assert.Equal(StatusCode.AlreadyExists, ex.StatusCode);
    }

    [Theory]
    [InlineData("not-an-email", "pikachu123", "Ash")]
    [InlineData("Ash <ash@example.com>", "pikachu123", "Ash")]
    [InlineData("ash@example.com", "short", "Ash")]
    [InlineData("ash@example.com", "pikachu123", "   ")]
    public async Task Register_RejectsInvalidInput(string email, string password, string name)
    {
        var ex = await Assert.ThrowsAsync<RpcException>(() => _service.Register(
            new RegisterRequest { Email = email, Password = password, Name = name }, _ctx));

        Assert.Equal(StatusCode.InvalidArgument, ex.StatusCode);
    }

    [Fact]
    public async Task Login_ReturnsTokenForValidCredentials()
    {
        var registered = await RegisterAsh();

        var response = await _service.Login(new LoginRequest { Email = "ASH@example.com", Password = "pikachu123" }, _ctx);

        Assert.Equal(registered.User.Id, response.User.Id);
        Assert.NotEmpty(response.AccessToken);
    }

    [Fact]
    public async Task Login_GivesSameErrorForWrongPasswordAndUnknownEmail()
    {
        await RegisterAsh();

        var wrongPassword = await Assert.ThrowsAsync<RpcException>(() =>
            _service.Login(new LoginRequest { Email = "ash@example.com", Password = "charmander1" }, _ctx));
        var unknownEmail = await Assert.ThrowsAsync<RpcException>(() =>
            _service.Login(new LoginRequest { Email = "misty@example.com", Password = "pikachu123" }, _ctx));

        Assert.Equal(StatusCode.Unauthenticated, wrongPassword.StatusCode);
        Assert.Equal(wrongPassword.Status.Detail, unknownEmail.Status.Detail);
    }

    [Fact]
    public async Task UpdateProfile_KeepsNameWhenOnlyAddressIsSent()
    {
        var userId = (await RegisterAsh()).User.Id;

        var updated = await _service.UpdateProfile(new UpdateProfileRequest
        {
            UserId = userId,
            ShippingAddress = new Address
            {
                RecipientName = "Ash Ketchum", Street = "Route 1 #10", City = "Pallet Town", Country = "CL",
            },
        }, _ctx);

        Assert.Equal("Ash Ketchum", updated.Name);
        Assert.Equal("Pallet Town", updated.ShippingAddress.City);

        var profile = await _service.GetProfile(new GetProfileRequest { UserId = userId }, _ctx);
        Assert.Equal("Route 1 #10", profile.ShippingAddress.Street);
    }

    [Fact]
    public async Task UpdateProfile_RejectsAddressWithoutStreet()
    {
        var userId = (await RegisterAsh()).User.Id;

        var ex = await Assert.ThrowsAsync<RpcException>(() => _service.UpdateProfile(new UpdateProfileRequest
        {
            UserId = userId,
            ShippingAddress = new Address { RecipientName = "Ash", City = "Pallet Town", Country = "CL" },
        }, _ctx));

        Assert.Equal(StatusCode.InvalidArgument, ex.StatusCode);
    }

    [Fact]
    public async Task GetProfile_ReturnsNotFoundForUnknownUser()
    {
        var ex = await Assert.ThrowsAsync<RpcException>(() =>
            _service.GetProfile(new GetProfileRequest { UserId = "usr_missing" }, _ctx));

        Assert.Equal(StatusCode.NotFound, ex.StatusCode);
    }
}
