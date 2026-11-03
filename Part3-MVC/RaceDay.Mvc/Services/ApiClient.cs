using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using RaceDay.Mvc.Models;

namespace RaceDay.Mvc.Services;

public class ApiResult<T>
{
    public bool Success { get; init; }
    public T? Data { get; init; }
    public int StatusCode { get; init; }
    /// <summary>A friendly message that is safe to show to the user.</summary>
    public string? Error { get; init; }
    public Dictionary<string, string[]> FieldErrors { get; init; } = new();
    public bool Unauthorized => StatusCode == 401;
}

public record FileData(byte[] Content, string ContentType);

public interface IApiClient
{
    Task<ApiResult<T>> GetAsync<T>(string url);
    Task<ApiResult<T>> PostAsync<T>(string url, object? body = null);
    Task<ApiResult<T>> PutAsync<T>(string url, object? body = null);
    Task<ApiResult<bool>> DeleteAsync(string url);
    Task<ApiResult<T>> UploadAsync<T>(string url, IFormFile file);
    Task<ApiResult<FileData>> GetFileAsync(string url);
    Task<ApiResult<LoginResponse>> LoginAsync(string email, string password);
    Task<ApiResult<bool>> LogoutAsync();
}

/// <summary>
/// Talks to the Part 2 API on behalf of the logged-in user. The API's session cookie is stored in the
/// MVC session and attached to every request, so the API always knows who is calling and in which role.
/// </summary>
public class ApiClient : IApiClient
{
    private const string ApiCookieKey = "ApiSessionCookie";
    private const string ApiSessionName = ".RaceDay.Session";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly HttpClient _http;
    private readonly IHttpContextAccessor _accessor;
    private readonly ILogger<ApiClient> _logger;

    public ApiClient(HttpClient http, IHttpContextAccessor accessor, ILogger<ApiClient> logger)
    {
        _http = http;
        _accessor = accessor;
        _logger = logger;
    }

    private ISession? Session => _accessor.HttpContext?.Session;

    private HttpRequestMessage Build(HttpMethod method, string url, HttpContent? content = null)
    {
        var request = new HttpRequestMessage(method, url) { Content = content };
        var cookie = Session?.GetString(ApiCookieKey);
        if (!string.IsNullOrEmpty(cookie)) request.Headers.TryAddWithoutValidation("Cookie", cookie);
        return request;
    }

    private static HttpContent? JsonBody(object? body) =>
        body is null ? null : JsonContent.Create(body, body.GetType(), null, JsonOptions);

    public async Task<ApiResult<T>> GetAsync<T>(string url)
    {
        using var request = Build(HttpMethod.Get, url);
        return await SendAsync<T>(request);
    }

    public async Task<ApiResult<T>> PostAsync<T>(string url, object? body = null)
    {
        using var request = Build(HttpMethod.Post, url, JsonBody(body));
        return await SendAsync<T>(request);
    }

    public async Task<ApiResult<T>> PutAsync<T>(string url, object? body = null)
    {
        using var request = Build(HttpMethod.Put, url, JsonBody(body));
        return await SendAsync<T>(request);
    }

    public async Task<ApiResult<bool>> DeleteAsync(string url)
    {
        using var request = Build(HttpMethod.Delete, url);
        return await SendAsync<bool>(request);
    }

    public async Task<ApiResult<T>> UploadAsync<T>(string url, IFormFile file)
    {
        using var content = new MultipartFormDataContent();
        await using var stream = file.OpenReadStream();
        var fileContent = new StreamContent(stream);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType);
        content.Add(fileContent, "file", file.FileName);

        using var request = Build(HttpMethod.Post, url, content);
        return await SendAsync<T>(request);
    }

    public async Task<ApiResult<FileData>> GetFileAsync(string url)
    {
        try
        {
            using var request = Build(HttpMethod.Get, url);
            using var response = await _http.SendAsync(request);
            if (!response.IsSuccessStatusCode) return await FailureAsync<FileData>(response);

            var bytes = await response.Content.ReadAsByteArrayAsync();
            var contentType = response.Content.Headers.ContentType?.ToString() ?? "application/octet-stream";
            return new ApiResult<FileData>
            {
                Success = true,
                StatusCode = (int)response.StatusCode,
                Data = new FileData(bytes, contentType)
            };
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "The API could not be reached while fetching a file.");
            return Unreachable<FileData>();
        }
    }

    public async Task<ApiResult<LoginResponse>> LoginAsync(string email, string password)
    {
        try
        {
            var payload = new { email, password };
            using var request = new HttpRequestMessage(HttpMethod.Post, "api/auth/login")
            {
                Content = JsonContent.Create(payload, payload.GetType(), null, JsonOptions)
            };
            using var response = await _http.SendAsync(request);
            if (!response.IsSuccessStatusCode) return await FailureAsync<LoginResponse>(response);

            // Keep the API's session cookie so later calls are made as this user.
            if (response.Headers.TryGetValues("Set-Cookie", out var cookies))
            {
                var sessionCookie = cookies.FirstOrDefault(c => c.StartsWith(ApiSessionName + "=", StringComparison.Ordinal));
                if (sessionCookie is not null) Session?.SetString(ApiCookieKey, sessionCookie.Split(';')[0]);
            }

            var data = await response.Content.ReadFromJsonAsync<LoginResponse>(JsonOptions);
            return new ApiResult<LoginResponse> { Success = true, Data = data, StatusCode = (int)response.StatusCode };
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            _logger.LogWarning(ex, "Login request to the API failed.");
            return Unreachable<LoginResponse>();
        }
    }

    public async Task<ApiResult<bool>> LogoutAsync()
    {
        using var request = Build(HttpMethod.Post, "api/auth/logout");
        var result = await SendAsync<bool>(request);
        Session?.Remove(ApiCookieKey);
        return result;
    }

    private async Task<ApiResult<T>> SendAsync<T>(HttpRequestMessage request)
    {
        try
        {
            using var response = await _http.SendAsync(request);
            if (!response.IsSuccessStatusCode) return await FailureAsync<T>(response);

            T? data = default;
            if (response.StatusCode != HttpStatusCode.NoContent && typeof(T) != typeof(bool))
                data = await response.Content.ReadFromJsonAsync<T>(JsonOptions);

            return new ApiResult<T> { Success = true, Data = data, StatusCode = (int)response.StatusCode };
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            _logger.LogWarning(ex, "A request to the API failed: {Method} {Url}", request.Method, request.RequestUri);
            return Unreachable<T>();
        }
    }

    private static ApiResult<T> Unreachable<T>() => new()
    {
        Success = false,
        StatusCode = 0,
        Error = "We can't reach the RaceDay service right now. Please try again in a moment."
    };

    /// <summary>Turns an API error into a friendly message - raw errors are never shown to the user.</summary>
    private static async Task<ApiResult<T>> FailureAsync<T>(HttpResponseMessage response)
    {
        var status = (int)response.StatusCode;
        var message = status switch
        {
            401 => "Your session has expired or you are not logged in. Please log in again.",
            403 => "You do not have permission to do that.",
            404 => "We could not find what you were looking for.",
            409 => "That action conflicts with the current data.",
            503 => "This feature is temporarily unavailable. Please try again later.",
            _ => "Something went wrong. Please try again."
        };
        var fieldErrors = new Dictionary<string, string[]>();

        try
        {
            var body = await response.Content.ReadAsStringAsync();
            if (!string.IsNullOrWhiteSpace(body) && body.TrimStart().StartsWith('{'))
            {
                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;

                string? detailText = null;
                if (root.TryGetProperty("detail", out var detail) && detail.ValueKind == JsonValueKind.String)
                    detailText = detail.GetString();
                if (!string.IsNullOrWhiteSpace(detailText)) message = detailText;

                if (root.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Object)
                {
                    foreach (var property in errors.EnumerateObject())
                    {
                        fieldErrors[property.Name] = property.Value.ValueKind == JsonValueKind.Array
                            ? property.Value.EnumerateArray().Select(x => x.GetString() ?? string.Empty).ToArray()
                            : Array.Empty<string>();
                    }
                    if (string.IsNullOrWhiteSpace(detailText)) message = "Please check the details you entered and try again.";
                }
            }
        }
        catch (Exception)
        {
            // keep the default friendly message
        }

        return new ApiResult<T> { Success = false, StatusCode = status, Error = message, FieldErrors = fieldErrors };
    }
}
