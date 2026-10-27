using System.Globalization;
using TcgStore.Users.Data;

namespace TcgStore.Users.Grpc;

internal static class UserMapper
{
    public static User ToProto(UserEntity user) => new()
    {
        Id = user.Id,
        Email = user.Email,
        Name = user.Name,
        Role = user.Role == Roles.Admin ? Role.Admin : Role.Customer,
        ShippingAddress = user.ShippingAddress is { } address ? ToProto(address) : null,
        CreatedAt = user.CreatedAt.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture),
    };

    private static Address ToProto(AddressEntity address) => new()
    {
        RecipientName = address.RecipientName,
        Street = address.Street,
        City = address.City,
        Region = address.Region,
        PostalCode = address.PostalCode,
        Country = address.Country,
    };
}
