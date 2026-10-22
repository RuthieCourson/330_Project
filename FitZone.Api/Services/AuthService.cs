using System.Security.Cryptography;
using FitZone.Api.Data;

namespace FitZone.Api.Services;

public record CurrentUser(int UserId, string Role, string FirstName, string LastName, string Email)
{
    public bool IsTrainer => Role == "trainer";
    public bool IsCustomer => Role == "customer";
}

/// <summary>
/// Password hashing (PBKDF2-SHA256) and token-based login sessions stored in
/// the auth_session table. The front end sends the token as
/// "Authorization: Bearer {token}".
/// </summary>
public class AuthService(Db db)
{
    private const int Iterations = 100_000;
    private static readonly TimeSpan SessionLength = TimeSpan.FromDays(7);

    // Stored format: iterations.base64Salt.base64Hash
    public static string HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, 32);
        return $"{Iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }

    public static bool VerifyPassword(string password, string stored)
    {
        var parts = stored.Split('.');
        if (parts.Length != 3 || !int.TryParse(parts[0], out var iterations)) return false;
        var salt = Convert.FromBase64String(parts[1]);
        var expected = Convert.FromBase64String(parts[2]);
        var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    public async Task<string> CreateSessionAsync(int userId)
    {
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        await db.ExecuteAsync(
            "INSERT INTO auth_session (token, user_id, expires_at) VALUES (@token, @userId, @expires)",
            new { token, userId, expires = DateTime.Now.Add(SessionLength) });
        return token;
    }

    public async Task EndSessionAsync(HttpContext http)
    {
        var token = ReadToken(http);
        if (token != null)
        {
            await db.ExecuteAsync("DELETE FROM auth_session WHERE token = @token", new { token });
        }
    }

    /// <summary>Returns the logged-in user, or null if the token is missing/expired.</summary>
    public async Task<CurrentUser?> GetUserAsync(HttpContext http)
    {
        var token = ReadToken(http);
        if (token == null) return null;

        var row = await db.QuerySingleAsync(@"
            SELECT u.user_id, u.role, u.first_name, u.last_name, u.email
            FROM   auth_session s
            JOIN   app_user u ON u.user_id = s.user_id
            WHERE  s.token = @token AND s.expires_at > NOW()", new { token });

        if (row == null) return null;
        return new CurrentUser(
            Convert.ToInt32(row["user_id"]),
            (string)row["role"]!,
            (string)row["first_name"]!,
            (string)row["last_name"]!,
            (string)row["email"]!);
    }

    private static string? ReadToken(HttpContext http)
    {
        var header = http.Request.Headers.Authorization.ToString();
        return header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ? header[7..].Trim() : null;
    }
}
