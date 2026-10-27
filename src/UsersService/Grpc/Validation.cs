using System.Net.Mail;
using System.Text;
using Grpc.Core;
using TcgStore.Users.Data;

namespace TcgStore.Users.Grpc;

internal static class Validation
{
    private const int MaxEmailLength = 254;
    private const int MinPasswordLength = 8;
    // bcrypt ignores everything after 72 bytes, so longer passwords would match on a prefix.
    private const int MaxPasswordBytes = 72;
    private const int MaxNameLength = 100;
    private const int MaxAddressFieldLength = 200;

    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    public static string ValidEmail(string email)
    {
        var normalized = NormalizeEmail(email);
        // MailAddress also accepts "Ash <ash@example.com>"; only the bare address is allowed.
        if (normalized.Length > MaxEmailLength
            || !MailAddress.TryCreate(normalized, out var parsed)
            || parsed.Address != normalized)
        {
            throw Invalid("email is not valid");
        }
        return normalized;
    }

    public static void CheckPassword(string password)
    {
        if (password.Length < MinPasswordLength)
            throw Invalid($"password must have at least {MinPasswordLength} characters");
        if (Encoding.UTF8.GetByteCount(password) > MaxPasswordBytes)
            throw Invalid($"password can't be longer than {MaxPasswordBytes} bytes");
    }

    public static string ValidName(string name)
    {
        var trimmed = name.Trim();
        if (trimmed.Length == 0)
            throw Invalid("name is required");
        if (trimmed.Length > MaxNameLength)
            throw Invalid($"name can't be longer than {MaxNameLength} characters");
        return trimmed;
    }

    public static AddressEntity ValidAddress(Address address) => new()
    {
        RecipientName = AddressField(address.RecipientName, "recipient_name", required: true),
        Street = AddressField(address.Street, "street", required: true),
        City = AddressField(address.City, "city", required: true),
        Region = AddressField(address.Region, "region", required: false),
        PostalCode = AddressField(address.PostalCode, "postal_code", required: false),
        Country = AddressField(address.Country, "country", required: true),
    };

    public static string RequiredUserId(string userId) =>
        string.IsNullOrWhiteSpace(userId) ? throw Invalid("user_id is required") : userId;

    private static string AddressField(string value, string field, bool required)
    {
        var trimmed = value.Trim();
        if (required && trimmed.Length == 0)
            throw Invalid($"shipping_address.{field} is required");
        if (trimmed.Length > MaxAddressFieldLength)
            throw Invalid($"shipping_address.{field} can't be longer than {MaxAddressFieldLength} characters");
        return trimmed;
    }

    private static RpcException Invalid(string message) => new(new Status(StatusCode.InvalidArgument, message));
}
