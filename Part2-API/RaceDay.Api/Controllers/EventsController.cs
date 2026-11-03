using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Filters;
using RaceDay.Api.Models;
using RaceDay.Api.Services;

namespace RaceDay.Api.Controllers;

[ApiController]
[Route("api/events")]
public class EventsController : ApiControllerBase
{
    private static readonly string[] AllowedStatuses = { "Planned", "Open", "Closed", "Completed", "Cancelled" };
    private readonly RaceDayDbContext _db;

    private readonly IBlobStorageService _blobs;

    public EventsController(RaceDayDbContext db, IBlobStorageService blobs)
    {
        _db = db;
        _blobs = blobs;
    }

    /// <summary>Lists all events. Public - no login needed. Supports optional filters.</summary>
    /// <remarks>Optional query parameters: location (contains), status, type (Run/Walk/Cycle), from and to (dates).</remarks>
    /// <response code="200">The matching events, soonest first.</response>
    [HttpGet]
    [ProducesResponseType(typeof(List<EventDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<EventDto>>> GetEvents(
        [FromQuery] string? location, [FromQuery] string? status, [FromQuery] EventType? type,
        [FromQuery(Name = "from")] DateTime? fromDate, [FromQuery(Name = "to")] DateTime? toDate)
    {
        IQueryable<RaceEvent> query = _db.Events.Include(e => e.Organiser);

        if (!string.IsNullOrWhiteSpace(location)) query = query.Where(e => e.Location.Contains(location));
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(e => e.Status == status);
        if (type.HasValue) query = query.Where(e => e.EventType == type.Value);
        if (fromDate.HasValue)
        {
            var fromDay = DateOnly.FromDateTime(fromDate.Value);
            query = query.Where(e => e.EventDate >= fromDay);
        }
        if (toDate.HasValue)
        {
            var toDay = DateOnly.FromDateTime(toDate.Value);
            query = query.Where(e => e.EventDate <= toDay);
        }

        var events = await query.OrderBy(e => e.EventDate).ToListAsync();
        return events.Select(e => e.ToDto()).ToList();
    }

    /// <summary>Lists the logged-in Organiser's own events with category and enrolment totals (Organiser dashboard).</summary>
    /// <response code="200">The Organiser's events, soonest first.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Logged in as a Participant.</response>
    [HttpGet("mine")]
    [SessionAuthorize(UserRole.Organiser)]
    [ProducesResponseType(typeof(List<OrganiserEventDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<List<OrganiserEventDto>>> GetMyEvents()
    {
        var userId = CurrentUserId;
        var events = await _db.Events
            .Where(e => e.OrganiserId == userId)
            .OrderBy(e => e.EventDate)
            .Select(e => new OrganiserEventDto
            {
                EventId = e.EventId,
                Name = e.Name,
                EventDate = e.EventDate,
                Location = e.Location,
                DistanceKm = e.DistanceKm,
                EventType = e.EventType,
                Status = e.Status,
                HasBanner = e.BannerBlobName != null,
                CategoryCount = e.Categories.Count,
                EnrolmentCount = e.Categories.SelectMany(c => c.Enrolments).Count(en => en.Status != "Cancelled")
            })
            .ToListAsync();
        return events;
    }

    /// <summary>Gets one event, including its categories. Public - no login needed.</summary>
    /// <response code="200">The event with its categories.</response>
    /// <response code="404">No event with that id.</response>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(EventDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EventDto>> GetEvent(int id)
    {
        var ev = await _db.Events.Include(e => e.Organiser).Include(e => e.Categories)
            .FirstOrDefaultAsync(e => e.EventId == id);
        if (ev is null) return NotFoundProblem($"No event with id {id}.");
        return ev.ToDto(includeCategories: true);
    }

    /// <summary>Creates a new event owned by the logged-in Organiser.</summary>
    /// <remarks>Expects: name, description, eventDate (yyyy-MM-dd, not in the past), location, distanceKm and eventType (Run, Walk or Cycle).</remarks>
    /// <response code="201">The created event.</response>
    /// <response code="400">Validation failed or the date is in the past.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Logged in as a Participant.</response>
    [HttpPost]
    [SessionAuthorize(UserRole.Organiser)]
    [ProducesResponseType(typeof(EventDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<EventDto>> CreateEvent(CreateEventRequest request)
    {
        if (request.EventDate!.Value < DateOnly.FromDateTime(DateTime.UtcNow.Date))
            return BadRequestProblem("The event date cannot be in the past.");

        var ev = new RaceEvent
        {
            OrganiserId = CurrentUserId,
            Name = request.Name.Trim(),
            Description = request.Description,
            EventDate = request.EventDate.Value,
            Location = request.Location.Trim(),
            DistanceKm = request.DistanceKm,
            EventType = request.EventType!.Value
        };
        _db.Events.Add(ev);
        await _db.SaveChangesAsync();
        await _db.Entry(ev).Reference(e => e.Organiser).LoadAsync();

        return Created($"/api/events/{ev.EventId}", ev.ToDto());
    }

    /// <summary>Updates an event. Only the Organiser who owns the event may do this.</summary>
    /// <remarks>Expects the same fields as creating an event, plus an optional status (Planned, Open, Closed, Completed, Cancelled).</remarks>
    /// <response code="200">The updated event.</response>
    /// <response code="400">Validation failed or invalid status.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Not an Organiser, or the event belongs to another Organiser.</response>
    /// <response code="404">No event with that id.</response>
    [HttpPut("{id:int}")]
    [SessionAuthorize(UserRole.Organiser)]
    [ProducesResponseType(typeof(EventDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EventDto>> UpdateEvent(int id, UpdateEventRequest request)
    {
        var ev = await _db.Events.Include(e => e.Organiser).FirstOrDefaultAsync(e => e.EventId == id);
        if (ev is null) return NotFoundProblem($"No event with id {id}.");
        if (ev.OrganiserId != CurrentUserId) return ForbiddenProblem("You can only edit your own events.");
        if (request.Status is not null && !AllowedStatuses.Contains(request.Status))
            return BadRequestProblem($"Status must be one of: {string.Join(", ", AllowedStatuses)}.");

        ev.Name = request.Name.Trim();
        ev.Description = request.Description;
        ev.EventDate = request.EventDate!.Value;
        ev.Location = request.Location.Trim();
        ev.DistanceKm = request.DistanceKm;
        ev.EventType = request.EventType!.Value;
        if (request.Status is not null) ev.Status = request.Status;

        await _db.SaveChangesAsync();
        return ev.ToDto();
    }

    /// <summary>Deletes an event and its categories. Only the owning Organiser may do this.</summary>
    /// <response code="204">Deleted.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Not an Organiser, or the event belongs to another Organiser.</response>
    /// <response code="404">No event with that id.</response>
    /// <response code="409">The event already has enrolments and cannot be deleted.</response>
    [HttpDelete("{id:int}")]
    [SessionAuthorize(UserRole.Organiser)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteEvent(int id)
    {
        var ev = await _db.Events.FirstOrDefaultAsync(e => e.EventId == id);
        if (ev is null) return NotFoundProblem($"No event with id {id}.");
        if (ev.OrganiserId != CurrentUserId) return ForbiddenProblem("You can only delete your own events.");

        if (await _db.Enrolments.AnyAsync(en => en.Category.EventId == id))
            return ConflictProblem("This event already has enrolments. Set its status to Cancelled instead of deleting it.");

        var bannerBlob = ev.BannerBlobName;
        _db.Events.Remove(ev);
        await _db.SaveChangesAsync();

        try { await _blobs.DeleteAsync(bannerBlob); }
        catch (Exception) { /* the event is already deleted; a leftover image is harmless */ }

        return NoContent();
    }
}
