using Microsoft.AspNetCore.Mvc;
using RaceDay.Mvc.Models;
using RaceDay.Mvc.Services;

namespace RaceDay.Mvc.Controllers;

public class AccountController : AppController
{
    public AccountController(IApiClient api) : base(api) { }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (HttpContext.Session.IsLoggedIn()) return RedirectToHome();
        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        HttpContext.Session.Clear();   // start from a clean session at every login
        var result = await Api.LoginAsync(model.Email, model.Password);
        if (!result.Success || result.Data is null)
        {
            AddApiErrors(result);
            return View(model);
        }

        HttpContext.Session.SignIn(result.Data);
        Success($"Welcome back, {result.Data.FullName}!");

        if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
            return LocalRedirect(model.ReturnUrl);
        return RedirectToHome();
    }

    [HttpGet]
    public IActionResult Register()
    {
        if (HttpContext.Session.IsLoggedIn()) return RedirectToHome();
        return View(new RegisterViewModel());
    }

    [HttpPost]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var created = await Api.PostAsync<UserDto>("api/auth/register", new
        {
            fullName = model.FullName,
            email = model.Email,
            password = model.Password,
            role = model.Role,
            phoneNumber = string.IsNullOrWhiteSpace(model.PhoneNumber) ? null : model.PhoneNumber
        });
        if (!created.Success)
        {
            AddApiErrors(created);
            return View(model);
        }

        // Log the new user straight in
        HttpContext.Session.Clear();
        var login = await Api.LoginAsync(model.Email, model.Password);
        if (!login.Success || login.Data is null)
        {
            Success("Your account was created. Please log in.");
            return RedirectToAction(nameof(Login));
        }

        HttpContext.Session.SignIn(login.Data);
        Success("Welcome to RaceDay! Your account is ready.");
        return RedirectToHome();
    }

    [HttpPost]
    public async Task<IActionResult> Logout()
    {
        await Api.LogoutAsync();
        HttpContext.Session.Clear();
        Success("You have been logged out.");
        return RedirectToAction(nameof(Login));
    }

    [HttpGet]
    public IActionResult AccessDenied() => View();
}
