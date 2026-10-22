using FitZone.Api.Data;
using MySqlConnector;

namespace FitZone.Api.Services;

public class ScheduleService(Db db)
{
    /// <summary>Returns a message if the trainer or room is already busy in that time window, otherwise null.</summary>
    public async Task<string?> FindConflictAsync(int trainerId, int? roomId, DateTime start, DateTime end,
        int? ignoreSessionId, MySqlConnection conn, MySqlTransaction tx)
    {
        var clash = await db.QuerySingleAsync(@"
            SELECT c.title, c.trainer_id = @trainerId AS trainer_busy
            FROM   class_session s JOIN fitness_class c ON c.class_id = s.class_id
            WHERE  s.status <> 'cancelled'
              AND  (@ignore IS NULL OR s.session_id <> @ignore)
              AND  s.start_time < @end AND s.end_time > @start
              AND  (c.trainer_id = @trainerId OR (@roomId IS NOT NULL AND s.room_id = @roomId))
            LIMIT 1", new { trainerId, roomId, start, end, ignore = ignoreSessionId }, conn, tx);

        if (clash == null) return null;
        return Convert.ToInt32(clash["trainer_busy"]) == 1
            ? $"you are already teaching \"{clash["title"]}\" at that time."
            : $"the room is already booked for \"{clash["title"]}\".";
    }
}
