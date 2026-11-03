using Microsoft.AspNetCore.Mvc;
using RaceDay.Mvc.Filters;
using RaceDay.Mvc.Models;
using RaceDay.Mvc.Services;

namespace RaceDay.Mvc.Controllers;

public class EventsController : AppController
{
    public EventsController(IApiClient api) : base(api) { }

    /// <summary>Full event information and the categories a Participant can enrol in.</summary>
    public async Task<IActionResult> Details(int id)
    {
        var result = await Api.GetAsync<EventDto>($"api/events/{id}");
        if (!result.Success || result.Data is null)
        {
            return View("Error", new ErrorViewModel
            {
                Title = result.StatusCode == 404 ? "Event not found" : "Something went wrong",
                Message = result.StatusCode == 404 ? "That event does not exist or has been removed." : result.Error ?? "Please try again."
            });
        }

        var model = new EventDetailsViewModel { Event = result.Data, Role = HttpContext.Session.GetRole() };

        if (model.Role == "Participant")
        {
            var mine = await Api.GetAsync<List<EnrolmentDto>>("api/users/me/enrolments");
            if (mine.Unauthorized) return SessionExpired();
            if (mine.Success && mine.Data is not null)
                model.EnrolledCategoryIds = mine.Data.Select(e => e.CategoryId).ToHashSet();
        }

        return View(model);
    }

    [HttpPost]
    [RequireLogin("Participant")]
    public async Task<IActionResult> Enrol(int eventId, int categoryId)
    {
        var result = await Api.PostAsync<EnrolmentDto>($"api/categories/{categoryId}/enrolments");
        if (result.Unauthorized) return SessionExpired();

        if (!result.Success)
        {
            Fail(result.Error ?? "We could not enrol you. Please try again.");
            return RedirectToAction(nameof(Details), new { id = eventId });
        }

        Success($"You are enrolled in {result.Data?.EventName} ({result.Data?.CategoryName}). The organiser will confirm your place.");
        return RedirectToAction("MyEnrolments", "Participant");
    }
}
