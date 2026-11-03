using RaceDay.Api.Models;

namespace RaceDay.Api.Dtos;

/// <summary>Maps entities to response DTOs so password hashes and EF navigation loops never reach the client.</summary>
public static class Mapper
{
    public static UserDto ToDto(this AppUser u) => new()
    {
        UserId = u.UserId,
        FullName = u.FullName,
        Email = u.Email,
        Role = u.Role,
        PhoneNumber = u.PhoneNumber,
        CreatedAt = u.CreatedAt,
        HasProfilePicture = !string.IsNullOrEmpty(u.ProfilePictureBlobName)
    };

    public static CategoryDto ToDto(this Category c) => new()
    {
        CategoryId = c.CategoryId,
        EventId = c.EventId,
        Name = c.Name,
        DistanceKm = c.DistanceKm,
        MaxParticipants = c.MaxParticipants,
        Fee = c.Fee,
        StartTime = c.StartTime
    };

    public static EventDto ToDto(this RaceEvent e, bool includeCategories = false) => new()
    {
        EventId = e.EventId,
        OrganiserId = e.OrganiserId,
        OrganiserName = e.Organiser?.FullName ?? string.Empty,
        Name = e.Name,
        Description = e.Description,
        EventDate = e.EventDate,
        Location = e.Location,
        DistanceKm = e.DistanceKm,
        EventType = e.EventType,
        Status = e.Status,
        HasBanner = !string.IsNullOrEmpty(e.BannerBlobName),
        Categories = includeCategories ? e.Categories.Select(c => c.ToDto()).ToList() : null
    };

    /// <summary>Requires Participant and Category.Event to be loaded.</summary>
    public static EnrolmentDto ToDto(this Enrolment e) => new()
    {
        EnrolmentId = e.EnrolmentId,
        ParticipantId = e.ParticipantId,
        ParticipantName = e.Participant.FullName,
        EventId = e.Category.EventId,
        EventName = e.Category.Event.Name,
        CategoryId = e.CategoryId,
        CategoryName = e.Category.Name,
        EnrolmentDate = e.EnrolmentDate,
        Status = e.Status,
        ResultStatus = e.Result?.Status,
        Position = e.Result?.Position,
        FinishTime = e.Result?.FinishTime
    };

    /// <summary>Requires Enrolment.Participant and Enrolment.Category.Event to be loaded.</summary>
    public static ResultDto ToDto(this Result r) => new()
    {
        ResultId = r.ResultId,
        EnrolmentId = r.EnrolmentId,
        ParticipantName = r.Enrolment.Participant.FullName,
        EventName = r.Enrolment.Category.Event.Name,
        CategoryName = r.Enrolment.Category.Name,
        FinishTime = r.FinishTime,
        Position = r.Position,
        Status = r.Status
    };
}
