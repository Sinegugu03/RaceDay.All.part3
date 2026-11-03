using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace RaceDay.Tests;

/// <summary>Event and category management - Organisers can manage them, everyone else is rejected.</summary>
public class EventTests : IClassFixture<RaceDayFactory>
{
    private readonly RaceDayFactory _factory;

    public EventTests(RaceDayFactory factory) => _factory = factory;

    [Fact]
    public async Task Organiser_CanCreateEvent_Returns201()
    {
        var organiser = await Api.LoginAs(_factory, "Organiser");

        var response = await organiser.PostAsJsonAsync("/api/events", Api.EventPayload());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Run", json.GetProperty("eventType").GetString());
        Assert.Equal("Planned", json.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Participant_CannotCreateEvent_Returns403()
    {
        var participant = await Api.LoginAs(_factory, "Participant");

        var response = await participant.PostAsJsonAsync("/api/events", Api.EventPayload());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UnauthenticatedUser_CannotCreateEvent_Returns401()
    {
        var anonymous = _factory.CreateClient();

        var response = await anonymous.PostAsJsonAsync("/api/events", Api.EventPayload());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateEvent_WithMissingFields_Returns400()
    {
        var organiser = await Api.LoginAs(_factory, "Organiser");

        var response = await organiser.PostAsJsonAsync("/api/events", new { description = "No name, date or type" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateEvent_WithDateInThePast_Returns400()
    {
        var organiser = await Api.LoginAs(_factory, "Organiser");

        var response = await organiser.PostAsJsonAsync("/api/events", new
        {
            name = "Old Race", eventDate = "2020-01-01", location = "Durban", distanceKm = 5, eventType = "Walk"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ViewingEvents_IsPublic_AndIncludesCreatedEvents()
    {
        var organiser = await Api.LoginAs(_factory, "Organiser");
        await Api.CreateEvent(organiser, "Public Visible Race");
        var anonymous = _factory.CreateClient();

        var response = await anonymous.GetAsync("/api/events");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Public Visible Race", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task GetEvent_WithUnknownId_Returns404()
    {
        var anonymous = _factory.CreateClient();

        var response = await anonymous.GetAsync("/api/events/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_CanUpdateOwnEvent()
    {
        var organiser = await Api.LoginAs(_factory, "Organiser");
        var eventId = await Api.CreateEvent(organiser);

        var response = await organiser.PutAsJsonAsync($"/api/events/{eventId}", new
        {
            name = "Renamed Race", description = "Updated", eventDate = Api.FutureDate(45),
            location = "Umhlanga", distanceKm = 21, eventType = "Cycle", status = "Open"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Renamed Race", json.GetProperty("name").GetString());
        Assert.Equal("Open", json.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Organiser_CannotUpdateAnotherOrganisersEvent_Returns403()
    {
        var owner = await Api.LoginAs(_factory, "Organiser");
        var otherOrganiser = await Api.LoginAs(_factory, "Organiser");
        var eventId = await Api.CreateEvent(owner);

        var response = await otherOrganiser.PutAsJsonAsync($"/api/events/{eventId}", Api.EventPayload("Hijacked"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Participant_CannotUpdateEvent_Returns403()
    {
        var organiser = await Api.LoginAs(_factory, "Organiser");
        var participant = await Api.LoginAs(_factory, "Participant");
        var eventId = await Api.CreateEvent(organiser);

        var response = await participant.PutAsJsonAsync($"/api/events/{eventId}", Api.EventPayload("Hijacked"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_CanDeleteOwnEvent_ThenItIsGone()
    {
        var organiser = await Api.LoginAs(_factory, "Organiser");
        var eventId = await Api.CreateEvent(organiser);

        var delete = await organiser.DeleteAsync($"/api/events/{eventId}");
        var get = await organiser.GetAsync($"/api/events/{eventId}");

        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
    }

    [Fact]
    public async Task Participant_CannotDeleteEvent_Returns403()
    {
        var organiser = await Api.LoginAs(_factory, "Organiser");
        var participant = await Api.LoginAs(_factory, "Participant");
        var eventId = await Api.CreateEvent(organiser);

        var response = await participant.DeleteAsync($"/api/events/{eventId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_CanAddCategory_AndAnyoneCanViewIt()
    {
        var organiser = await Api.LoginAs(_factory, "Organiser");
        var eventId = await Api.CreateEvent(organiser);

        var create = await organiser.PostAsJsonAsync($"/api/events/{eventId}/categories", Api.CategoryPayload());
        var list = await _factory.CreateClient().GetAsync($"/api/events/{eventId}/categories");

        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        Assert.Contains("Senior", await list.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Participant_CannotAddCategory_Returns403()
    {
        var organiser = await Api.LoginAs(_factory, "Organiser");
        var participant = await Api.LoginAs(_factory, "Participant");
        var eventId = await Api.CreateEvent(organiser);

        var response = await participant.PostAsJsonAsync($"/api/events/{eventId}/categories", Api.CategoryPayload());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_CannotAddCategoryToAnotherOrganisersEvent_Returns403()
    {
        var owner = await Api.LoginAs(_factory, "Organiser");
        var other = await Api.LoginAs(_factory, "Organiser");
        var eventId = await Api.CreateEvent(owner);

        var response = await other.PostAsJsonAsync($"/api/events/{eventId}/categories", Api.CategoryPayload());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Categories_ForUnknownEvent_Returns404()
    {
        var response = await _factory.CreateClient().GetAsync("/api/events/999999/categories");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
