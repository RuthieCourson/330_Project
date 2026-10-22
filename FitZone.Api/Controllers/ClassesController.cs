using FitZone.Api.Data;
using FitZone.Api.Models;
using FitZone.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace FitZone.Api.Controllers;

[ApiController]
[Route("api/classes")]
public class ClassesController(Db db, AuthService auth, ScheduleService schedule) : ControllerBase
{
    private static readonly string[] ClassTypes = ["group", "private"];
    private static readonly string[] Difficulties = ["Beginner", "Intermediate", "Advanced"];

    // Whitelisted ORDER BY clauses (never put user text directly into SQL)
    private static readonly Dictionary<string, string> SortOptions = new()
    {
        ["next"]       = "next_session IS NULL, next_session",
        ["title"]      = "c.title",
        ["price_asc"]  = "c.price, c.title",
        ["price_desc"] = "c.price DESC, c.title",
        ["popular"]    = "total_bookings DESC, c.title",
        ["duration"]   = "c.duration_min, c.title",
    };

    /// <summary>
    /// Search & filter classes.
    /// GET /api/classes?q=yoga&amp;category=1&amp;type=group&amp;difficulty=Beginner&amp;trainer=1&amp;from=2026-10-01&amp;to=2026-10-31&amp;sort=price_asc
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Search(string? q, int? category, string? type, string? difficulty,
        int? trainer, DateTime? from, DateTime? to, string? sort)
    {
        var orderBy = SortOptions.GetValueOrDefault(sort ?? "next", SortOptions["next"]);
        var dateFiltered = from != null || to != null;

        var sql = $@"
            SELECT  c.class_id, c.title, c.description, c.class_type, c.difficulty,
                    c.duration_min, c.price, c.max_capacity,
                    cat.category_id, cat.name AS category,
                    c.trainer_id, CONCAT(u.first_name, ' ', u.last_name) AS trainer_name,
                    MIN(s.start_time)            AS next_session,
                    COUNT(DISTINCT s.session_id) AS upcoming_sessions,
                    (SELECT COUNT(*) FROM booking b
                     JOIN class_session bs ON bs.session_id = b.session_id
                     WHERE bs.class_id = c.class_id AND b.status IN ('confirmed','attended')) AS total_bookings
            FROM    fitness_class c
            JOIN    category cat ON cat.category_id = c.category_id
            JOIN    app_user u   ON u.user_id = c.trainer_id
            LEFT JOIN class_session s
                    ON  s.class_id = c.class_id
                    AND s.status = 'scheduled'
                    AND s.start_time > NOW()
                    AND (@from IS NULL OR s.start_time >= @from)
                    AND (@to   IS NULL OR s.start_time <  DATE_ADD(@to, INTERVAL 1 DAY))
            WHERE   c.is_active = TRUE
              AND   (@q IS NULL OR c.title LIKE @like OR c.description LIKE @like
                     OR cat.name LIKE @like OR CONCAT(u.first_name, ' ', u.last_name) LIKE @like)
              AND   (@category   IS NULL OR c.category_id = @category)
              AND   (@type       IS NULL OR c.class_type  = @type)
              AND   (@difficulty IS NULL OR c.difficulty  = @difficulty)
              AND   (@trainer    IS NULL OR c.trainer_id  = @trainer)
            GROUP BY c.class_id, c.title, c.description, c.class_type, c.difficulty, c.duration_min,
                     c.price, c.max_capacity, cat.category_id, cat.name, c.trainer_id, u.first_name, u.last_name
            {(dateFiltered ? "HAVING COUNT(s.session_id) > 0" : "")}
            ORDER BY {orderBy}";

        var qTrim = string.IsNullOrWhiteSpace(q) ? null : q.Trim();
        var rows = await db.QueryAsync(sql, new
        {
            q = qTrim,
            like = "%" + qTrim + "%",
            category,
            type = string.IsNullOrWhiteSpace(type) ? null : type,
            difficulty = string.IsNullOrWhiteSpace(difficulty) ? null : difficulty,
            trainer,
            from,
            to
        });
        return Ok(rows);
    }

    /// <summary>Class details plus its upcoming sessions with seats left.</summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id)
    {
        var cls = await db.QuerySingleAsync(@"
            SELECT  c.*, cat.name AS category,
                    CONCAT(u.first_name, ' ', u.last_name) AS trainer_name,
                    t.bio AS trainer_bio, t.specialty AS trainer_specialty,
                    t.years_experience AS trainer_years, t.certifications AS trainer_certifications
            FROM    fitness_class c
            JOIN    category cat ON cat.category_id = c.category_id
            JOIN    trainer t    ON t.trainer_id = c.trainer_id
            JOIN    app_user u   ON u.user_id = c.trainer_id
            WHERE   c.class_id = @id", new { id });
        if (cls == null) return NotFound(new { message = "Class not found." });

        var user = await auth.GetUserAsync(HttpContext);
        var sessions = await db.QueryAsync(@"
            SELECT  v.session_id, v.start_time, v.end_time, v.status, v.room_name,
                    v.max_capacity, v.booked_count, v.seats_left,
                    mb.booking_id AS my_booking_id
            FROM    v_session_availability v
            LEFT JOIN booking mb ON mb.session_id = v.session_id
                                AND mb.customer_id = @customerId
                                AND mb.status = 'confirmed'
            WHERE   v.class_id = @id AND v.start_time > NOW() AND v.status = 'scheduled'
            ORDER BY v.start_time", new { id, customerId = user?.IsCustomer == true ? user.UserId : (int?)null });

        cls["sessions"] = sessions;
        return Ok(cls);
    }

    [HttpPost]
    public async Task<IActionResult> Create(ClassRequest req)
    {
        var user = await auth.GetUserAsync(HttpContext);
        if (user == null || !user.IsTrainer) return StatusCode(403, new { message = "Only trainers can create classes." });

        var error = Validate(req);
        if (error != null) return BadRequest(new { message = error });

        var id = await db.InsertAsync(@"
            INSERT INTO fitness_class (trainer_id, category_id, title, description, class_type, difficulty,
                                       duration_min, price, max_capacity, is_active)
            VALUES (@TrainerId, @CategoryId, @Title, @Description, @ClassType, @Difficulty,
                    @DurationMin, @Price, @MaxCapacity, @IsActive)",
            new { TrainerId = user.UserId, req.CategoryId, Title = req.Title.Trim(), req.Description, req.ClassType,
                  req.Difficulty, req.DurationMin, req.Price, req.MaxCapacity, req.IsActive });

        return await Get((int)id);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, ClassRequest req)
    {
        var user = await auth.GetUserAsync(HttpContext);
        var ownerCheck = await CheckOwnerAsync(id, user);
        if (ownerCheck != null) return ownerCheck;

        var error = Validate(req);
        if (error != null) return BadRequest(new { message = error });

        // Can't shrink capacity below what's already booked on an upcoming session
        var maxBooked = Convert.ToInt32(await db.ScalarAsync(@"
            SELECT COALESCE(MAX(booked_count), 0) FROM v_session_availability
            WHERE class_id = @id AND start_time > NOW() AND status = 'scheduled'", new { id }));
        if (req.MaxCapacity < maxBooked)
            return BadRequest(new { message = $"An upcoming session already has {maxBooked} bookings. Capacity can't be lower than that." });

        await db.ExecuteAsync(@"
            UPDATE fitness_class
            SET    category_id = @CategoryId, title = @Title, description = @Description,
                   class_type = @ClassType, difficulty = @Difficulty, duration_min = @DurationMin,
                   price = @Price, max_capacity = @MaxCapacity, is_active = @IsActive
            WHERE  class_id = @id",
            new { id, req.CategoryId, Title = req.Title.Trim(), req.Description, req.ClassType, req.Difficulty,
                  req.DurationMin, req.Price, req.MaxCapacity, req.IsActive });

        return await Get(id);
    }

    /// <summary>
    /// Deletes a class. If it has any booking history, it is archived instead
    /// (set inactive, future sessions and bookings cancelled) so customer
    /// booking history is preserved.
    /// </summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var user = await auth.GetUserAsync(HttpContext);
        var ownerCheck = await CheckOwnerAsync(id, user);
        if (ownerCheck != null) return ownerCheck;

        var bookingCount = Convert.ToInt32(await db.ScalarAsync(@"
            SELECT COUNT(*) FROM booking b JOIN class_session s ON s.session_id = b.session_id
            WHERE s.class_id = @id", new { id }));

        if (bookingCount == 0)
        {
            await db.ExecuteAsync("DELETE FROM fitness_class WHERE class_id = @id", new { id });
            return Ok(new { message = "Class deleted." });
        }

        await using var conn = await db.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();
        await db.ExecuteAsync(@"
            UPDATE booking b JOIN class_session s ON s.session_id = b.session_id
            SET    b.status = 'cancelled', b.cancelled_at = NOW()
            WHERE  s.class_id = @id AND s.start_time > NOW() AND b.status = 'confirmed'", new { id }, conn, tx);
        await db.ExecuteAsync(@"
            UPDATE class_session SET status = 'cancelled'
            WHERE  class_id = @id AND start_time > NOW() AND status = 'scheduled'", new { id }, conn, tx);
        await db.ExecuteAsync("UPDATE fitness_class SET is_active = FALSE WHERE class_id = @id", new { id }, conn, tx);
        await tx.CommitAsync();

        return Ok(new { message = "Class has booking history, so it was archived and its upcoming sessions were cancelled." });
    }

    // ----- Sessions belonging to a class -----

    [HttpPost("{id:int}/sessions")]
    public async Task<IActionResult> AddSession(int id, SessionRequest req)
    {
        var user = await auth.GetUserAsync(HttpContext);
        var ownerCheck = await CheckOwnerAsync(id, user);
        if (ownerCheck != null) return ownerCheck;

        if (req.StartTime <= DateTime.Now) return BadRequest(new { message = "Session must start in the future." });
        if (req.RepeatWeeks < 1 || req.RepeatWeeks > 12) return BadRequest(new { message = "Repeat must be between 1 and 12 weeks." });

        var cls = (await db.QuerySingleAsync("SELECT duration_min, max_capacity FROM fitness_class WHERE class_id = @id", new { id }))!;
        var duration = Convert.ToInt32(cls["duration_min"]);

        if (req.RoomId != null)
        {
            var roomCap = await db.ScalarAsync("SELECT capacity FROM room WHERE room_id = @RoomId", new { req.RoomId });
            if (roomCap == null) return BadRequest(new { message = "Room not found." });
            if (Convert.ToInt32(roomCap) < Convert.ToInt32(cls["max_capacity"]))
                return BadRequest(new { message = $"That room only holds {roomCap} people, but the class capacity is {cls["max_capacity"]}." });
        }

        await using var conn = await db.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();
        var created = 0;
        for (var week = 0; week < req.RepeatWeeks; week++)
        {
            var start = req.StartTime.AddDays(7 * week);
            var end = start.AddMinutes(duration);

            var conflict = await schedule.FindConflictAsync(user!.UserId, req.RoomId, start, end, null, conn, tx);
            if (conflict != null)
            {
                await tx.RollbackAsync();
                return Conflict(new { message = $"{start:MMM d, h:mm tt}: {conflict}" });
            }

            await db.ExecuteAsync(@"
                INSERT INTO class_session (class_id, room_id, start_time, end_time)
                VALUES (@id, @RoomId, @start, @end)", new { id, req.RoomId, start, end }, conn, tx);
            created++;
        }
        await tx.CommitAsync();
        return Ok(new { message = $"{created} session(s) added." });
    }

    private async Task<IActionResult?> CheckOwnerAsync(int classId, CurrentUser? user)
    {
        if (user == null || !user.IsTrainer) return StatusCode(403, new { message = "Only trainers can manage classes." });
        var owner = await db.ScalarAsync("SELECT trainer_id FROM fitness_class WHERE class_id = @classId", new { classId });
        if (owner == null) return NotFound(new { message = "Class not found." });
        if (Convert.ToInt32(owner) != user.UserId) return StatusCode(403, new { message = "You can only manage your own classes." });
        return null;
    }

    private static string? Validate(ClassRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Title)) return "Title is required.";
        if (!ClassTypes.Contains(req.ClassType)) return "Class type must be group or private.";
        if (!Difficulties.Contains(req.Difficulty)) return "Difficulty must be Beginner, Intermediate, or Advanced.";
        if (req.DurationMin < 15 || req.DurationMin > 240) return "Duration must be between 15 and 240 minutes.";
        if (req.Price < 0) return "Price can't be negative.";
        if (req.ClassType == "private") req.MaxCapacity = 1;
        if (req.MaxCapacity < 1 || req.MaxCapacity > 100) return "Capacity must be between 1 and 100.";
        return null;
    }
}
