using Microsoft.AspNetCore.Mvc;
using RaceDay.Mvc.Filters;
using RaceDay.Mvc.Models;
using RaceDay.Mvc.Services;

namespace RaceDay.Mvc.Controllers;

[RequireLogin]
public class ProfileController : AppController
{
    public ProfileController(IApiClient api) : base(api) { }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var result = await Api.GetAsync<UserDto>("api/users/me");
        if (result.Unauthorized) return SessionExpired();
        if (!result.Success || result.Data is null)
        {
            Fail(result.Error ?? "We could not load your profile.");
            return RedirectToHome();
        }
        return View(ToViewModel(result.Data));
    }

    [HttpPost]
    public async Task<IActionResult> Index(ProfileViewModel model)
    {
        var imageError = ValidateImage(model.Picture);
        if (imageError is not null) ModelState.AddModelError(nameof(model.Picture), imageError);
        if (!ModelState.IsValid) return View(model);

        var update = await Api.PutAsync<UserDto>("api/users/me", new
        {
            fullName = model.FullName,
            phoneNumber = string.IsNullOrWhiteSpace(model.PhoneNumber) ? null : model.PhoneNumber
        });
        if (update.Unauthorized) return SessionExpired();
        if (!update.Success)
        {
            AddApiErrors(update);
            return View(model);
        }
        HttpContext.Session.SetFullName(model.FullName);

        if (model.Picture is { Length: > 0 })
        {
            var upload = await Api.UploadAsync<UserDto>("api/users/me/picture", model.Picture);
            if (upload.Unauthorized) return SessionExpired();
            if (!upload.Success)
            {
                Fail($"Your details were saved, but the picture could not be uploaded: {upload.Error}");
                return RedirectToAction(nameof(Index));
            }
        }

        Success("Your profile has been updated.");
        return RedirectToAction(nameof(Index));
    }

    private static ProfileViewModel ToViewModel(UserDto user) => new()
    {
        UserId = user.UserId,
        FullName = user.FullName,
        PhoneNumber = user.PhoneNumber,
        Email = user.Email,
        Role = user.Role,
        HasProfilePicture = user.HasProfilePicture
    };
}
