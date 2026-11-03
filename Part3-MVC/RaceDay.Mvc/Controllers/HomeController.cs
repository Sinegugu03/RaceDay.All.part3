using Microsoft.AspNetCore.Mvc;
using RaceDay.Mvc.Helpers;
using RaceDay.Mvc.Models;
using RaceDay.Mvc.Services;

namespace RaceDay.Mvc.Controllers;

public class HomeController : AppController
{
    public HomeController(IApiClient api) : base(api) { }

    /// <summary>Upcoming events with filtering. Organisers go straight to their dashboard.</summary>
    public async Task<IActionResult> Index(
        string? location, string? type,
        [FromQuery(Name = "from")] DateTime? fromDate, [FromQuery(Name = "to")] DateTime? toDate)
    {
        if (HttpContext.Session.GetRole() == "Organiser") return RedirectToAction("Dashboard", "Organiser");

        var query = Format.Query(
            ("location", location),
            ("type", type),
            ("from", (fromDate ?? DateTime.Today).ToString("yyyy-MM-dd")),
            ("to", toDate?.ToString("yyyy-MM-dd")));

        var result = await Api.GetAsync<List<EventDto>>("api/events" + query);

        return View(new HomeViewModel
        {
            Filter = new EventFilterViewModel { Location = location, Type = type, From = fromDate, To = toDate },
            Events = result.Data ?? new List<EventDto>(),
            Error = result.Success ? null : result.Error
        });
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() =>
        View("Error", new ErrorViewModel
        {
            Title = "Something went wrong",
            Message = "Sorry, something unexpected happened on our side. Please try again in a moment."
        });

    public IActionResult StatusPage(int code)
    {
        var model = code switch
        {
            404 => new ErrorViewModel { Title = "Page not found", Message = "We couldn't find the page you were looking for." },
            403 => new ErrorViewModel { Title = "No access", Message = "You do not have permission to open that page." },
            _ => new ErrorViewModel()
        };
        Response.StatusCode = code;
        return View("Error", model);
    }
}
