using FitZone.Api.Data;
using FitZone.Api.Models;
using FitZone.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace FitZone.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(Db db, AuthService auth) : ControllerBase
{
    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest req)
    {
        req.Email = req.Email.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(req.FirstName) || string.IsNullOrWhiteSpace(req.LastName))
            return BadRequest(new { message = "First and last name are required." });
        if (!req.Email.Contains('@'))
            return BadRequest(new { message = "Please enter a valid email address." });
        if (req.Password.Length < 8)
            return BadRequest(new { message = "Password must be at least 8 characters." });
        if (req.Role != "customer" && req.Role != "trainer")
            return BadRequest(new { message = "Role must be customer or trainer." });

        var exists = await db.ScalarAsync("SELECT COUNT(*) FROM app_user WHERE email = @Email", new { req.Email });
        if (Convert.ToInt32(exists) > 0)
            return Conflict(new { message = "An account with that email already exists." });

        await using var conn = await db.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();

        var userId = (int)await db.InsertAsync(@"
            INSERT INTO app_user (first_name, last_name, email, phone, password_hash, role)
            VALUES (@FirstName, @LastName, @Email, @Phone, @Hash, @Role)",
            new { FirstName = req.FirstName.Trim(), LastName = req.LastName.Trim(), req.Email, req.Phone,
                  Hash = AuthService.HashPassword(req.Password), req.Role }, conn, tx);

        // Every user also gets a row in its subtype table
        var subtypeSql = req.Role == "trainer"
            ? "INSERT INTO trainer (trainer_id) VALUES (@userId)"
            : "INSERT INTO customer (customer_id) VALUES (@userId)";
        await db.ExecuteAsync(subtypeSql, new { userId }, conn, tx);

        await tx.CommitAsync();

        var token = await auth.CreateSessionAsync(userId);
        return Ok(new { token, user = new { user_id = userId, role = req.Role, first_name = req.FirstName, last_name = req.LastName, email = req.Email } });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest req)
    {
        var row = await db.QuerySingleAsync(
            "SELECT user_id, first_name, last_name, email, role, password_hash FROM app_user WHERE email = @email",
            new { email = req.Email.Trim().ToLowerInvariant() });

        if (row == null || !AuthService.VerifyPassword(req.Password, (string)row["password_hash"]!))
            return Unauthorized(new { message = "Invalid email or password." });

        row.Remove("password_hash");
        var token = await auth.CreateSessionAsync(Convert.ToInt32(row["user_id"]));
        return Ok(new { token, user = row });
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await auth.EndSessionAsync(HttpContext);
        return NoContent();
    }

    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        var user = await auth.GetUserAsync(HttpContext);
        if (user == null) return Unauthorized(new { message = "Not logged in." });
        return Ok(new { user_id = user.UserId, role = user.Role, first_name = user.FirstName, last_name = user.LastName, email = user.Email });
    }
}
