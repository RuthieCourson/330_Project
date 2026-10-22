using FitZone.Api.Data;
using FitZone.Api.Models;
using FitZone.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace FitZone.Api.Controllers;

/// <summary>
/// Trainer dashboard: the trainer's own classes, sessions, and the bookings
/// customers have made for them.
/// </summary>
[ApiController]
[Route("api/trainer")]
public class TrainerController(Db db, AuthService auth, ScheduleService schedule) : ControllerBase
{
    private static readonly string[] BookingStatuses = ["confirmed", "cancelled", "attended", "no_show"];

    [HttpGet("summary")]
    public async Task<IActionResult> Summary()
    {
        var user = await auth.GetUserAsync(HttpContext);
        if (user == null || !user.IsTrainer) return Forbidden();

        var row = await db.QuerySingleAsync(@"
            SELECT
              (SELECT COUNT(*) FROM fitness_class WHERE trainer_id = @id AND is_active = TRUE) AS active_classes,
              (SELECT COUNT(*) FROM class_session s JOIN fitness_class c ON c.class_id = s.class_id
               WHERE c.trainer_id = @id AND s.status = 'scheduled' AND s.start_time > NOW()) AS upcoming_sessions,
              (SELECT COUNT(*) FROM booking b JOIN class_session s ON s.session_id = b.session_id
               JOIN fitness_class c ON c.class_id = s.class_id
               WHERE c.trainer_id = @id AND b.status = 'confirmed' AND s.start_time > NOW()) AS upcoming_bookings,
              (SELECT COALESCE(SUM(c.price), 0) FROM booking b JOIN class_session s ON s.session_id = b.session_id
               JOIN fitness_class c ON c.class_id = s.class_id
               WHERE c.trainer_id = @id AND b.status = 'attended'
                 AND s.start_time >= DATE_SUB(NOW(), INTERVAL 30 DAY)) AS revenue_30_days", new { id = user.UserId });
        return Ok(row);
    }

    /// <summary>All of this trainer's classes (including inactive) with stats.</summary>
    [HttpGet("classes")]
    public async Task<IActionResult> Classes()
    {
        var user = await auth.GetUserAsync(HttpContext);
        if (user == null || !user.IsTrainer) return Forbidden();

        var rows = await db.QueryAsync(@"
            SELECT c.class_id, c.title, c.description, c.class_type, c.difficulty, c.duration_min,
                   c.price, c.max_capacity, c.is_active, c.category_id, cat.name AS category,
                   SUM(s.status = 'scheduled' AND s.start_time > NOW()) AS upcoming_sessions,
                   MIN(CASE WHEN s.status = 'scheduled' AND s.start_time > NOW() THEN s.start_time END) AS next_session
            FROM   fitness_class c
            JOIN   category cat ON cat.category_id = c.category_id
            LEFT JOIN class_session s ON s.class_id = c.class_id
            WHERE  c.trainer_id = @id
            GROUP BY c.class_id, c.title, c.description, c.class_type, c.difficulty, c.duration_min,
                     c.price, c.max_capacity, c.is_active, c.category_id, cat.name
            ORDER BY c.is_active DESC, c.title", new { id = user.UserId });
        return Ok(rows);
    }

    /// <summary>GET /api/trainer/sessions?scope=upcoming|past&amp;classId=3</summary>
    [HttpGet("sessions")]
    public async Task<IActionResult> Sessions(string scope = "upcoming", int? classId = null)
    {
        var user = await auth.GetUserAsync(HttpContext);
        if (user == null || !user.IsTrainer) return Forbidden();

        var upcoming = scope != "past";
        var rows = await db.QueryAsync($@"
            SELECT v.session_id, v.class_id, c.title, c.class_type, v.start_time, v.end_time, v.status,
                   v.room_name, s.room_id, v.max_capacity, v.booked_count, v.seats_left
            FROM   v_session_availability v
            JOIN   class_session s ON s.session_id = v.session_id
            JOIN   fitness_class c ON c.class_id = v.class_id
            WHERE  c.trainer_id = @id
              AND  (@classId IS NULL OR v.class_id = @classId)
              AND  v.start_time {(upcoming ? ">" : "<=")} NOW()
            ORDER BY v.start_time {(upcoming ? "" : "DESC")}
            LIMIT 200", new { id = user.UserId, classId });
        return Ok(rows);
    }

    /// <summary>Roster: who is booked into one of my sessions.</summary>
    [HttpGet("sessions/{sessionId:int}/roster")]
    public async Task<IActionResult> Roster(int sessionId)
    {
        var user = await auth.GetUserAsync(HttpContext);
        if (user == null || !user.IsTrainer) return Forbidden();
        if (!await OwnsSessionAsync(sessionId, user.UserId)) return NotFound(new { message = "Session not found." });

        var rows = await db.QueryAsync(@"
            SELECT b.booking_id, b.confirmation_code, b.status, b.booked_at, b.cancelled_at, b.notes,
                   u.user_id AS customer_id, u.first_name, u.last_name, u.email, u.phone,
                   cu.fitness_goal
            FROM   booking b
            JOIN   app_user u  ON u.user_id = b.customer_id
            JOIN   customer cu ON cu.customer_id = b.customer_id
            WHERE  b.session_id = @sessionId
            ORDER BY b.status = 'cancelled', u.last_name, u.first_name", new { sessionId });
        return Ok(rows);
    }

    /// <summary>All bookings across my classes. GET /api/trainer/bookings?status=confirmed&amp;q=emily</summary>
    [HttpGet("bookings")]
    public async Task<IActionResult> Bookings(string? status, string? q, string scope = "upcoming")
    {
        var user = await auth.GetUserAsync(HttpContext);
        if (user == null || !user.IsTrainer) return Forbidden();

        var upcoming = scope != "past";
        var qTrim = string.IsNullOrWhiteSpace(q) ? null : q.Trim();
        var rows = await db.QueryAsync($@"
            SELECT b.booking_id, b.confirmation_code, b.status, b.booked_at,
                   s.session_id, s.start_time, c.class_id, c.title, c.class_type,
                   CONCAT(u.first_name, ' ', u.last_name) AS customer_name, u.email
            FROM   booking b
            JOIN   class_session s ON s.session_id = b.session_id
            JOIN   fitness_class c ON c.class_id = s.class_id
            JOIN   app_user u      ON u.user_id = b.customer_id
            WHERE  c.trainer_id = @id
              AND  s.start_time {(upcoming ? ">" : "<=")} NOW()
              AND  (@status IS NULL OR b.status = @status)
              AND  (@q IS NULL OR CONCAT(u.first_name, ' ', u.last_name) LIKE @like
                    OR u.email LIKE @like OR c.title LIKE @like OR b.confirmation_code = @q)
            ORDER BY s.start_time {(upcoming ? "" : "DESC")}, u.last_name
            LIMIT 300",
            new { id = user.UserId, status = string.IsNullOrWhiteSpace(status) ? null : status, q = qTrim, like = "%" + qTrim + "%" });
        return Ok(rows);
    }

    /// <summary>Mark attendance (attended / no_show) or cancel a customer's booking.</summary>
    [HttpPut("bookings/{bookingId:int}/status")]
    public async Task<IActionResult> SetBookingStatus(int bookingId, BookingStatusRequest req)
    {
        var user = await auth.GetUserAsync(HttpContext);
        if (user == null || !user.IsTrainer) return Forbidden();
        if (!BookingStatuses.Contains(req.Status)) return BadRequest(new { message = "Invalid status." });

        var booking = await db.QuerySingleAsync(@"
            SELECT b.booking_id, b.status, s.start_time, s.session_id
            FROM   booking b
            JOIN   class_session s ON s.session_id = b.session_id
            JOIN   fitness_class c ON c.class_id = s.class_id
            WHERE  b.booking_id = @bookingId AND c.trainer_id = @trainerId",
            new { bookingId, trainerId = user.UserId });
        if (booking == null) return NotFound(new { message = "Booking not found." });

        var started = (DateTime)booking["start_time"]! <= DateTime.Now;
        if ((req.Status == "attended" || req.Status == "no_show") && !started)
            return BadRequest(new { message = "Attendance can only be recorded once the session has started." });

        await db.ExecuteAsync(@"
            UPDATE booking
            SET    status = @Status,
                   cancelled_at = CASE WHEN @Status = 'cancelled' THEN NOW() ELSE NULL END
            WHERE  booking_id = @bookingId", new { req.Status, bookingId });
        return Ok(new { message = "Booking updated." });
    }

    /// <summary>Reschedule a session or change its room.</summary>
    [HttpPut("sessions/{sessionId:int}")]
    public async Task<IActionResult> UpdateSession(int sessionId, SessionRequest req)
    {
        var user = await auth.GetUserAsync(HttpContext);
        if (user == null || !user.IsTrainer) return Forbidden();
        if (!await OwnsSessionAsync(sessionId, user.UserId)) return NotFound(new { message = "Session not found." });
        if (req.StartTime <= DateTime.Now) return BadRequest(new { message = "Session must start in the future." });

        var duration = Convert.ToInt32(await db.ScalarAsync(@"
            SELECT c.duration_min FROM class_session s JOIN fitness_class c ON c.class_id = s.class_id
            WHERE s.session_id = @sessionId", new { sessionId }));
        var end = req.StartTime.AddMinutes(duration);

        await using var conn = await db.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();
        var conflict = await schedule.FindConflictAsync(user.UserId, req.RoomId, req.StartTime, end, sessionId, conn, tx);
        if (conflict != null) return Conflict(new { message = "Can't reschedule: " + conflict });

        await db.ExecuteAsync(@"
            UPDATE class_session SET start_time = @StartTime, end_time = @end, room_id = @RoomId, status = 'scheduled'
            WHERE  session_id = @sessionId", new { req.StartTime, end, req.RoomId, sessionId }, conn, tx);
        await tx.CommitAsync();
        return Ok(new { message = "Session updated." });
    }

    /// <summary>Cancel a session; all confirmed bookings on it are cancelled too.</summary>
    [HttpPost("sessions/{sessionId:int}/cancel")]
    public async Task<IActionResult> CancelSession(int sessionId)
    {
        var user = await auth.GetUserAsync(HttpContext);
        if (user == null || !user.IsTrainer) return Forbidden();
        if (!await OwnsSessionAsync(sessionId, user.UserId)) return NotFound(new { message = "Session not found." });

        await using var conn = await db.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();
        var affected = await db.ExecuteAsync(@"
            UPDATE booking SET status = 'cancelled', cancelled_at = NOW()
            WHERE  session_id = @sessionId AND status = 'confirmed'", new { sessionId }, conn, tx);
        await db.ExecuteAsync("UPDATE class_session SET status = 'cancelled' WHERE session_id = @sessionId",
            new { sessionId }, conn, tx);
        await tx.CommitAsync();
        return Ok(new { message = $"Session cancelled. {affected} booking(s) were cancelled." });
    }

    /// <summary>Delete a session outright (only allowed if nobody ever booked it).</summary>
    [HttpDelete("sessions/{sessionId:int}")]
    public async Task<IActionResult> DeleteSession(int sessionId)
    {
        var user = await auth.GetUserAsync(HttpContext);
        if (user == null || !user.IsTrainer) return Forbidden();
        if (!await OwnsSessionAsync(sessionId, user.UserId)) return NotFound(new { message = "Session not found." });

        var count = Convert.ToInt32(await db.ScalarAsync("SELECT COUNT(*) FROM booking WHERE session_id = @sessionId", new { sessionId }));
        if (count > 0) return BadRequest(new { message = "This session has bookings. Cancel it instead so customers keep their history." });

        await db.ExecuteAsync("DELETE FROM class_session WHERE session_id = @sessionId", new { sessionId });
        return Ok(new { message = "Session deleted." });
    }

    private async Task<bool> OwnsSessionAsync(int sessionId, int trainerId)
    {
        var count = await db.ScalarAsync(@"
            SELECT COUNT(*) FROM class_session s JOIN fitness_class c ON c.class_id = s.class_id
            WHERE  s.session_id = @sessionId AND c.trainer_id = @trainerId", new { sessionId, trainerId });
        return Convert.ToInt32(count) > 0;
    }

    private ObjectResult Forbidden() => StatusCode(403, new { message = "Trainer login required." });
}
