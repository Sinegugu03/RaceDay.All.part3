using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Filters;
using RaceDay.Api.Models;

namespace RaceDay.Api.Controllers;

[ApiController]
public class EnrolmentsController : ApiControllerBase
{
    private static readonly string[] ClosedStatuses = { "Closed", "Completed", "Cancelled" };
    private static readonly string[] AllowedStatuses = { "Pending", "Confirmed", "Cancelled" };
    private readonly RaceDayDbContext _db;

    public EnrolmentsController(RaceDayDbContext db) => _db = db;

    private IQueryable<Enrolment> EnrolmentQuery => _db.Enrolments
        .Include(e => e.Participant)
        .Include(e => e.Category).ThenInclude(c => c.Event)
        .Include(e => e.Result);

    /// <summary>Enrols the logged-in Participant into a category of an event.</summary>
    /// <remarks>
    /// No body needed - the category id is in the URL and the participant comes from the session.
    /// The enrolment records the link between the Participant, the event and the chosen category.
    /// </remarks>
    /// <response code="201">The enrolment record.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Logged in as an Organiser.</response>
    /// <response code="404">No category with that id.</response>
    /// <response code="409">Already enrolled, the category is full, or the event is closed.</response>
    [HttpPost("api/categories/{categoryId:int}/enrolments")]
    [SessionAuthorize(UserRole.Participant)]
    [ProducesResponseType(typeof(EnrolmentDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<EnrolmentDto>> Enrol(int categoryId)
    {
        var userId = CurrentUserId;
        var category = await _db.Categories.Include(c => c.Event).FirstOrDefaultAsync(c => c.CategoryId == categoryId);
        if (category is null) return NotFoundProblem($"No category with id {categoryId}.");

        if (ClosedStatuses.Contains(category.Event.Status))
            return ConflictProblem($"This event is {category.Event.Status} and no longer accepts enrolments.");

        if (await _db.Enrolments.AnyAsync(e => e.ParticipantId == userId && e.CategoryId == categoryId))
            return ConflictProblem("You are already enrolled in this category.");

        var taken = await _db.Enrolments.CountAsync(e => e.CategoryId == categoryId && e.Status != "Cancelled");
        if (taken >= category.MaxParticipants)
            return ConflictProblem("This category is full.");

        var enrolment = new Enrolment { ParticipantId = userId, CategoryId = categoryId, Status = "Pending" };
        _db.Enrolments.Add(enrolment);
        await _db.SaveChangesAsync();

        var saved = await EnrolmentQuery.FirstAsync(e => e.EnrolmentId == enrolment.EnrolmentId);
        return Created($"/api/enrolments/{saved.EnrolmentId}", saved.ToDto());
    }

    /// <summary>Lists all enrolments of the logged-in Participant.</summary>
    /// <response code="200">The participant's enrolments, newest first.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Logged in as an Organiser.</response>
    [HttpGet("api/users/me/enrolments")]
    [SessionAuthorize(UserRole.Participant)]
    [ProducesResponseType(typeof(List<EnrolmentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<List<EnrolmentDto>>> GetMyEnrolments()
    {
        var userId = CurrentUserId;
        var list = await EnrolmentQuery.Where(e => e.ParticipantId == userId)
            .OrderByDescending(e => e.EnrolmentDate).ToListAsync();
        return list.Select(e => e.ToDto()).ToList();
    }

    /// <summary>Lists everyone enrolled in a category. Only the Organiser who owns the event may do this.</summary>
    /// <response code="200">The enrolments for the category.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Not an Organiser, or the event belongs to another Organiser.</response>
    /// <response code="404">No category with that id.</response>
    [HttpGet("api/categories/{categoryId:int}/enrolments")]
    [SessionAuthorize(UserRole.Organiser)]
    [ProducesResponseType(typeof(List<EnrolmentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<EnrolmentDto>>> GetCategoryEnrolments(int categoryId)
    {
        var category = await _db.Categories.Include(c => c.Event).FirstOrDefaultAsync(c => c.CategoryId == categoryId);
        if (category is null) return NotFoundProblem($"No category with id {categoryId}.");
        if (category.Event.OrganiserId != CurrentUserId)
            return ForbiddenProblem("You can only view enrolments for your own events.");

        var list = await EnrolmentQuery.Where(e => e.CategoryId == categoryId)
            .OrderBy(e => e.EnrolmentDate).ToListAsync();
        return list.Select(e => e.ToDto()).ToList();
    }

    /// <summary>Cancels (removes) an enrolment. Allowed for the Participant who owns it, or the Organiser of the event.</summary>
    /// <response code="204">Enrolment removed.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">The enrolment belongs to someone else's event or account.</response>
    /// <response code="404">No enrolment with that id.</response>
    /// <response code="409">A result has already been captured for this enrolment.</response>
    [HttpDelete("api/enrolments/{id:int}")]
    [SessionAuthorize(UserRole.Participant, UserRole.Organiser)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CancelEnrolment(int id)
    {
        var userId = CurrentUserId;
        var enrolment = await EnrolmentQuery.FirstOrDefaultAsync(e => e.EnrolmentId == id);
        if (enrolment is null) return NotFoundProblem($"No enrolment with id {id}.");

        var allowed = CurrentRole == UserRole.Participant
            ? enrolment.ParticipantId == userId
            : enrolment.Category.Event.OrganiserId == userId;
        if (!allowed) return ForbiddenProblem("You are not allowed to cancel this enrolment.");

        if (await _db.Results.AnyAsync(r => r.EnrolmentId == id))
            return ConflictProblem("A result has already been captured, so this enrolment cannot be cancelled.");

        _db.Enrolments.Remove(enrolment);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>Lists every enrolment for an event, with each participant's category, status and result. Only the Organiser who owns the event may do this.</summary>
    /// <response code="200">The event's enrolments grouped by category name.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Not an Organiser, or the event belongs to another Organiser.</response>
    /// <response code="404">No event with that id.</response>
    [HttpGet("api/events/{eventId:int}/enrolments")]
    [SessionAuthorize(UserRole.Organiser)]
    [ProducesResponseType(typeof(List<EnrolmentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<EnrolmentDto>>> GetEventEnrolments(int eventId)
    {
        var ev = await _db.Events.FirstOrDefaultAsync(e => e.EventId == eventId);
        if (ev is null) return NotFoundProblem($"No event with id {eventId}.");
        if (ev.OrganiserId != CurrentUserId) return ForbiddenProblem("You can only view enrolments for your own events.");

        var list = await EnrolmentQuery.Where(e => e.Category.EventId == eventId)
            .OrderBy(e => e.Category.Name).ThenBy(e => e.EnrolmentDate).ToListAsync();
        return list.Select(e => e.ToDto()).ToList();
    }

    /// <summary>Changes an enrolment's status to Pending, Confirmed or Cancelled. Only the Organiser who owns the event may do this.</summary>
    /// <remarks>Expects: status ("Pending", "Confirmed" or "Cancelled"). New enrolments start as Pending.</remarks>
    /// <response code="200">The updated enrolment.</response>
    /// <response code="400">Invalid status.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Not an Organiser, or the event belongs to another Organiser.</response>
    /// <response code="404">No enrolment with that id.</response>
    [HttpPut("api/enrolments/{id:int}/status")]
    [SessionAuthorize(UserRole.Organiser)]
    [ProducesResponseType(typeof(EnrolmentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EnrolmentDto>> UpdateEnrolmentStatus(int id, UpdateEnrolmentStatusRequest request)
    {
        var enrolment = await EnrolmentQuery.FirstOrDefaultAsync(e => e.EnrolmentId == id);
        if (enrolment is null) return NotFoundProblem($"No enrolment with id {id}.");
        if (enrolment.Category.Event.OrganiserId != CurrentUserId)
            return ForbiddenProblem("You can only change enrolments for your own events.");
        if (!AllowedStatuses.Contains(request.Status))
            return BadRequestProblem($"Status must be one of: {string.Join(", ", AllowedStatuses)}.");

        enrolment.Status = request.Status;
        await _db.SaveChangesAsync();
        return enrolment.ToDto();
    }
}
