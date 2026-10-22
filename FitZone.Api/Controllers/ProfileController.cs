using FitZone.Api.Data;
using FitZone.Api.Models;
using FitZone.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace FitZone.Api.Controllers;

[ApiController]
[Route("api/profile")]
public class ProfileController(Db db, AuthService auth) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var user = await auth.GetUserAsync(HttpContext);
        if (user == null) return Unauthorized(new { message = "Please log in." });

        var profile = await db.QuerySingleAsync(@"
            SELECT u.user_id, u.first_name, u.last_name, u.email, u.phone, u.role, u.created_at,
                   c.date_of_birth, c.fitness_goal, c.emergency_contact,
                   t.bio, t.specialty, t.years_experience, t.certifications
            FROM   app_user u
            LEFT JOIN customer c ON c.customer_id = u.user_id
            LEFT JOIN trainer  t ON t.trainer_id  = u.user_id
            WHERE  u.user_id = @UserId", new { user.UserId });

        return Ok(profile);
    }

    [HttpPut]
    public async Task<IActionResult> Update(ProfileUpdateRequest req)
    {
        var user = await auth.GetUserAsync(HttpContext);
        if (user == null) return Unauthorized(new { message = "Please log in." });
        if (string.IsNullOrWhiteSpace(req.FirstName) || string.IsNullOrWhiteSpace(req.LastName))
            return BadRequest(new { message = "First and last name are required." });

        await using var conn = await db.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();

        await db.ExecuteAsync(
            "UPDATE app_user SET first_name = @FirstName, last_name = @LastName, phone = @Phone WHERE user_id = @UserId",
            new { req.FirstName, req.LastName, req.Phone, user.UserId }, conn, tx);

        if (user.IsCustomer)
        {
            await db.ExecuteAsync(@"
                UPDATE customer SET date_of_birth = @DateOfBirth, fitness_goal = @FitnessGoal,
                       emergency_contact = @EmergencyContact
                WHERE  customer_id = @UserId",
                new { req.DateOfBirth, req.FitnessGoal, req.EmergencyContact, user.UserId }, conn, tx);
        }
        else
        {
            await db.ExecuteAsync(@"
                UPDATE trainer SET bio = @Bio, specialty = @Specialty,
                       years_experience = @YearsExperience, certifications = @Certifications
                WHERE  trainer_id = @UserId",
                new { req.Bio, req.Specialty, YearsExperience = req.YearsExperience ?? 0, req.Certifications, user.UserId }, conn, tx);
        }

        await tx.CommitAsync();
        return await Get();
    }

    [HttpPut("password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest req)
    {
        var user = await auth.GetUserAsync(HttpContext);
        if (user == null) return Unauthorized(new { message = "Please log in." });

        var stored = (string?)await db.ScalarAsync("SELECT password_hash FROM app_user WHERE user_id = @UserId", new { user.UserId });
        if (stored == null || !AuthService.VerifyPassword(req.CurrentPassword, stored))
            return BadRequest(new { message = "Current password is incorrect." });
        if (req.NewPassword.Length < 8)
            return BadRequest(new { message = "New password must be at least 8 characters." });

        await db.ExecuteAsync("UPDATE app_user SET password_hash = @hash WHERE user_id = @UserId",
            new { hash = AuthService.HashPassword(req.NewPassword), user.UserId });
        return Ok(new { message = "Password updated." });
    }
}
