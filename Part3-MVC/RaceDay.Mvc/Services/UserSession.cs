using RaceDay.Mvc.Models;

namespace RaceDay.Mvc.Services;

/// <summary>Keeps the logged-in user's id, name and role in the MVC session so every page can see them.</summary>
public static class UserSession
{
    private const string UserIdKey = "UserId";
    private const string FullNameKey = "FullName";
    private const string RoleKey = "Role";

    public static void SignIn(this ISession session, LoginResponse user)
    {
        session.SetInt32(UserIdKey, user.UserId);
        session.SetString(FullNameKey, user.FullName);
        session.SetString(RoleKey, user.Role);
    }

    public static bool IsLoggedIn(this ISession session) => session.GetInt32(UserIdKey) is not null;

    public static int? GetUserId(this ISession session) => session.GetInt32(UserIdKey);

    public static string? GetRole(this ISession session) => session.GetString(RoleKey);

    public static string? GetFullName(this ISession session) => session.GetString(FullNameKey);

    public static void SetFullName(this ISession session, string fullName) => session.SetString(FullNameKey, fullName);
}
