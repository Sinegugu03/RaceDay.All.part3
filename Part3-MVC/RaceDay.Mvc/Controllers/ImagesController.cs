using Microsoft.AspNetCore.Mvc;
using RaceDay.Mvc.Services;

namespace RaceDay.Mvc.Controllers;

/// <summary>
/// Serves event banners and profile pictures. The images are fetched from the Part 2 API - this app
/// never talks to Azure Blob Storage directly.
/// </summary>
public class ImagesController : AppController
{
    public ImagesController(IApiClient api) : base(api) { }

    [HttpGet("images/event/{id:int}")]
    public Task<IActionResult> EventBanner(int id) =>
        ServeAsync($"api/events/{id}/banner", "/img/event-placeholder.svg");

    [HttpGet("images/profile/{id:int}")]
    public Task<IActionResult> ProfilePicture(int id) =>
        ServeAsync($"api/users/{id}/picture", "/img/avatar-placeholder.svg");

    private async Task<IActionResult> ServeAsync(string apiUrl, string placeholder)
    {
        var result = await Api.GetFileAsync(apiUrl);
        if (!result.Success || result.Data is null) return Redirect(placeholder);

        Response.Headers.CacheControl = "private, max-age=60";
        return File(result.Data.Content, result.Data.ContentType);
    }
}
