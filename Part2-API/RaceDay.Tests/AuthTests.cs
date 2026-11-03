using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RaceDay.Api.Data;

namespace RaceDay.Tests;

/// <summary>User registration, login, logout, profile and password hashing.</summary>
public class AuthTests : IClassFixture<RaceDayFactory>
{
    private readonly RaceDayFactory _factory;

    public AuthTests(RaceDayFactory factory) => _factory = factory;

    private static object RegisterBody(string email, string role = "Participant", string password = Api.Password) =>
        new { fullName = "Sipho Dlamini", email, password, role, phoneNumber = "0821234567" };

    [Fact]
    public async Task Register_WithValidDetails_Returns201_AndNeverReturnsPassword()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/register", RegisterBody(Api.NewEmail()));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("password", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Register_AsOrganiser_StoresTheOrganiserRole()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/register", RegisterBody(Api.NewEmail(), "Organiser"));

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Organiser", json.GetProperty("role").GetString());
    }

    [Fact]
    public async Task Register_WithDuplicateEmail_Returns409()
    {
        var client = _factory.CreateClient();
        var email = Api.NewEmail();
        await client.PostAsJsonAsync("/api/auth/register", RegisterBody(email));

        var second = await client.PostAsJsonAsync("/api/auth/register", RegisterBody(email));

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Register_AsAdmin_Returns400()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/register", RegisterBody(Api.NewEmail(), "Admin"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_WithInvalidEmailAndShortPassword_Returns400()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/register", RegisterBody("not-an-email", password: "short"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_WithoutARole_Returns400()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/register",
            new { fullName = "No Role", email = Api.NewEmail(), password = Api.Password });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_StoresAHashedPassword_NotThePlainText()
    {
        var client = _factory.CreateClient();
        var email = Api.NewEmail();
        await client.PostAsJsonAsync("/api/auth/register", RegisterBody(email));

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RaceDayDbContext>();
        var user = await db.Users.SingleAsync(u => u.Email == email);

        Assert.NotEqual(Api.Password, user.PasswordHash);
        Assert.DoesNotContain(Api.Password, user.PasswordHash);
    }

    [Fact]
    public async Task Login_WithValidCredentials_Returns200_WithTheUsersRole()
    {
        var client = _factory.CreateClient();
        var email = Api.NewEmail();
        await client.PostAsJsonAsync("/api/auth/register", RegisterBody(email, "Organiser"));

        var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password = Api.Password });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Organiser", json.GetProperty("role").GetString());
    }

    [Fact]
    public async Task Login_WithWrongPassword_Returns401()
    {
        var client = _factory.CreateClient();
        var email = Api.NewEmail();
        await client.PostAsJsonAsync("/api/auth/register", RegisterBody(email));

        var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "WrongPassword1!" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_WithUnknownEmail_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new { email = Api.NewEmail(), password = Api.Password });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetMyProfile_WhenNotLoggedIn_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/users/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetMyProfile_WhenLoggedIn_ReturnsTheirOwnProfile()
    {
        var email = Api.NewEmail();
        var client = await Api.LoginAs(_factory, "Participant", email);

        var response = await client.GetAsync("/api/users/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(email, json.GetProperty("email").GetString());
    }

    [Fact]
    public async Task UpdateMyProfile_ChangesTheName()
    {
        var client = await Api.LoginAs(_factory, "Participant");

        var response = await client.PutAsJsonAsync("/api/users/me", new { fullName = "Updated Name", phoneNumber = "0710000000" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Updated Name", json.GetProperty("fullName").GetString());
    }

    [Fact]
    public async Task Logout_EndsTheSession()
    {
        var client = await Api.LoginAs(_factory, "Participant");

        var logout = await client.PostAsync("/api/auth/logout", null);
        var afterLogout = await client.GetAsync("/api/users/me");

        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, afterLogout.StatusCode);
    }

    [Fact]
    public async Task GetUserById_AsParticipant_Returns403_ButAsOrganiserReturns200()
    {
        var participantEmail = Api.NewEmail();
        var participant = await Api.LoginAs(_factory, "Participant", participantEmail);
        var organiser = await Api.LoginAs(_factory, "Organiser");
        var me = await participant.GetFromJsonAsync<JsonElement>("/api/users/me");
        var participantId = me.GetProperty("userId").GetInt32();

        var asParticipant = await participant.GetAsync($"/api/users/{participantId}");
        var asOrganiser = await organiser.GetAsync($"/api/users/{participantId}");

        Assert.Equal(HttpStatusCode.Forbidden, asParticipant.StatusCode);
        Assert.Equal(HttpStatusCode.OK, asOrganiser.StatusCode);
    }
}
