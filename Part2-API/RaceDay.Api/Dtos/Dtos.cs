using System.ComponentModel.DataAnnotations;
using RaceDay.Api.Models;

namespace RaceDay.Api.Dtos;

// ---------- Requests ----------

public class RegisterRequest
{
    [Required, StringLength(100, MinimumLength = 2)] public string FullName { get; set; } = string.Empty;
    [Required, EmailAddress, StringLength(150)] public string Email { get; set; } = string.Empty;
    [Required, StringLength(100, MinimumLength = 8)] public string Password { get; set; } = string.Empty;
    /// <summary>Either "Organiser" or "Participant".</summary>
    [Required] public UserRole? Role { get; set; }
    [StringLength(20)] public string? PhoneNumber { get; set; }
}

public class LoginRequest
{
    [Required, EmailAddress] public string Email { get; set; } = string.Empty;
    [Required] public string Password { get; set; } = string.Empty;
}

public class UpdateProfileRequest
{
    [Required, StringLength(100, MinimumLength = 2)] public string FullName { get; set; } = string.Empty;
    [StringLength(20)] public string? PhoneNumber { get; set; }
}

public class CreateEventRequest
{
    [Required, StringLength(150)] public string Name { get; set; } = string.Empty;
    [StringLength(1000)] public string? Description { get; set; }
    /// <summary>Format yyyy-MM-dd.</summary>
    [Required] public DateOnly? EventDate { get; set; }
    [Required, StringLength(200)] public string Location { get; set; } = string.Empty;
    [Range(0.1, 1000)] public decimal DistanceKm { get; set; }
    /// <summary>Run, Walk or Cycle.</summary>
    [Required] public EventType? EventType { get; set; }
}

public class UpdateEventRequest : CreateEventRequest
{
    /// <summary>Optional. Planned, Open, Closed, Completed or Cancelled.</summary>
    public string? Status { get; set; }
}

public class CategoryRequest
{
    [Required, StringLength(100)] public string Name { get; set; } = string.Empty;
    [Range(0.1, 1000)] public decimal DistanceKm { get; set; }
    [Range(1, 100000)] public int MaxParticipants { get; set; } = 100;
    [Range(0, 100000)] public decimal Fee { get; set; }
    /// <summary>Format HH:mm:ss (optional).</summary>
    public TimeOnly? StartTime { get; set; }
}

public class CaptureResultRequest
{
    /// <summary>Format HH:mm:ss. Required when Status is Finished.</summary>
    public TimeOnly? FinishTime { get; set; }
    [Range(1, 100000)] public int? Position { get; set; }
    /// <summary>Finished (default), DNF, DQ or Not Started.</summary>
    public string? Status { get; set; }
}

// ---------- Responses ----------

public class UserDto
{
    public int UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public string? PhoneNumber { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool HasProfilePicture { get; set; }
}

public class LoginResponse
{
    public int UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class CategoryDto
{
    public int CategoryId { get; set; }
    public int EventId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal DistanceKm { get; set; }
    public int MaxParticipants { get; set; }
    public decimal Fee { get; set; }
    public TimeOnly? StartTime { get; set; }
}

public class EventDto
{
    public int EventId { get; set; }
    public int OrganiserId { get; set; }
    public string OrganiserName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateOnly EventDate { get; set; }
    public string Location { get; set; } = string.Empty;
    public decimal DistanceKm { get; set; }
    public EventType EventType { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool HasBanner { get; set; }
    public List<CategoryDto>? Categories { get; set; }
}

public class EnrolmentDto
{
    public int EnrolmentId { get; set; }
    public int ParticipantId { get; set; }
    public string ParticipantName { get; set; } = string.Empty;
    public int EventId { get; set; }
    public string EventName { get; set; } = string.Empty;
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public DateTime EnrolmentDate { get; set; }
    public string Status { get; set; } = string.Empty;
    /// <summary>Result status (Finished, DNF, DQ...) once the Organiser has captured a result; otherwise null.</summary>
    public string? ResultStatus { get; set; }
    public int? Position { get; set; }
    public TimeOnly? FinishTime { get; set; }
}

public class ResultDto
{
    public int ResultId { get; set; }
    public int EnrolmentId { get; set; }
    public string ParticipantName { get; set; } = string.Empty;
    public string EventName { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public TimeOnly? FinishTime { get; set; }
    public int? Position { get; set; }
    public string Status { get; set; } = string.Empty;
}

// ---------- Part 3 additions ----------

public class UpdateEnrolmentStatusRequest
{
    /// <summary>Pending, Confirmed or Cancelled.</summary>
    [Required] public string Status { get; set; } = string.Empty;
}

/// <summary>One row of the Organiser dashboard: an event with its enrolment totals.</summary>
public class OrganiserEventDto
{
    public int EventId { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateOnly EventDate { get; set; }
    public string Location { get; set; } = string.Empty;
    public decimal DistanceKm { get; set; }
    public EventType EventType { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool HasBanner { get; set; }
    public int CategoryCount { get; set; }
    /// <summary>Enrolments that are not cancelled.</summary>
    public int EnrolmentCount { get; set; }
}

/// <summary>A Participant's personal race history entry, e.g. "47th out of 312 finishers".</summary>
public class MyResultDto
{
    public int ResultId { get; set; }
    public int EnrolmentId { get; set; }
    public string EventName { get; set; } = string.Empty;
    public DateOnly EventDate { get; set; }
    public string Location { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public TimeOnly? FinishTime { get; set; }
    public int? Position { get; set; }
    /// <summary>Number of participants who finished in the same category.</summary>
    public int TotalFinishers { get; set; }
    public string Status { get; set; } = string.Empty;
}
