using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace RaceDay.Tests;

/// <summary>Event banners and profile pictures stored through the API (Azure Blob Storage is faked in tests).</summary>
public class ImageTests : IClassFixture<RaceDayFactory>
{
    // Starts with the PNG signature, which is all the API's "is this really an image?" check looks at.
    private static readonly byte[] PngBytes =
        { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52 };

    private readonly RaceDayFactory _factory;

    public ImageTests(RaceDayFactory factory) => _factory = factory;

    private static MultipartFormDataContent FileContent(byte[] bytes, string contentType, string fileName = "image.png")
    {
        var content = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        content.Add(file, "file", fileName);
        return content;
    }

    [Fact]
    public async Task Organiser_CanUploadABanner_AndTheEventThenHasOne()
    {
        var organiser = await Api.LoginAs(_factory, "Organiser");
        var eventId = await Api.CreateEvent(organiser);

        var response = await organiser.PostAsync($"/api/events/{eventId}/banner", FileContent(PngBytes, "image/png"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(json.GetProperty("hasBanner").GetBoolean());
    }

    [Fact]
    public async Task Banner_CanBeDownloadedByAnyone_WithTheImageContentType()
    {
        var organiser = await Api.LoginAs(_factory, "Organiser");
        var eventId = await Api.CreateEvent(organiser);
        await organiser.PostAsync($"/api/events/{eventId}/banner", FileContent(PngBytes, "image/png"));

        var response = await _factory.CreateClient().GetAsync($"/api/events/{eventId}/banner");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/png", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(PngBytes, await response.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task Banner_WhenTheEventHasNone_Returns404()
    {
        var organiser = await Api.LoginAs(_factory, "Organiser");
        var eventId = await Api.CreateEvent(organiser);

        var response = await _factory.CreateClient().GetAsync($"/api/events/{eventId}/banner");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Participant_CannotUploadABanner_Returns403()
    {
        var organiser = await Api.LoginAs(_factory, "Organiser");
        var participant = await Api.LoginAs(_factory, "Participant");
        var eventId = await Api.CreateEvent(organiser);

        var response = await participant.PostAsync($"/api/events/{eventId}/banner", FileContent(PngBytes, "image/png"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_CannotUploadABannerForAnotherOrganisersEvent_Returns403()
    {
        var owner = await Api.LoginAs(_factory, "Organiser");
        var other = await Api.LoginAs(_factory, "Organiser");
        var eventId = await Api.CreateEvent(owner);

        var response = await other.PostAsync($"/api/events/{eventId}/banner", FileContent(PngBytes, "image/png"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UnauthenticatedUser_CannotUploadABanner_Returns401()
    {
        var response = await _factory.CreateClient().PostAsync("/api/events/1/banner", FileContent(PngBytes, "image/png"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UploadingANonImageContentType_Returns400()
    {
        var organiser = await Api.LoginAs(_factory, "Organiser");
        var eventId = await Api.CreateEvent(organiser);

        var response = await organiser.PostAsync($"/api/events/{eventId}/banner",
            FileContent(Encoding.UTF8.GetBytes("just some text"), "text/plain", "notes.txt"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UploadingATextFileDisguisedAsAPng_Returns400()
    {
        var organiser = await Api.LoginAs(_factory, "Organiser");
        var eventId = await Api.CreateEvent(organiser);

        var response = await organiser.PostAsync($"/api/events/{eventId}/banner",
            FileContent(Encoding.UTF8.GetBytes("this is definitely not an image"), "image/png"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UploadingAnImageForAnUnknownEvent_Returns404()
    {
        var organiser = await Api.LoginAs(_factory, "Organiser");

        var response = await organiser.PostAsync("/api/events/999999/banner", FileContent(PngBytes, "image/png"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task User_CanUploadAProfilePicture_AndDownloadIt()
    {
        var participant = await Api.LoginAs(_factory, "Participant");

        var upload = await participant.PostAsync("/api/users/me/picture", FileContent(PngBytes, "image/png"));
        var me = await participant.GetFromJsonAsync<JsonElement>("/api/users/me");
        var download = await participant.GetAsync($"/api/users/{me.GetProperty("userId").GetInt32()}/picture");

        Assert.Equal(HttpStatusCode.OK, upload.StatusCode);
        Assert.True(me.GetProperty("hasProfilePicture").GetBoolean());
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal("image/png", download.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task ProfilePicture_WhenNotLoggedIn_Returns401()
    {
        var response = await _factory.CreateClient().GetAsync("/api/users/1/picture");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ProfilePicture_WhenTheUserHasNone_Returns404()
    {
        var participant = await Api.LoginAs(_factory, "Participant");
        var me = await participant.GetFromJsonAsync<JsonElement>("/api/users/me");

        var response = await participant.GetAsync($"/api/users/{me.GetProperty("userId").GetInt32()}/picture");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
