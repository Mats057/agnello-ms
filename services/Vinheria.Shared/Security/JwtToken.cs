using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Vinheria.Shared.Security;

public static class JwtToken
{
    public static string Create(string secret, string subject, string role, TimeSpan lifetime)
    {
        var header = Encode(JsonSerializer.Serialize(new { alg = "HS256", typ = "JWT" }));
        var payload = Encode(JsonSerializer.Serialize(new
        {
            sub = subject,
            role,
            exp = DateTimeOffset.UtcNow.Add(lifetime).ToUnixTimeSeconds()
        }));
        var signed = $"{header}.{payload}";

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        return $"{signed}.{Encode(hmac.ComputeHash(Encoding.UTF8.GetBytes(signed)))}";
    }

    public static bool IsValid(string? authorization, string secret, string requiredRole)
    {
        if (string.IsNullOrWhiteSpace(authorization))
        {
            return false;
        }

        var separator = authorization.IndexOf(' ');
        if (separator <= 0 ||
            !authorization[..separator].Equals("Bearer", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var token = authorization[(separator + 1)..].Trim();
        var parts = token.Split('.');
        if (string.IsNullOrWhiteSpace(token) || parts.Length != 3)
        {
            return false;
        }

        try
        {
            var payload = JsonSerializer.Deserialize<Payload>(Decode(parts[1]));
            if (payload is null ||
                payload.Expiration < DateTimeOffset.UtcNow.ToUnixTimeSeconds() ||
                !string.Equals(payload.Role, requiredRole, StringComparison.Ordinal))
            {
                return false;
            }

            var signed = $"{parts[0]}.{parts[1]}";
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
            var expected = Encode(hmac.ComputeHash(Encoding.UTF8.GetBytes(signed)));

            return CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(expected),
                Encoding.UTF8.GetBytes(parts[2]));
        }
        catch (JsonException)
        {
            return false;
        }
        catch (FormatException)
        {
            return false;
        }

        static byte[] Decode(string value)
        {
            var padded = value.Replace('-', '+').Replace('_', '/');
            padded += new string('=', (4 - padded.Length % 4) % 4);
            return Convert.FromBase64String(padded);
        }
    }

    private static string Encode(string value) => Encode(Encoding.UTF8.GetBytes(value));

    private static string Encode(byte[] value) =>
        Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private sealed record Payload(
        [property: JsonPropertyName("role")] string Role,
        [property: JsonPropertyName("exp")] long Expiration);
}
