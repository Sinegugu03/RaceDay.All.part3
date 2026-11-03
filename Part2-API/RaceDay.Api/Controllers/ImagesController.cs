using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Filters;
using RaceDay.Api.Models;
using RaceDay.Api.Services;

namespace RaceDay.Api.Controllers;

/// <summary>Event banners and profile pictures, stored in Azure Blob Storage. Every image goes through the API.</summary>
[ApiController]
public class ImagesController : ApiControllerBase
{
    private const long MaxBytes = 5 * 1024 * 1024;

    private static readonly Dictionary<string, string> AllowedTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["image/jpeg"] = ".jpg",
        ["image/png"] = ".png",
        ["image/webp"] = ".webp"
    };

    private readonly RaceDayDbContext _db;
    private readonly IBlobStorageService _blobs;

    public ImagesController(RaceDayDbContext db, IBlobStorageService blobs)
    {
        _db = db;
        _blobs = blobs;
    }

    // ---------- Event banner ----------

    /// <summary>Uploads (or replaces) an event's banner image. Only the Organiser who owns the event may do this.</summary>
    /// <remarks>Expects a multipart/form-data upload with one file field named "file". JPG, PNG or WEBP, maximum 5 MB. The image is stored in Azure Blob Storage.</remarks>
    /// <response code="200">The updated event (hasBanner is now true).</response>
    /// <response code="400">Missing file, wrong type, not a real image, or larger than 5 MB.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Not an Organiser, or the event belongs to another Organiser.</response>
    /// <response code="404">No event with that id.</response>
    /// <response code="503">Image storage is not configured.</response>
    [HttpPost("api/events/{id:int}/banner")]
    [SessionAuthorize(UserRole.Organiser)]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(EventDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<EventDto>> UploadBanner(int id, [FromForm] IFormFile file)
    {
        var ev = await _db.Events.Include(e => e.Organiser).FirstOrDefaultAsync(e => e.EventId == id);
        if (ev is null) return NotFoundProblem($"No event with id {id}.");
        if (ev.OrganiserId != CurrentUserId) return ForbiddenProblem("You can only upload banners for your own events.");

        var problem = await CheckImageAsync(file);
        if (problem is not null) return problem;

        await using var stream = file.OpenReadStream();
        var blobName = await _blobs.UploadAsync(stream, file.ContentType, "events", AllowedTypes[file.ContentType]);

        var previous = ev.BannerBlobName;
        ev.BannerBlobName = blobName;
        await _db.SaveChangesAsync();
        await TryDeleteAsync(previous);

        return ev.ToDto();
    }

    /// <summary>Gets an event's banner image (the image file itself). Public - no login needed.</summary>
    /// <response code="200">The image.</response>
    /// <response code="404">The event has no banner.</response>
    [HttpGet("api/events/{id:int}/banner")]
    [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetBanner(int id)
    {
        var ev = await _db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.EventId == id);
        if (ev is null || string.IsNullOrEmpty(ev.BannerBlobName) || !_blobs.IsConfigured)
            return NotFoundProblem("This event has no banner image.");

        var file = await _blobs.DownloadAsync(ev.BannerBlobName);
        if (file is null) return NotFoundProblem("The banner image could not be found.");
        return File(file.Content, file.ContentType);
    }

    // ---------- Profile picture ----------

    /// <summary>Uploads (or replaces) the logged-in user's profile picture.</summary>
    /// <remarks>Expects a multipart/form-data upload with one file field named "file". JPG, PNG or WEBP, maximum 5 MB.</remarks>
    /// <response code="200">The updated profile (hasProfilePicture is now true).</response>
    /// <response code="400">Missing file, wrong type, not a real image, or larger than 5 MB.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="503">Image storage is not configured.</response>
    [HttpPost("api/users/me/picture")]
    [SessionAuthorize]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<UserDto>> UploadProfilePicture([FromForm] IFormFile file)
    {
        var user = await _db.Users.FindAsync(CurrentUserId);
        if (user is null) return NotFoundProblem("Your account no longer exists.");

        var problem = await CheckImageAsync(file);
        if (problem is not null) return problem;

        await using var stream = file.OpenReadStream();
        var blobName = await _blobs.UploadAsync(stream, file.ContentType, "profiles", AllowedTypes[file.ContentType]);

        var previous = user.ProfilePictureBlobName;
        user.ProfilePictureBlobName = blobName;
        await _db.SaveChangesAsync();
        await TryDeleteAsync(previous);

        return user.ToDto();
    }

    /// <summary>Gets a user's profile picture (the image file itself). Requires a login.</summary>
    /// <response code="200">The image.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="404">The user has no profile picture.</response>
    [HttpGet("api/users/{id:int}/picture")]
    [SessionAuthorize]
    [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProfilePicture(int id)
    {
        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == id);
        if (user is null || string.IsNullOrEmpty(user.ProfilePictureBlobName) || !_blobs.IsConfigured)
            return NotFoundProblem("This user has no profile picture.");

        var file = await _blobs.DownloadAsync(user.ProfilePictureBlobName);
        if (file is null) return NotFoundProblem("The profile picture could not be found.");
        return File(file.Content, file.ContentType);
    }

    // ---------- Helpers ----------

    private async Task<ObjectResult?> CheckImageAsync(IFormFile? file)
    {
        if (!_blobs.IsConfigured)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new ProblemDetails
            {
                Status = StatusCodes.Status503ServiceUnavailable,
                Title = "Service Unavailable",
                Detail = "Image storage is not configured on the server."
            });
        }
        if (file is null || file.Length == 0) return BadRequestProblem("Please choose an image file to upload.");
        if (file.Length > MaxBytes) return BadRequestProblem("The image is too large. The maximum size is 5 MB.");
        if (!AllowedTypes.ContainsKey(file.ContentType)) return BadRequestProblem("Only JPG, PNG or WEBP images are allowed.");
        if (!await HasImageSignatureAsync(file)) return BadRequestProblem("The file is not a valid image.");
        return null;
    }

    /// <summary>Checks the first bytes of the file so a renamed text file cannot be uploaded as an image.</summary>
    private static async Task<bool> HasImageSignatureAsync(IFormFile file)
    {
        await using var stream = file.OpenReadStream();
        var header = new byte[12];
        var read = await stream.ReadAsync(header, 0, header.Length);
        if (read < 4) return false;

        var jpeg = header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF;
        var png = header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47;
        var webp = read >= 12 &&
                   header[0] == 0x52 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x46 &&
                   header[8] == 0x57 && header[9] == 0x45 && header[10] == 0x42 && header[11] == 0x50;
        return jpeg || png || webp;
    }

    private async Task TryDeleteAsync(string? blobName)
    {
        try { await _blobs.DeleteAsync(blobName); }
        catch (Exception) { /* the new image is saved; a leftover old image is harmless */ }
    }
}
