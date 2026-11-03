using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace RaceDay.Tests;

/// <summary>Organisers capture results; participants can only see their own.</summary>
public class ResultTests : IClassFixture<RaceDayFactory>
{
    private readonly RaceDayFactory _factory;

    public ResultTests(RaceDayFactory factory) => _factory = factory;

    private static object ResultBody(int position = 1) => new { finishTime = "00:48:30", position, status = "Finished" };

    [Fact]
    public async Task Organiser_CanCaptureResult_Returns201()
    {
        var organiser = await Api.LoginAs(_factory, "Organiser");
        var participant = await Api.LoginAs(_factory, "Participant");
        var categoryId = await Api.CreateCategory(organiser, await Api.CreateEvent(organiser));
        var enrolmentId = await Api.Enrol(participant, categoryId);

        var response = await organiser.PostAsJsonAsync($"/api/enrolments/{enrolmentId}/result", ResultBody());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, json.GetProperty("position").GetInt32());
        Assert.Equal("Finished", json.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Participant_CannotCaptureResult_Returns403()
    {
        var organiser = await Api.LoginAs(_factory, "Organiser");
        var participant = await Api.LoginAs(_factory, "Participant");
        var categoryId = await Api.CreateCategory(organiser, await Api.CreateEvent(organiser));
        var enrolmentId = await Api.Enrol(participant, categoryId);

        var response = await participant.PostAsJsonAsync($"/api/enrolments/{enrolmentId}/result", ResultBody());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_CannotCaptureResultForAnotherOrganisersEvent_Returns403()
    {
        var owner = await Api.LoginAs(_factory, "Organiser");
        var other = await Api.LoginAs(_factory, "Organiser");
        var participant = await Api.LoginAs(_factory, "Participant");
        var categoryId = await Api.CreateCategory(owner, await Api.CreateEvent(owner));
        var enrolmentId = await Api.Enrol(participant, categoryId);

        var response = await other.PostAsJsonAsync($"/api/enrolments/{enrolmentId}/result", ResultBody());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CaptureResult_Twice_Returns409()
    {
        var organiser = await Api.LoginAs(_factory, "Organiser");
        var participant = await Api.LoginAs(_factory, "Participant");
        var categoryId = await Api.CreateCategory(organiser, await Api.CreateEvent(organiser));
        var enrolmentId = await Api.Enrol(participant, categoryId);
        await organiser.PostAsJsonAsync($"/api/enrolments/{enrolmentId}/result", ResultBody());

        var second = await organiser.PostAsJsonAsync($"/api/enrolments/{enrolmentId}/result", ResultBody(2));

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task CaptureResult_FinishedWithoutFinishTime_Returns400()
    {
        var organiser = await Api.LoginAs(_factory, "Organiser");
        var participant = await Api.LoginAs(_factory, "Participant");
        var categoryId = await Api.CreateCategory(organiser, await Api.CreateEvent(organiser));
        var enrolmentId = await Api.Enrol(participant, categoryId);

        var response = await organiser.PostAsJsonAsync($"/api/enrolments/{enrolmentId}/result", new { position = 1, status = "Finished" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Participant_CanViewOwnResult_ButAnotherParticipantCannot()
    {
        var organiser = await Api.LoginAs(_factory, "Organiser");
        var owner = await Api.LoginAs(_factory, "Participant");
        var stranger = await Api.LoginAs(_factory, "Participant");
        var categoryId = await Api.CreateCategory(organiser, await Api.CreateEvent(organiser));
        var enrolmentId = await Api.Enrol(owner, categoryId);
        await organiser.PostAsJsonAsync($"/api/enrolments/{enrolmentId}/result", ResultBody());

        var byOwner = await owner.GetAsync($"/api/enrolments/{enrolmentId}/result");
        var byStranger = await stranger.GetAsync($"/api/enrolments/{enrolmentId}/result");

        Assert.Equal(HttpStatusCode.OK, byOwner.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, byStranger.StatusCode);
    }

    [Fact]
    public async Task ViewResult_BeforeItIsCaptured_Returns404()
    {
        var organiser = await Api.LoginAs(_factory, "Organiser");
        var participant = await Api.LoginAs(_factory, "Participant");
        var categoryId = await Api.CreateCategory(organiser, await Api.CreateEvent(organiser));
        var enrolmentId = await Api.Enrol(participant, categoryId);

        var response = await participant.GetAsync($"/api/enrolments/{enrolmentId}/result");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CategoryLeaderboard_IsPublic_AndOrderedByPosition()
    {
        var organiser = await Api.LoginAs(_factory, "Organiser");
        var first = await Api.LoginAs(_factory, "Participant");
        var second = await Api.LoginAs(_factory, "Participant");
        var categoryId = await Api.CreateCategory(organiser, await Api.CreateEvent(organiser));
        var enrolmentA = await Api.Enrol(first, categoryId);
        var enrolmentB = await Api.Enrol(second, categoryId);
        await organiser.PostAsJsonAsync($"/api/enrolments/{enrolmentA}/result", ResultBody(position: 2));
        await organiser.PostAsJsonAsync($"/api/enrolments/{enrolmentB}/result", ResultBody(position: 1));

        var response = await _factory.CreateClient().GetAsync($"/api/categories/{categoryId}/results");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var list = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, list.GetArrayLength());
        Assert.Equal(1, list[0].GetProperty("position").GetInt32());
        Assert.Equal(2, list[1].GetProperty("position").GetInt32());
    }
}
