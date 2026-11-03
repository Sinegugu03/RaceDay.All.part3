using Microsoft.AspNetCore.Mvc;
using RaceDay.Mvc.Services;

namespace RaceDay.Mvc.Controllers;

/// <summary>Shared helpers for friendly messages, API errors and session expiry.</summary>
public abstract class AppController : Controller
{
    protected IApiClient Api { get; }

    protected AppController(IApiClient api) => Api = api;

    protected void Success(string message) => TempData["Success"] = message;

    protected void Fail(string message) => TempData["Error"] = message;

    /// <summary>The API says we are no longer logged in: clear the session and go to the login page.</summary>
    protected IActionResult SessionExpired()
    {
        HttpContext.Session.Clear();
        Fail("Your session has expired. Please log in again.");
        return RedirectToAction("Login", "Account");
    }

    /// <summary>Shows API validation or business-rule problems in the form's validation summary.</summary>
    protected void AddApiErrors<T>(ApiResult<T> result)
    {
        foreach (var message in result.FieldErrors.Values.SelectMany(v => v).Where(m => !string.IsNullOrWhiteSpace(m)))
            ModelState.AddModelError(string.Empty, message);

        if (!ModelState.Values.Any(v => v.Errors.Count > 0) && !string.IsNullOrWhiteSpace(result.Error))
            ModelState.AddModelError(string.Empty, result.Error);
    }

    protected IActionResult RedirectToHome() =>
        HttpContext.Session.GetRole() == "Organiser"
            ? RedirectToAction("Dashboard", "Organiser")
            : RedirectToAction("Index", "Home");

    protected static string? NormaliseTime(string? time)
    {
        if (string.IsNullOrWhiteSpace(time)) return null;
        return time.Length == 5 ? time + ":00" : time;
    }

    private static readonly string[] AllowedImageTypes = { "image/jpeg", "image/png", "image/webp" };

    /// <summary>Returns a friendly message when the chosen file is not an acceptable image (null when it is fine or absent).</summary>
    protected static string? ValidateImage(IFormFile? file)
    {
        if (file is null || file.Length == 0) return null;
        if (file.Length > 5 * 1024 * 1024) return "That image is too large. Please choose one smaller than 5 MB.";
        if (!AllowedImageTypes.Contains(file.ContentType, StringComparer.OrdinalIgnoreCase))
            return "Only JPG, PNG or WEBP images are allowed.";
        return null;
    }
}
