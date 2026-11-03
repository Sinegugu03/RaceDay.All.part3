using System.Net.Http.Json;
using System.Text.Json;

namespace RaceDay.Tests;

/// <summary>Small helpers so each test reads like the scenario it checks.</summary>
public static class Api
{
    public const string Password = "Password123!";

    public static string NewEmail() => $"{Guid.NewGuid():N}@test.com";

    public static string FutureDate(int days = 30) =>
        DateOnly.FromDateTime(DateTime.UtcNow.AddDays(days)).ToString("yyyy-MM-dd");

    /// <summary>Registers a new user and logs in. Each client has its own cookie jar, i.e. its own session.</summary>
    public static async Task<HttpClient> LoginAs(RaceDayFactory factory, string role, string? email = null)
    {
        var client = factory.CreateClient();
        email ??= NewEmail();

        var register = await client.PostAsJsonAsync("/api/auth/register",
            new { fullName = $"Test {role}", email, password = Password, role, phoneNumber = "0821234567" });
        register.EnsureSuccessStatusCode();

        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password = Password });
        login.EnsureSuccessStatusCode();
        return client;
    }

    public static object EventPayload(string name = "Durban 10km Run") => new
    {
        name,
        description = "A fun community race.",
        eventDate = FutureDate(),
        location = "Durban",
        distanceKm = 10,
        eventType = "Run"
    };

    public static object CategoryPayload(int maxParticipants = 100) => new
    {
        name = "Senior",
        distanceKm = 10,
        maxParticipants,
        fee = 150,
        startTime = "06:30:00"
    };

    public static async Task<int> ReadId(HttpResponseMessage response, string property)
    {
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        return json.GetProperty(property).GetInt32();
    }

    public static async Task<int> CreateEvent(HttpClient organiser, string name = "Durban 10km Run") =>
        await ReadId(await organiser.PostAsJsonAsync("/api/events", EventPayload(name)), "eventId");

    public static async Task<int> CreateCategory(HttpClient organiser, int eventId, int maxParticipants = 100) =>
        await ReadId(await organiser.PostAsJsonAsync($"/api/events/{eventId}/categories", CategoryPayload(maxParticipants)), "categoryId");

    public static async Task<int> Enrol(HttpClient participant, int categoryId) =>
        await ReadId(await participant.PostAsync($"/api/categories/{categoryId}/enrolments", null), "enrolmentId");
}
