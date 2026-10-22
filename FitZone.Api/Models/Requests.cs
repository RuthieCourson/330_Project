namespace FitZone.Api.Models;

// Request bodies sent from the front end (JSON -> C#)

public class RegisterRequest
{
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string Email { get; set; } = "";
    public string? Phone { get; set; }
    public string Password { get; set; } = "";
    public string Role { get; set; } = "customer";   // "customer" or "trainer"
}

public class LoginRequest
{
    public string Email { get; set; } = "";
    public string Password { get; set; } = "";
}

public class ProfileUpdateRequest
{
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string? Phone { get; set; }
    // customer fields
    public DateTime? DateOfBirth { get; set; }
    public string? FitnessGoal { get; set; }
    public string? EmergencyContact { get; set; }
    // trainer fields
    public string? Bio { get; set; }
    public string? Specialty { get; set; }
    public int? YearsExperience { get; set; }
    public string? Certifications { get; set; }
}

public class ChangePasswordRequest
{
    public string CurrentPassword { get; set; } = "";
    public string NewPassword { get; set; } = "";
}

public class ClassRequest
{
    public int CategoryId { get; set; }
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public string ClassType { get; set; } = "group";        // group | private
    public string Difficulty { get; set; } = "Beginner";    // Beginner | Intermediate | Advanced
    public int DurationMin { get; set; }
    public decimal Price { get; set; }
    public int MaxCapacity { get; set; }
    public bool IsActive { get; set; } = true;
}

public class SessionRequest
{
    public int? RoomId { get; set; }
    public DateTime StartTime { get; set; }
    // Optional: repeat the same time every week for N weeks (1 = just this one)
    public int RepeatWeeks { get; set; } = 1;
}

public class BookingRequest
{
    public int SessionId { get; set; }
    public string? Notes { get; set; }
}

public class BookingStatusRequest
{
    public string Status { get; set; } = "";   // attended | no_show | confirmed | cancelled
}
