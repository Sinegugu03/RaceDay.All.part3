using Microsoft.AspNetCore.Mvc;
using RaceDay.Mvc.Filters;
using RaceDay.Mvc.Models;
using RaceDay.Mvc.Services;

namespace RaceDay.Mvc.Controllers;

[RequireLogin("Organiser")]
public class OrganiserController : AppController
{
    public OrganiserController(IApiClient api) : base(api) { }

    // ---------- Dashboard ----------

    public async Task<IActionResult> Dashboard()
    {
        var result = await Api.GetAsync<List<OrganiserEventDto>>("api/events/mine");
        if (result.Unauthorized) return SessionExpired();

        var events = result.Data ?? new List<OrganiserEventDto>();
        var today = DateOnly.FromDateTime(DateTime.Today);
        var upcoming = events.Where(e => e.EventDate >= today).OrderBy(e => e.EventDate).ToList();

        return View(new OrganiserDashboardViewModel
        {
            Events = events,
            NextEvents = upcoming.Take(5).ToList(),
            TotalEvents = events.Count,
            UpcomingEvents = upcoming.Count,
            TotalEnrolments = events.Sum(e => e.EnrolmentCount),
            Error = result.Success ? null : result.Error
        });
    }

    // ---------- Events management ----------

    public async Task<IActionResult> Events()
    {
        var result = await Api.GetAsync<List<OrganiserEventDto>>("api/events/mine");
        if (result.Unauthorized) return SessionExpired();
        if (!result.Success) Fail(result.Error ?? "We could not load your events.");
        return View(result.Data ?? new List<OrganiserEventDto>());
    }

    [HttpGet]
    public IActionResult CreateEvent() =>
        View("EventForm", new EventFormViewModel { EventDate = DateTime.Today.AddDays(30) });

    [HttpPost]
    public async Task<IActionResult> CreateEvent(EventFormViewModel model)
    {
        var imageError = ValidateImage(model.Banner);
        if (imageError is not null) ModelState.AddModelError(nameof(model.Banner), imageError);
        if (!ModelState.IsValid) return View("EventForm", model);

        var created = await Api.PostAsync<EventDto>("api/events", ToApiBody(model));
        if (created.Unauthorized) return SessionExpired();
        if (!created.Success || created.Data is null)
        {
            AddApiErrors(created);
            return View("EventForm", model);
        }

        if (model.Banner is { Length: > 0 })
        {
            var upload = await Api.UploadAsync<EventDto>($"api/events/{created.Data.EventId}/banner", model.Banner);
            if (!upload.Success)
            {
                Fail($"The event was created, but the banner could not be uploaded: {upload.Error}");
                return RedirectToAction(nameof(EditEvent), new { id = created.Data.EventId });
            }
        }

        Success("Event created. Now add categories so participants can enrol.");
        return RedirectToAction(nameof(Categories), new { eventId = created.Data.EventId });
    }

    [HttpGet]
    public async Task<IActionResult> EditEvent(int id)
    {
        var result = await Api.GetAsync<EventDto>($"api/events/{id}");
        if (result.Unauthorized) return SessionExpired();
        if (!result.Success || result.Data is null) return EventNotAvailable(result);
        if (!IsOwner(result.Data)) return RedirectToAction("AccessDenied", "Account");

        var ev = result.Data;
        return View("EventForm", new EventFormViewModel
        {
            EventId = ev.EventId,
            Name = ev.Name,
            Description = ev.Description,
            EventDate = ev.EventDate.ToDateTime(TimeOnly.MinValue),
            Location = ev.Location,
            DistanceKm = ev.DistanceKm,
            EventType = ev.EventType,
            Status = ev.Status,
            HasBanner = ev.HasBanner
        });
    }

    [HttpPost]
    public async Task<IActionResult> EditEvent(EventFormViewModel model)
    {
        var imageError = ValidateImage(model.Banner);
        if (imageError is not null) ModelState.AddModelError(nameof(model.Banner), imageError);
        if (!ModelState.IsValid) return View("EventForm", model);

        var updated = await Api.PutAsync<EventDto>($"api/events/{model.EventId}", ToApiBody(model));
        if (updated.Unauthorized) return SessionExpired();
        if (!updated.Success)
        {
            AddApiErrors(updated);
            return View("EventForm", model);
        }

        if (model.Banner is { Length: > 0 })
        {
            var upload = await Api.UploadAsync<EventDto>($"api/events/{model.EventId}/banner", model.Banner);
            if (!upload.Success)
            {
                Fail($"The event was saved, but the banner could not be uploaded: {upload.Error}");
                return RedirectToAction(nameof(EditEvent), new { id = model.EventId });
            }
        }

        Success("Event updated.");
        return RedirectToAction(nameof(Events));
    }

    [HttpPost]
    public async Task<IActionResult> DeleteEvent(int id)
    {
        var result = await Api.DeleteAsync($"api/events/{id}");
        if (result.Unauthorized) return SessionExpired();

        if (result.Success) Success("Event deleted.");
        else Fail(result.Error ?? "We could not delete that event.");
        return RedirectToAction(nameof(Events));
    }

    // ---------- Categories ----------

    [HttpGet]
    public async Task<IActionResult> Categories(int eventId) => await CategoriesPage(eventId, new CategoryFormViewModel { EventId = eventId });

    [HttpPost]
    public async Task<IActionResult> AddCategory([Bind(Prefix = "NewCategory")] CategoryFormViewModel model)
    {
        if (!ModelState.IsValid) return await CategoriesPage(model.EventId, model);

        var result = await Api.PostAsync<CategoryDto>($"api/events/{model.EventId}/categories", new
        {
            name = model.Name,
            distanceKm = model.DistanceKm,
            maxParticipants = model.MaxParticipants,
            fee = model.Fee,
            startTime = NormaliseTime(model.StartTime)
        });
        if (result.Unauthorized) return SessionExpired();
        if (!result.Success)
        {
            AddApiErrors(result);
            return await CategoriesPage(model.EventId, model);
        }

        Success($"Category \"{model.Name}\" added.");
        return RedirectToAction(nameof(Categories), new { eventId = model.EventId });
    }

    [HttpPost]
    public async Task<IActionResult> DeleteCategory(int eventId, int categoryId)
    {
        var result = await Api.DeleteAsync($"api/categories/{categoryId}");
        if (result.Unauthorized) return SessionExpired();

        if (result.Success) Success("Category deleted.");
        else Fail(result.Error ?? "We could not delete that category.");
        return RedirectToAction(nameof(Categories), new { eventId });
    }

    // ---------- Enrolments ----------

    [HttpGet]
    public async Task<IActionResult> Enrolments(int eventId)
    {
        var page = await LoadEventWithEnrolments(eventId);
        return page.Result ?? View(page.Model);
    }

    [HttpPost]
    public async Task<IActionResult> SetEnrolmentStatus(int eventId, int enrolmentId, string status)
    {
        var result = await Api.PutAsync<EnrolmentDto>($"api/enrolments/{enrolmentId}/status", new { status });
        if (result.Unauthorized) return SessionExpired();

        if (result.Success) Success($"Enrolment marked as {status}.");
        else Fail(result.Error ?? "We could not change that enrolment.");
        return RedirectToAction(nameof(Enrolments), new { eventId });
    }

    // ---------- Results ----------

    [HttpGet]
    public async Task<IActionResult> Results(int eventId)
    {
        var page = await LoadEventWithEnrolments(eventId);
        return page.Result ?? View(page.Model);
    }

    [HttpPost]
    public async Task<IActionResult> SaveResult(ResultFormViewModel model)
    {
        if (!ModelState.IsValid)
        {
            Fail("Please check the finish time (hh:mm:ss) and the position, then try again.");
            return RedirectToAction(nameof(Results), new { eventId = model.EventId });
        }

        var body = new { finishTime = NormaliseTime(model.FinishTime), position = model.Position, status = model.Status };
        var url = $"api/enrolments/{model.EnrolmentId}/result";
        var result = model.HasResult
            ? await Api.PutAsync<object>(url, body)
            : await Api.PostAsync<object>(url, body);
        if (result.Unauthorized) return SessionExpired();

        if (result.Success) Success("Result saved and published.");
        else Fail(result.Error ?? "We could not save that result.");
        return RedirectToAction(nameof(Results), new { eventId = model.EventId });
    }

    // ---------- Helpers ----------

    private bool IsOwner(EventDto ev) => ev.OrganiserId == HttpContext.Session.GetUserId();

    private static object ToApiBody(EventFormViewModel m) => new
    {
        name = m.Name,
        description = m.Description,
        eventDate = m.EventDate!.Value.ToString("yyyy-MM-dd"),
        location = m.Location,
        distanceKm = m.DistanceKm,
        eventType = m.EventType,
        status = m.Status
    };

    private IActionResult EventNotAvailable<T>(ApiResult<T> result) =>
        View("Error", new ErrorViewModel
        {
            Title = result.StatusCode == 404 ? "Event not found" : "Something went wrong",
            Message = result.StatusCode == 404 ? "That event does not exist or has been removed." : result.Error ?? "Please try again."
        });

    private async Task<IActionResult> CategoriesPage(int eventId, CategoryFormViewModel form)
    {
        var result = await Api.GetAsync<EventDto>($"api/events/{eventId}");
        if (result.Unauthorized) return SessionExpired();
        if (!result.Success || result.Data is null) return EventNotAvailable(result);
        if (!IsOwner(result.Data)) return RedirectToAction("AccessDenied", "Account");

        return View("Categories", new CategoriesViewModel { Event = result.Data, NewCategory = form });
    }

    private async Task<(IActionResult? Result, EventEnrolmentsViewModel Model)> LoadEventWithEnrolments(int eventId)
    {
        var model = new EventEnrolmentsViewModel();

        var ev = await Api.GetAsync<EventDto>($"api/events/{eventId}");
        if (ev.Unauthorized) return (SessionExpired(), model);
        if (!ev.Success || ev.Data is null) return (EventNotAvailable(ev), model);
        if (!IsOwner(ev.Data)) return (RedirectToAction("AccessDenied", "Account"), model);

        var list = await Api.GetAsync<List<EnrolmentDto>>($"api/events/{eventId}/enrolments");
        if (list.Unauthorized) return (SessionExpired(), model);
        if (!list.Success) Fail(list.Error ?? "We could not load the enrolments.");

        model.Event = ev.Data;
        model.Enrolments = list.Data ?? new List<EnrolmentDto>();
        return (null, model);
    }
}
