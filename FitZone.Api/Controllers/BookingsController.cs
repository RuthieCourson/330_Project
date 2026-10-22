using System.Security.Cryptography;
using FitZone.Api.Data;
using FitZone.Api.Models;
using FitZone.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace FitZone.Api.Controllers;

/// <summary>Customer booking management: book, view history, cancel.</summary>
[ApiController]
[Route("api/bookings")]
public class BookingsController(Db db, AuthService auth) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Book(BookingRequest req)
    {
        var user = await auth.GetUserAsync(HttpContext);
        if (user == null || !user.IsCustomer) return StatusCode(403, new { message = "Please log in as a customer to book." });

        await using var conn = await db.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();

        // Lock the session row so two people can't grab the last seat at the same time
        var session = await db.QuerySingleAsync(@"
            SELECT s.session_id, s.start_time, s.end_time, s.status, c.max_capacity, c.is_active, c.title
            FROM   class_session s JOIN fitness_class c ON c.class_id = s.class_id
            WHERE  s.session_id = @SessionId
            FOR UPDATE", new { req.SessionId }, conn, tx);

        if (session == null) return NotFound(new { message = "Session not found." });
        if ((string)session["status"]! != "scheduled" || !Convert.ToBoolean(session["is_active"]))
            return BadRequest(new { message = "This session is not open for booking." });
        if ((DateTime)session["start_time"]! <= DateTime.Now)
            return BadRequest(new { message = "This session has already started." });

        var booked = Convert.ToInt32(await db.ScalarAsync(@"
            SELECT COUNT(*) FROM booking WHERE session_id = @SessionId AND status IN ('confirmed','attended')",
            new { req.SessionId }, conn, tx));
        if (booked >= Convert.ToInt32(session["max_capacity"]))
            return Conflict(new { message = "Sorry, this session is full." });

        // Don't let a customer be in two places at once
        var overlap = await db.ScalarAsync(@"
            SELECT c.title FROM booking b
            JOIN   class_session s ON s.session_id = b.session_id
            JOIN   fitness_class c ON c.class_id = s.class_id
            WHERE  b.customer_id = @customerId AND b.status = 'confirmed' AND b.session_id <> @SessionId
              AND  s.start_time < @end AND s.end_time > @start
            LIMIT 1", new { customerId = user.UserId, req.SessionId, start = session["start_time"], end = session["end_time"] }, conn, tx);
        if (overlap != null)
            return Conflict(new { message = $"You already have \"{overlap}\" booked at that time." });

        var existing = await db.QuerySingleAsync(
            "SELECT booking_id, status FROM booking WHERE customer_id = @customerId AND session_id = @SessionId",
            new { customerId = user.UserId, req.SessionId }, conn, tx);

        var code = NewConfirmationCode();
        long bookingId;
        if (existing == null)
        {
            bookingId = await db.InsertAsync(@"
                INSERT INTO booking (customer_id, session_id, confirmation_code, notes)
                VALUES (@customerId, @SessionId, @code, @Notes)",
                new { customerId = user.UserId, req.SessionId, code, req.Notes }, conn, tx);
        }
        else if ((string)existing["status"]! == "cancelled")
        {
            // Re-booking a session they previously cancelled
            bookingId = Convert.ToInt64(existing["booking_id"]);
            await db.ExecuteAsync(@"
                UPDATE booking SET status = 'confirmed', confirmation_code = @code, booked_at = NOW(),
                       cancelled_at = NULL, notes = @Notes
                WHERE  booking_id = @bookingId", new { bookingId, code, req.Notes }, conn, tx);
        }
        else
        {
            return Conflict(new { message = "You are already booked for this session." });
        }

        await tx.CommitAsync();
        return Ok(await GetBookingAsync(bookingId, user.UserId));
    }

    /// <summary>GET /api/bookings/mine?scope=upcoming|past|all</summary>
    [HttpGet("mine")]
    public async Task<IActionResult> Mine(string scope = "all")
    {
        var user = await auth.GetUserAsync(HttpContext);
        if (user == null || !user.IsCustomer) return StatusCode(403, new { message = "Please log in as a customer." });

        var where = scope switch
        {
            "upcoming" => "AND s.start_time > NOW() AND b.status = 'confirmed'",
            "past"     => "AND (s.start_time <= NOW() OR b.status <> 'confirmed')",
            _          => ""
        };

        var rows = await db.QueryAsync($@"
            {BookingSelect}
            WHERE b.customer_id = @customerId {where}
            ORDER BY s.start_time {(scope == "upcoming" ? "" : "DESC")}", new { customerId = user.UserId });
        return Ok(rows);
    }

    [HttpPost("{id:int}/cancel")]
    public async Task<IActionResult> Cancel(int id)
    {
        var user = await auth.GetUserAsync(HttpContext);
        if (user == null || !user.IsCustomer) return StatusCode(403, new { message = "Please log in as a customer." });

        var booking = await GetBookingAsync(id, user.UserId);
        if (booking == null) return NotFound(new { message = "Booking not found." });
        if ((string)booking["status"]! != "confirmed")
            return BadRequest(new { message = "Only confirmed bookings can be cancelled." });
        if ((DateTime)booking["start_time"]! <= DateTime.Now)
            return BadRequest(new { message = "This session has already started and can't be cancelled." });

        await db.ExecuteAsync(
            "UPDATE booking SET status = 'cancelled', cancelled_at = NOW() WHERE booking_id = @id", new { id });
        return Ok(await GetBookingAsync(id, user.UserId));
    }

    private const string BookingSelect = @"
        SELECT b.booking_id, b.confirmation_code, b.status, b.booked_at, b.cancelled_at, b.notes,
               s.session_id, s.start_time, s.end_time, s.status AS session_status,
               r.name AS room_name,
               c.class_id, c.title, c.class_type, c.price, c.duration_min,
               cat.name AS category,
               CONCAT(u.first_name, ' ', u.last_name) AS trainer_name
        FROM   booking b
        JOIN   class_session s ON s.session_id = b.session_id
        JOIN   fitness_class c ON c.class_id = s.class_id
        JOIN   category cat    ON cat.category_id = c.category_id
        JOIN   app_user u      ON u.user_id = c.trainer_id
        LEFT JOIN room r       ON r.room_id = s.room_id";

    private Task<Dictionary<string, object?>?> GetBookingAsync(long bookingId, int customerId)
    {
        return db.QuerySingleAsync($"{BookingSelect} WHERE b.booking_id = @bookingId AND b.customer_id = @customerId",
            new { bookingId, customerId });
    }

    private static string NewConfirmationCode()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        return new string(Enumerable.Range(0, 8).Select(_ => chars[RandomNumberGenerator.GetInt32(chars.Length)]).ToArray());
    }
}
