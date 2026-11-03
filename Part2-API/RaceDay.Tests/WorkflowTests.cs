using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace RaceDay.Tests;

/// <summary>Endpoints added for the Part 3 front end: organiser dashboard data, enrolment statuses and personal results.</summary>
public class WorkflowTests : IClassFixture<RaceDayFactory>
{
    private readonly RaceDayFactory _factory;

    public WorkflowTests(RaceDayFactory factory) => _factory = factory;

    private static object ResultBody(int position) => new { finishTime = "00:48:30", position, status = "Finished" };

    [Fact]
    public async Task MyEvents_ReturnsOnlyTheOrganisersOwnEvents_WithEnrolmentCounts()
    {
        var organiser = await Api.LoginAs(_factory, "Organiser");
        var other = await Api.LoginAs(_factory, "Organiser");
        var participant = await Api.LoginAs(_factory, "Participant");
        var eventId = await Api.CreateEvent(organiser, "Dashboard Race");
        await Api.CreateEvent(other, "Someone Elses Race");
        var categoryId = await Api.CreateCategory(organiser, eventId);
        await Api.Enrol(participant, categoryId);

        var response = await organiser.GetAsync("/api/events/mine");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var list = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, list.GetArrayLength());
        Assert.Equal("Dashboard Race", list[0].GetProperty("name").GetString());
        Assert.Equal(1, list[0].GetProperty("enrolmentCount").GetInt32());
        Assert.Equal(1, list[0].GetProperty("categoryCount").GetInt32());
    }

    [Fact]
    public async Task MyEvents_AsParticipant_Returns403()
    {
        var participant = await Api.LoginAs(_factory, "Participant");

        var response = await participant.GetAsync("/api/events/mine");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_CanListAllEnrolmentsForTheirEvent()
    {
        var organiser = await Api.LoginAs(_factory, "Organiser");
        var first = await Api.LoginAs(_factory, "Participant");
        var second = await Api.LoginAs(_factory, "Participant");
        var eventId = await Api.CreateEvent(organiser);
        var categoryId = await Api.CreateCategory(organiser, eventId);
        await Api.Enrol(first, categoryId);
        await Api.Enrol(second, categoryId);

        var response = await organiser.GetAsync($"/api/events/{eventId}/enrolments");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var list = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, list.GetArrayLength());
        Assert.Equal("Senior", list[0].GetProperty("categoryName").GetString());
    }

    [Fact]
    public async Task OtherOrganiser_CannotListEnrolmentsForSomeoneElsesEvent_Returns403()
    {
        var owner = await Api.LoginAs(_factory, "Organiser");
        var other = await Api.LoginAs(_factory, "Organiser");
        var eventId = await Api.CreateEvent(owner);

        var response = await other.GetAsync($"/api/events/{eventId}/enrolments");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task NewEnrolments_StartAsPending()
    {
        var organiser = await Api.LoginAs(_factory, "Organiser");
        var participant = await Api.LoginAs(_factory, "Participant");
        var categoryId = await Api.CreateCategory(organiser, await Api.CreateEvent(organiser));

        var response = await participant.PostAsync($"/api/categories/{categoryId}/enrolments", null);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Pending", json.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Organiser_CanConfirmAnEnrolment()
    {
        var organiser = await Api.LoginAs(_factory, "Organiser");
        var participant = await Api.LoginAs(_factory, "Participant");
        var categoryId = await Api.CreateCategory(organiser, await Api.CreateEvent(organiser));
        var enrolmentId = await Api.Enrol(participant, categoryId);

        var response = await organiser.PutAsJsonAsync($"/api/enrolments/{enrolmentId}/status", new { status = "Confirmed" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Confirmed", json.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Participant_CannotChangeAnEnrolmentStatus_Returns403()
    {
        var organiser = await Api.LoginAs(_factory, "Organiser");
        var participant = await Api.LoginAs(_factory, "Participant");
        var categoryId = await Api.CreateCategory(organiser, await Api.CreateEvent(organiser));
        var enrolmentId = await Api.Enrol(participant, categoryId);

        var response = await participant.PutAsJsonAsync($"/api/enrolments/{enrolmentId}/status", new { status = "Confirmed" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ChangingAnEnrolmentToAnInvalidStatus_Returns400()
    {
        var organiser = await Api.LoginAs(_factory, "Organiser");
        var participant = await Api.LoginAs(_factory, "Participant");
        var categoryId = await Api.CreateCategory(organiser, await Api.CreateEvent(organiser));
        var enrolmentId = await Api.Enrol(participant, categoryId);

        var response = await organiser.PutAsJsonAsync($"/api/enrolments/{enrolmentId}/status", new { status = "Whatever" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_CanCorrectACapturedResult()
    {
        var organiser = await Api.LoginAs(_factory, "Organiser");
        var participant = await Api.LoginAs(_factory, "Participant");
        var categoryId = await Api.CreateCategory(organiser, await Api.CreateEvent(organiser));
        var enrolmentId = await Api.Enrol(participant, categoryId);
        await organiser.PostAsJsonAsync($"/api/enrolments/{enrolmentId}/result", ResultBody(5));

        var response = await organiser.PutAsJsonAsync($"/api/enrolments/{enrolmentId}/result", ResultBody(3));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(3, json.GetProperty("position").GetInt32());
    }

    [Fact]
    public async Task CorrectingAResultThatDoesNotExistYet_Returns404()
    {
        var organiser = await Api.LoginAs(_factory, "Organiser");
        var participant = await Api.LoginAs(_factory, "Participant");
        var categoryId = await Api.CreateCategory(organiser, await Api.CreateEvent(organiser));
        var enrolmentId = await Api.Enrol(participant, categoryId);

        var response = await organiser.PutAsJsonAsync($"/api/enrolments/{enrolmentId}/result", ResultBody(1));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task EventEnrolments_IncludeTheCapturedResult()
    {
        var organiser = await Api.LoginAs(_factory, "Organiser");
        var participant = await Api.LoginAs(_factory, "Participant");
        var eventId = await Api.CreateEvent(organiser);
        var categoryId = await Api.CreateCategory(organiser, eventId);
        var enrolmentId = await Api.Enrol(participant, categoryId);
        await organiser.PostAsJsonAsync($"/api/enrolments/{enrolmentId}/result", ResultBody(2));

        var list = await organiser.GetFromJsonAsync<JsonElement>($"/api/events/{eventId}/enrolments");

        Assert.Equal(2, list[0].GetProperty("position").GetInt32());
        Assert.Equal("Finished", list[0].GetProperty("resultStatus").GetString());
    }

    [Fact]
    public async Task MyResults_ShowPositionAndTotalFinishers()
    {
        var organiser = await Api.LoginAs(_factory, "Organiser");
        var runnerA = await Api.LoginAs(_factory, "Participant");
        var runnerB = await Api.LoginAs(_factory, "Participant");
        var eventId = await Api.CreateEvent(organiser, "History Race");
        var categoryId = await Api.CreateCategory(organiser, eventId);
        var enrolmentA = await Api.Enrol(runnerA, categoryId);
        var enrolmentB = await Api.Enrol(runnerB, categoryId);
        await organiser.PostAsJsonAsync($"/api/enrolments/{enrolmentA}/result", ResultBody(2));
        await organiser.PostAsJsonAsync($"/api/enrolments/{enrolmentB}/result", ResultBody(1));

        var response = await runnerA.GetAsync("/api/users/me/results");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var list = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, list.GetArrayLength());
        Assert.Equal("History Race", list[0].GetProperty("eventName").GetString());
        Assert.Equal(2, list[0].GetProperty("position").GetInt32());
        Assert.Equal(2, list[0].GetProperty("totalFinishers").GetInt32());
    }

    [Fact]
    public async Task MyResults_AsOrganiser_Returns403()
    {
        var organiser = await Api.LoginAs(_factory, "Organiser");

        var response = await organiser.GetAsync("/api/users/me/results");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task MyResults_WhenNotLoggedIn_Returns401()
    {
        var response = await _factory.CreateClient().GetAsync("/api/users/me/results");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
