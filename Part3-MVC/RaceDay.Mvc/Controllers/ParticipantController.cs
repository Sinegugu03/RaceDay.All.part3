using Microsoft.AspNetCore.Mvc;
using RaceDay.Mvc.Filters;
using RaceDay.Mvc.Models;
using RaceDay.Mvc.Services;

namespace RaceDay.Mvc.Controllers;

[RequireLogin("Participant")]
public class ParticipantController : AppController
{
    public ParticipantController(IApiClient api) : base(api) { }

    /// <summary>Every event the participant enrolled for, with its status.</summary>
    public async Task<IActionResult> MyEnrolments()
    {
        var result = await Api.GetAsync<List<EnrolmentDto>>("api/users/me/enrolments");
        if (result.Unauthorized) return SessionExpired();
        if (!result.Success) Fail(result.Error ?? "We could not load your enrolments.");
        return View(result.Data ?? new List<EnrolmentDto>());
    }

    [HttpPost]
    public async Task<IActionResult> CancelEnrolment(int id)
    {
        var result = await Api.DeleteAsync($"api/enrolments/{id}");
        if (result.Unauthorized) return SessionExpired();

        if (result.Success) Success("Your enrolment has been cancelled.");
        else Fail(result.Error ?? "We could not cancel that enrolment.");
        return RedirectToAction(nameof(MyEnrolments));
    }

    /// <summary>Personal race history: event, date, category, finish time and position (e.g. 47th of 312).</summary>
    public async Task<IActionResult> MyResults()
    {
        var result = await Api.GetAsync<List<MyResultDto>>("api/users/me/results");
        if (result.Unauthorized) return SessionExpired();
        if (!result.Success) Fail(result.Error ?? "We could not load your results.");
        return View(result.Data ?? new List<MyResultDto>());
    }
}
