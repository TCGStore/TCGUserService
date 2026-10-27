using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;
using TcgStore.Users;
using TcgStore.Users.Auth;
using TcgStore.Users.Data;
using TcgStore.Users.Grpc;

var config = UsersConfig.FromEnvironment();
var builder = WebApplication.CreateBuilder(args);

// gRPC without TLS (h2c) needs an HTTP/2-only endpoint; TLS ends at the cluster edge.
builder.WebHost.ConfigureKestrel(kestrel =>
    kestrel.ListenAnyIP(config.GrpcPort, listen => listen.Protocols = HttpProtocols.Http2));

builder.Services.AddSingleton(config);
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<TokenService>();
builder.Services.AddDbContext<UsersDbContext>(options =>
    options.UseNpgsql(config.PostgresConnection).UseSnakeCaseNamingConvention());

builder.Services.AddGrpc();
builder.Services.AddGrpcHealthChecks();
// Lets grpcurl and Postman discover the API without the .proto file.
builder.Services.AddGrpcReflection();

var app = builder.Build();

await DatabaseSetup.InitializeAsync(app.Services);

app.MapGrpcService<UsersGrpcService>();
app.MapGrpcHealthChecksService();
app.MapGrpcReflectionService();

app.Logger.LogInformation("users-service listening on port {Port}", config.GrpcPort);
await app.RunAsync();
