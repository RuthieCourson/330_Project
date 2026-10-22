using FitZone.Api.Data;
using Microsoft.AspNetCore.Mvc;

namespace FitZone.Api.Controllers;

/// <summary>Drop-down data for the front end (categories, rooms, trainers).</summary>
[ApiController]
[Route("api/lookups")]
public class LookupsController(Db db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var categories = await db.QueryAsync("SELECT category_id, name, description FROM category ORDER BY name");
        var rooms = await db.QueryAsync("SELECT room_id, name, capacity FROM room ORDER BY name");
        var trainers = await db.QueryAsync(@"
            SELECT t.trainer_id, CONCAT(u.first_name, ' ', u.last_name) AS name, t.specialty, t.years_experience, t.bio
            FROM   trainer t JOIN app_user u ON u.user_id = t.trainer_id
            ORDER BY u.last_name");
        return Ok(new { categories, rooms, trainers });
    }
}
