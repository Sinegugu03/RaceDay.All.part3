namespace RaceDay.Api.Models;

public enum UserRole { Organiser, Participant, Admin }

public enum EventType { Run, Walk, Cycle }

/// <summary>A registered user. The Role column separates Organisers from Participants.</summary>
public class AppUser
{
    public int UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public UserRole Role { get; set; } = UserRole.Participant;
    public string? PhoneNumber { get; set; }
    /// <summary>Name of the profile picture blob in Azure Blob Storage (null = no picture).</summary>
    public string? ProfilePictureBlobName { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<RaceEvent> OrganisedEvents { get; set; } = new();
    public List<Enrolment> Enrolments { get; set; } = new();
}

/// <summary>A race event created by an Organiser (table: Events).</summary>
public class RaceEvent
{
    public int EventId { get; set; }
    public int OrganiserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    /// <summary>Name of the banner image blob in Azure Blob Storage (null = no banner).</summary>
    public string? BannerBlobName { get; set; }
    public DateOnly EventDate { get; set; }
    public string Location { get; set; } = string.Empty;
    public decimal DistanceKm { get; set; }
    public EventType EventType { get; set; }
    public string Status { get; set; } = "Planned";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public AppUser Organiser { get; set; } = null!;
    public List<Category> Categories { get; set; } = new();
}

/// <summary>An age/distance category inside an event (e.g. Under 20, Senior, 10km).</summary>
public class Category
{
    public int CategoryId { get; set; }
    public int EventId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal DistanceKm { get; set; }
    public int MaxParticipants { get; set; } = 100;
    public decimal Fee { get; set; }
    public TimeOnly? StartTime { get; set; }

    public RaceEvent Event { get; set; } = null!;
    public List<Enrolment> Enrolments { get; set; } = new();
}

/// <summary>Links a Participant to a Category (and therefore to an Event).</summary>
public class Enrolment
{
    public int EnrolmentId { get; set; }
    public int ParticipantId { get; set; }
    public int CategoryId { get; set; }
    public DateTime EnrolmentDate { get; set; } = DateTime.UtcNow;
    public string Status { get; set; } = "Pending";

    public AppUser Participant { get; set; } = null!;
    public Category Category { get; set; } = null!;
    public Result? Result { get; set; }
    public Payment? Payment { get; set; }
}

/// <summary>Finish time and position for one enrolment (1:1).</summary>
public class Result
{
    public int ResultId { get; set; }
    public int EnrolmentId { get; set; }
    public TimeOnly? FinishTime { get; set; }
    public int? Position { get; set; }
    public string Status { get; set; } = "Not Started";

    public Enrolment Enrolment { get; set; } = null!;
}

/// <summary>Entry-fee payment for one enrolment (1:1). Kept to match the Part 1 ERD; used in Part 3.</summary>
public class Payment
{
    public int PaymentId { get; set; }
    public int EnrolmentId { get; set; }
    public decimal Amount { get; set; }
    public DateTime? PaymentDate { get; set; }
    public string PaymentStatus { get; set; } = "Unpaid";

    public Enrolment Enrolment { get; set; } = null!;
}
