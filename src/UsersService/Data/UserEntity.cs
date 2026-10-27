namespace TcgStore.Users.Data;

public static class Roles
{
    // Same strings as the JWT "role" claim.
    public const string Customer = "customer";
    public const string Admin = "admin";
}

public class UserEntity
{
    public string Id { get; set; } = "";
    public string Email { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string Name { get; set; } = "";
    public string Role { get; set; } = Roles.Customer;
    public AddressEntity? ShippingAddress { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public static string NewId() => "usr_" + Guid.NewGuid().ToString("N")[..12];
}

public class AddressEntity
{
    public string RecipientName { get; set; } = "";
    public string Street { get; set; } = "";
    public string City { get; set; } = "";
    public string Region { get; set; } = "";
    public string PostalCode { get; set; } = "";
    public string Country { get; set; } = "";
}
