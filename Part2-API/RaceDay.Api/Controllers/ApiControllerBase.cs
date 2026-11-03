using Microsoft.AspNetCore.Mvc;
using RaceDay.Api.Models;
using RaceDay.Api.Services;

namespace RaceDay.Api.Controllers;

/// <summary>Shared helpers: current session user and consistent error responses.</summary>
public abstract class ApiControllerBase : ControllerBase
{
    protected int CurrentUserId =>
        HttpContext.Session.GetUserId() ?? throw new InvalidOperationException("No logged-in user in the session.");

    protected UserRole CurrentRole =>
        HttpContext.Session.GetRole() ?? throw new InvalidOperationException("No role in the session.");

    protected ObjectResult ForbiddenProblem(string detail) =>
        StatusCode(StatusCodes.Status403Forbidden,
            new ProblemDetails { Status = StatusCodes.Status403Forbidden, Title = "Forbidden", Detail = detail });

    protected NotFoundObjectResult NotFoundProblem(string detail) =>
        NotFound(new ProblemDetails { Status = StatusCodes.Status404NotFound, Title = "Not Found", Detail = detail });

    protected ConflictObjectResult ConflictProblem(string detail) =>
        Conflict(new ProblemDetails { Status = StatusCodes.Status409Conflict, Title = "Conflict", Detail = detail });

    protected BadRequestObjectResult BadRequestProblem(string detail) =>
        BadRequest(new ProblemDetails { Status = StatusCodes.Status400BadRequest, Title = "Bad Request", Detail = detail });
}
