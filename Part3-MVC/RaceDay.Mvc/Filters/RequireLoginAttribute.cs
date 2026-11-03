using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using RaceDay.Mvc.Services;

namespace RaceDay.Mvc.Filters;

/// <summary>
/// Protects pages. Not logged in: redirect to the login page. Logged in with the wrong role: friendly
/// "access denied" page. Example: [RequireLogin("Organiser")].
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class RequireLoginAttribute : Attribute, IAuthorizationFilter
{
    private readonly string[] _roles;

    public RequireLoginAttribute(params string[] roles) => _roles = roles;

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var session = context.HttpContext.Session;

        if (!session.IsLoggedIn())
        {
            var request = context.HttpContext.Request;
            var returnUrl = request.Method == "GET" ? request.Path + request.QueryString : null;
            context.Result = new RedirectToActionResult("Login", "Account", new { returnUrl });
            return;
        }

        var role = session.GetRole();
        if (_roles.Length > 0 && (role is null || !_roles.Contains(role)))
            context.Result = new RedirectToActionResult("AccessDenied", "Account", null);
    }
}
