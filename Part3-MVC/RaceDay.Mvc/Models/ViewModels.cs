using System.ComponentModel.DataAnnotations;

namespace RaceDay.Mvc.Models;

// Optional properties are nullable on purpose: with <Nullable> enabled, MVC treats non-nullable
// reference types as [Required].

public class LoginViewModel
{
    [Required(ErrorMessage = "Please enter your email address."), EmailAddress(ErrorMessage = "Please enter a valid email address.")]
    [Display(Name = "Email address")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please enter your password."), DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    public string? ReturnUrl { get; set; }
}

public class RegisterViewModel
{
    [Required(ErrorMessage = "Please enter your full name."), StringLength(100, MinimumLength = 2, ErrorMessage = "Your name must be between 2 and 100 characters.")]
    [Display(Name = "Full name")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please enter your email address."), EmailAddress(ErrorMessage = "Please enter a valid email address."), StringLength(150)]
    [Display(Name = "Email address")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please choose a password."), DataType(DataType.Password)]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "Your password must be at least 8 characters.")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please confirm your password."), DataType(DataType.Password)]
    [Compare(nameof(Password), ErrorMessage = "The two passwords do not match.")]
    [Display(Name = "Confirm password")]
    public string ConfirmPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please choose how you will use RaceDay.")]
    [RegularExpression("^(Organiser|Participant)$", ErrorMessage = "Please choose Organiser or Participant.")]
    public string Role { get; set; } = "Participant";

    [StringLength(20, ErrorMessage = "The phone number is too long.")]
    [Display(Name = "Phone number")]
    public string? PhoneNumber { get; set; }
}

public class ErrorViewModel
{
    public string Title { get; set; } = "Something went wrong";
    public string Message { get; set; } = "Please try again in a moment.";
}

public class EventFilterViewModel
{
    public string? Location { get; set; }
    public string? Type { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
}

public class HomeViewModel
{
    public EventFilterViewModel Filter { get; set; } = new();
    public List<EventDto> Events { get; set; } = new();
    public string? Error { get; set; }
}

public class EventDetailsViewModel
{
    public EventDto Event { get; set; } = new();
    public string? Role { get; set; }
    public HashSet<int> EnrolledCategoryIds { get; set; } = new();
}

public class ProfileViewModel
{
    public int UserId { get; set; }

    [Required(ErrorMessage = "Please enter your full name."), StringLength(100, MinimumLength = 2, ErrorMessage = "Your name must be between 2 and 100 characters.")]
    [Display(Name = "Full name")]
    public string FullName { get; set; } = string.Empty;

    [StringLength(20, ErrorMessage = "The phone number is too long.")]
    [Display(Name = "Phone number")]
    public string? PhoneNumber { get; set; }

    public string? Email { get; set; }
    public string? Role { get; set; }
    public bool HasProfilePicture { get; set; }

    [Display(Name = "Profile picture")]
    public IFormFile? Picture { get; set; }
}

public class EventFormViewModel
{
    public int EventId { get; set; }

    [Required(ErrorMessage = "Please enter the event name."), StringLength(150)]
    [Display(Name = "Event name")]
    public string Name { get; set; } = string.Empty;

    [StringLength(1000, ErrorMessage = "The description is too long (maximum 1000 characters).")]
    public string? Description { get; set; }

    [Required(ErrorMessage = "Please choose the event date."), DataType(DataType.Date)]
    [Display(Name = "Date")]
    public DateTime? EventDate { get; set; }

    [Required(ErrorMessage = "Please enter the location."), StringLength(200)]
    public string Location { get; set; } = string.Empty;

    [Range(0.1, 1000, ErrorMessage = "Distance must be between 0.1 and 1000 km.")]
    [Display(Name = "Distance (km)")]
    public decimal DistanceKm { get; set; }

    [Required(ErrorMessage = "Please choose the event type.")]
    [RegularExpression("^(Run|Walk|Cycle)$", ErrorMessage = "Please choose Run, Walk or Cycle.")]
    [Display(Name = "Event type")]
    public string EventType { get; set; } = "Run";

    public string? Status { get; set; }

    [Display(Name = "Banner image")]
    public IFormFile? Banner { get; set; }

    public bool HasBanner { get; set; }
}

public class CategoryFormViewModel
{
    public int EventId { get; set; }

    [Required(ErrorMessage = "Please enter a category name, e.g. Under 20, Senior or 10km."), StringLength(100)]
    [Display(Name = "Category name")]
    public string Name { get; set; } = string.Empty;

    [Range(0.1, 1000, ErrorMessage = "Distance must be between 0.1 and 1000 km.")]
    [Display(Name = "Distance (km)")]
    public decimal DistanceKm { get; set; }

    [Range(1, 100000, ErrorMessage = "Maximum participants must be at least 1.")]
    [Display(Name = "Maximum participants")]
    public int MaxParticipants { get; set; } = 100;

    [Range(0, 100000, ErrorMessage = "The fee cannot be negative.")]
    [Display(Name = "Entry fee (R)")]
    public decimal Fee { get; set; }

    [RegularExpression(@"^([01]\d|2[0-3]):[0-5]\d(:[0-5]\d)?$", ErrorMessage = "Enter the start time as hh:mm.")]
    [Display(Name = "Start time")]
    public string? StartTime { get; set; }
}

public class CategoriesViewModel
{
    public EventDto Event { get; set; } = new();
    public CategoryFormViewModel NewCategory { get; set; } = new();
}

public class EventEnrolmentsViewModel
{
    public EventDto Event { get; set; } = new();
    public List<EnrolmentDto> Enrolments { get; set; } = new();
}

public class ResultFormViewModel
{
    public int EventId { get; set; }
    public int EnrolmentId { get; set; }
    public bool HasResult { get; set; }

    [RegularExpression(@"^([01]\d|2[0-3]):[0-5]\d(:[0-5]\d)?$", ErrorMessage = "Enter the finish time as hh:mm:ss.")]
    public string? FinishTime { get; set; }

    [Range(1, 100000, ErrorMessage = "Position must be 1 or higher.")]
    public int? Position { get; set; }

    [Required]
    [RegularExpression("^(Finished|DNF|DQ|Not Started)$", ErrorMessage = "Please choose a valid result status.")]
    public string Status { get; set; } = "Finished";
}

public class OrganiserDashboardViewModel
{
    public List<OrganiserEventDto> Events { get; set; } = new();
    public List<OrganiserEventDto> NextEvents { get; set; } = new();
    public int TotalEvents { get; set; }
    public int UpcomingEvents { get; set; }
    public int TotalEnrolments { get; set; }
    public string? Error { get; set; }
}
