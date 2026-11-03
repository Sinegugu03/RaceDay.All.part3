using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using RaceDay.Api.Models;
using RaceDay.Api.Services;

namespace RaceDay.Api.Filters;

/// <summary>
/// Session-based authorisation. With no roles it only requires a logged-in user;
/// with roles, the session role must match one of them.
/// 401 = not logged in, 403 = logged in with the wrong role.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class SessionAuthorizeAttribute : Attribute, IAuthorizationFilter
{
    private readonly UserRole[] _roles;

    public SessionAuthorizeAttribute(params UserRole[] roles) => _roles = roles;

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var session = context.HttpContext.Session;
        var userId = session.GetUserId();
        var role = session.GetRole();

        if (userId is null || role is null)
        {
            context.Result = new UnauthorizedObjectResult(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Unauthorized",
                Detail = "You must log in before using this endpoint."
            });
            return;
        }

        if (_roles.Length > 0 && !_roles.Contains(role.Value))
        {
            context.Result = new ObjectResult(new ProblemDetails
            {
                Status = StatusCodes.Status403Forbidden,
                Title = "Forbidden",
                Detail = $"This endpoint is only available to: {string.Join(", ", _roles)}."
            })
            { StatusCode = StatusCodes.Status403Forbidden };
        }
    }
}
