using RaceDay.Api.Models;

namespace RaceDay.Api.Services;

public static class SessionKeys
{
    public const string UserId = "UserId";
    public const string Role = "Role";
}

/// <summary>Helpers that store and read the logged-in user's id and role in the server-side session.</summary>
public static class SessionAuthExtensions
{
    public static void SignIn(this ISession session, AppUser user)
    {
        session.Clear();
        session.SetInt32(SessionKeys.UserId, user.UserId);
        session.SetString(SessionKeys.Role, user.Role.ToString());
    }

    public static int? GetUserId(this ISession session) => session.GetInt32(SessionKeys.UserId);

    public static UserRole? GetRole(this ISession session)
    {
        var text = session.GetString(SessionKeys.Role);
        return Enum.TryParse<UserRole>(text, out var role) ? role : null;
    }
}
