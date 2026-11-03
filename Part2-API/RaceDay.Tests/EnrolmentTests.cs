using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace RaceDay.Tests;

/// <summary>Participants enrol in categories and the link is recorded correctly; roles are enforced.</summary>
public class EnrolmentTests : IClassFixture<RaceDayFactory>
{
    private readonly RaceDayFactory _factory;

    public EnrolmentTests(RaceDayFactory factory) => _factory = factory;

    [Fact]
    public async Task Participant_CanEnrol_AndTheEnrolmentLinksParticipantEventAndCategory()
    {
        var organiser = await Api.LoginAs(_factory, "Organiser");
        var participantEmail = Api.NewEmail();
        var participant = await Api.LoginAs(_factory, "Participant", participantEmail);
        var eventId = await Api.CreateEvent(organiser, "Linked Race");
        var categoryId = await Api.CreateCategory(organiser, eventId);

        var response = await participant.PostAsync($"/api/categories/{categoryId}/enrolments", null);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(eventId, json.GetProperty("eventId").GetInt32());
        Assert.Equal(categoryId, json.GetProperty("categoryId").GetInt32());
        Assert.Equal("Linked Race", json.GetProperty("eventName").GetString());
        Assert.Equal("Senior", json.GetProperty("categoryName").GetString());
        Assert.True(json.GetProperty("participantId").GetInt32() > 0);
    }

    [Fact]
    public async Task EnrolmentIsRecorded_AndShowsInTheParticipantsOwnList()
    {
        var organiser = await Api.LoginAs(_factory, "Organiser");
        var participant = await Api.LoginAs(_factory, "Participant");
        var categoryId = await Api.CreateCategory(organiser, await Api.CreateEvent(organiser));
        var enrolmentId = await Api.Enrol(participant, categoryId);

        var response = await participant.GetAsync("/api/users/me/enrolments");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var list = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains(list.EnumerateArray(), e => e.GetProperty("enrolmentId").GetInt32() == enrolmentId);
    }

    [Fact]
    public async Task Organiser_CannotEnrol_Returns403()
    {
        var organiser = await Api.LoginAs(_factory, "Organiser");
        var categoryId = await Api.CreateCategory(organiser, await Api.CreateEvent(organiser));

        var response = await organiser.PostAsync($"/api/categories/{categoryId}/enrolments", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UnauthenticatedUser_CannotEnrol_Returns401()
    {
        var response = await _factory.CreateClient().PostAsync("/api/categories/1/enrolments", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Enrol_InUnknownCategory_Returns404()
    {
        var participant = await Api.LoginAs(_factory, "Participant");

        var response = await participant.PostAsync("/api/categories/999999/enrolments", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Enrol_Twice_InTheSameCategory_Returns409()
    {
        var organiser = await Api.LoginAs(_factory, "Organiser");
        var participant = await Api.LoginAs(_factory, "Participant");
        var categoryId = await Api.CreateCategory(organiser, await Api.CreateEvent(organiser));
        await Api.Enrol(participant, categoryId);

        var second = await participant.PostAsync($"/api/categories/{categoryId}/enrolments", null);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Enrol_WhenCategoryIsFull_Returns409()
    {
        var organiser = await Api.LoginAs(_factory, "Organiser");
        var first = await Api.LoginAs(_factory, "Participant");
        var second = await Api.LoginAs(_factory, "Participant");
        var categoryId = await Api.CreateCategory(organiser, await Api.CreateEvent(organiser), maxParticipants: 1);
        await Api.Enrol(first, categoryId);

        var response = await second.PostAsync($"/api/categories/{categoryId}/enrolments", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Enrol_WhenEventIsClosed_Returns409()
    {
        var organiser = await Api.LoginAs(_factory, "Organiser");
        var participant = await Api.LoginAs(_factory, "Participant");
        var eventId = await Api.CreateEvent(organiser);
        var categoryId = await Api.CreateCategory(organiser, eventId);
        await organiser.PutAsJsonAsync($"/api/events/{eventId}", new
        {
            name = "Closed Race", eventDate = Api.FutureDate(), location = "Durban",
            distanceKm = 10, eventType = "Run", status = "Closed"
        });

        var response = await participant.PostAsync($"/api/categories/{categoryId}/enrolments", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_CanViewEnrolmentsForTheirOwnEvent()
    {
        var organiser = await Api.LoginAs(_factory, "Organiser");
        var participant = await Api.LoginAs(_factory, "Participant");
        var categoryId = await Api.CreateCategory(organiser, await Api.CreateEvent(organiser));
        await Api.Enrol(participant, categoryId);

        var response = await organiser.GetAsync($"/api/categories/{categoryId}/enrolments");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var list = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, list.GetArrayLength());
    }

    [Fact]
    public async Task Participant_CannotViewCategoryEnrolments_Returns403()
    {
        var organiser = await Api.LoginAs(_factory, "Organiser");
        var participant = await Api.LoginAs(_factory, "Participant");
        var categoryId = await Api.CreateCategory(organiser, await Api.CreateEvent(organiser));

        var response = await participant.GetAsync($"/api/categories/{categoryId}/enrolments");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_CannotViewEnrolmentsOfAnotherOrganisersEvent_Returns403()
    {
        var owner = await Api.LoginAs(_factory, "Organiser");
        var other = await Api.LoginAs(_factory, "Organiser");
        var categoryId = await Api.CreateCategory(owner, await Api.CreateEvent(owner));

        var response = await other.GetAsync($"/api/categories/{categoryId}/enrolments");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Participant_CanCancelOwnEnrolment_ButNotSomeoneElses()
    {
        var organiser = await Api.LoginAs(_factory, "Organiser");
        var owner = await Api.LoginAs(_factory, "Participant");
        var stranger = await Api.LoginAs(_factory, "Participant");
        var categoryId = await Api.CreateCategory(organiser, await Api.CreateEvent(organiser));
        var enrolmentId = await Api.Enrol(owner, categoryId);

        var byStranger = await stranger.DeleteAsync($"/api/enrolments/{enrolmentId}");
        var byOwner = await owner.DeleteAsync($"/api/enrolments/{enrolmentId}");

        Assert.Equal(HttpStatusCode.Forbidden, byStranger.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, byOwner.StatusCode);
    }
}
