using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Filters;
using RaceDay.Api.Models;

namespace RaceDay.Api.Controllers;

[ApiController]
public class ResultsController : ApiControllerBase
{
    private static readonly string[] AllowedStatuses = { "Not Started", "Finished", "DNF", "DQ" };
    private readonly RaceDayDbContext _db;

    public ResultsController(RaceDayDbContext db) => _db = db;

    private IQueryable<Enrolment> EnrolmentQuery => _db.Enrolments
        .Include(e => e.Participant)
        .Include(e => e.Category).ThenInclude(c => c.Event);

    private IQueryable<Result> ResultQuery => _db.Results
        .Include(r => r.Enrolment).ThenInclude(e => e.Participant)
        .Include(r => r.Enrolment).ThenInclude(e => e.Category).ThenInclude(c => c.Event);

    /// <summary>Captures the finish time and position for a participant's enrolment. Organiser who owns the event only.</summary>
    /// <remarks>Expects: finishTime (HH:mm:ss, required when status is Finished), position and status (Finished, DNF, DQ, Not Started).</remarks>
    /// <response code="201">The captured result.</response>
    /// <response code="400">Validation failed.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Not an Organiser, or the event belongs to another Organiser.</response>
    /// <response code="404">No enrolment with that id.</response>
    /// <response code="409">A result was already captured for this enrolment.</response>
    [HttpPost("api/enrolments/{enrolmentId:int}/result")]
    [SessionAuthorize(UserRole.Organiser)]
    [ProducesResponseType(typeof(ResultDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ResultDto>> CaptureResult(int enrolmentId, CaptureResultRequest request)
    {
        var enrolment = await EnrolmentQuery.FirstOrDefaultAsync(e => e.EnrolmentId == enrolmentId);
        if (enrolment is null) return NotFoundProblem($"No enrolment with id {enrolmentId}.");
        if (enrolment.Category.Event.OrganiserId != CurrentUserId)
            return ForbiddenProblem("You can only capture results for your own events.");

        if (await _db.Results.AnyAsync(r => r.EnrolmentId == enrolmentId))
            return ConflictProblem("A result has already been captured for this enrolment.");

        var status = request.Status ?? "Finished";
        if (!AllowedStatuses.Contains(status))
            return BadRequestProblem($"Status must be one of: {string.Join(", ", AllowedStatuses)}.");
        if (status == "Finished" && request.FinishTime is null)
            return BadRequestProblem("finishTime is required when the status is Finished.");

        var result = new Result
        {
            EnrolmentId = enrolmentId,
            FinishTime = request.FinishTime,
            Position = request.Position,
            Status = status
        };
        _db.Results.Add(result);
        await _db.SaveChangesAsync();

        result.Enrolment = enrolment;
        return Created($"/api/enrolments/{enrolmentId}/result", result.ToDto());
    }

    /// <summary>Gets the result for one enrolment. Allowed for the Participant who owns it, or the Organiser of the event.</summary>
    /// <response code="200">The result.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">The enrolment belongs to someone else.</response>
    /// <response code="404">No enrolment, or no result captured yet.</response>
    [HttpGet("api/enrolments/{enrolmentId:int}/result")]
    [SessionAuthorize(UserRole.Participant, UserRole.Organiser)]
    [ProducesResponseType(typeof(ResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ResultDto>> GetResult(int enrolmentId)
    {
        var userId = CurrentUserId;
        var enrolment = await EnrolmentQuery.FirstOrDefaultAsync(e => e.EnrolmentId == enrolmentId);
        if (enrolment is null) return NotFoundProblem($"No enrolment with id {enrolmentId}.");

        var allowed = CurrentRole == UserRole.Participant
            ? enrolment.ParticipantId == userId
            : enrolment.Category.Event.OrganiserId == userId;
        if (!allowed) return ForbiddenProblem("You are not allowed to view this result.");

        var result = await ResultQuery.FirstOrDefaultAsync(r => r.EnrolmentId == enrolmentId);
        if (result is null) return NotFoundProblem("No result has been captured for this enrolment yet.");
        return result.ToDto();
    }

    /// <summary>Gets the results (leaderboard) for a category, ordered by finishing position. Public - no login needed.</summary>
    /// <response code="200">The category's results, best position first.</response>
    /// <response code="404">No category with that id.</response>
    [HttpGet("api/categories/{categoryId:int}/results")]
    [ProducesResponseType(typeof(List<ResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<ResultDto>>> GetCategoryResults(int categoryId)
    {
        if (!await _db.Categories.AnyAsync(c => c.CategoryId == categoryId))
            return NotFoundProblem($"No category with id {categoryId}.");

        var results = await ResultQuery.Where(r => r.Enrolment.CategoryId == categoryId).ToListAsync();
        return results
            .OrderBy(r => r.Position ?? int.MaxValue)
            .ThenBy(r => r.FinishTime)
            .Select(r => r.ToDto())
            .ToList();
    }

    /// <summary>Corrects a result that was already captured. Only the Organiser who owns the event may do this.</summary>
    /// <remarks>Expects the same fields as capturing a result: finishTime, position and status.</remarks>
    /// <response code="200">The updated result.</response>
    /// <response code="400">Validation failed.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Not an Organiser, or the event belongs to another Organiser.</response>
    /// <response code="404">No enrolment, or no result captured yet.</response>
    [HttpPut("api/enrolments/{enrolmentId:int}/result")]
    [SessionAuthorize(UserRole.Organiser)]
    [ProducesResponseType(typeof(ResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ResultDto>> UpdateResult(int enrolmentId, CaptureResultRequest request)
    {
        var enrolment = await EnrolmentQuery.FirstOrDefaultAsync(e => e.EnrolmentId == enrolmentId);
        if (enrolment is null) return NotFoundProblem($"No enrolment with id {enrolmentId}.");
        if (enrolment.Category.Event.OrganiserId != CurrentUserId)
            return ForbiddenProblem("You can only change results for your own events.");

        var result = await _db.Results.FirstOrDefaultAsync(r => r.EnrolmentId == enrolmentId);
        if (result is null) return NotFoundProblem("No result has been captured for this enrolment yet.");

        var status = request.Status ?? "Finished";
        if (!AllowedStatuses.Contains(status))
            return BadRequestProblem($"Status must be one of: {string.Join(", ", AllowedStatuses)}.");
        if (status == "Finished" && request.FinishTime is null)
            return BadRequestProblem("finishTime is required when the status is Finished.");

        result.FinishTime = request.FinishTime;
        result.Position = request.Position;
        result.Status = status;
        await _db.SaveChangesAsync();

        result.Enrolment = enrolment;
        return result.ToDto();
    }

    /// <summary>Gets the logged-in Participant's race history: event, date, category, finish time, position and total finishers.</summary>
    /// <response code="200">The participant's results, most recent event first.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Logged in as an Organiser.</response>
    [HttpGet("api/users/me/results")]
    [SessionAuthorize(UserRole.Participant)]
    [ProducesResponseType(typeof(List<MyResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<List<MyResultDto>>> GetMyResults()
    {
        var userId = CurrentUserId;
        var mine = await ResultQuery.Where(r => r.Enrolment.ParticipantId == userId).ToListAsync();

        var categoryIds = mine.Select(r => r.Enrolment.CategoryId).Distinct().ToList();
        var totals = await _db.Results
            .Where(r => r.Status == "Finished" && categoryIds.Contains(r.Enrolment.CategoryId))
            .GroupBy(r => r.Enrolment.CategoryId)
            .Select(g => new { CategoryId = g.Key, Total = g.Count() })
            .ToListAsync();
        var lookup = totals.ToDictionary(t => t.CategoryId, t => t.Total);

        return mine
            .OrderByDescending(r => r.Enrolment.Category.Event.EventDate)
            .Select(r => new MyResultDto
            {
                ResultId = r.ResultId,
                EnrolmentId = r.EnrolmentId,
                EventName = r.Enrolment.Category.Event.Name,
                EventDate = r.Enrolment.Category.Event.EventDate,
                Location = r.Enrolment.Category.Event.Location,
                CategoryName = r.Enrolment.Category.Name,
                FinishTime = r.FinishTime,
                Position = r.Position,
                TotalFinishers = lookup.GetValueOrDefault(r.Enrolment.CategoryId),
                Status = r.Status
            })
            .ToList();
    }
}
