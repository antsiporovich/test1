using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using PropertyManagement.Infrastructure.Identity;
using PropertyManagement.Web.Models;

namespace PropertyManagement.Web.Controllers;

public class AccountController(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager) : Controller
{
    [HttpGet]
    [AllowAnonymous]
    public IActionResult Register(string? returnUrl = null)
    {
        if (signInManager.IsSignedIn(User))
        {
            return RedirectToRoleHome();
        }

        ViewData["ReturnUrl"] = returnUrl;
        return View(new RegisterViewModel());
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model, string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = new ApplicationUser
        {
            UserName = model.Email,
            Email = model.Email,
            DisplayName = model.Email,
        };

        var result = await userManager.CreateAsync(user, model.Password);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return View(model);
        }

        // Same flow as user creation, not a separate step, so a user is never left roleless.
        await userManager.AddToRoleAsync(user, model.Role!.Value.ToString());
        await signInManager.SignInAsync(user, isPersistent: false);

        return RedirectToLocalOrRoleHome(returnUrl);
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult Login(string? returnUrl = null)
    {
        if (signInManager.IsSignedIn(User))
        {
            return RedirectToRoleHome();
        }

        ViewData["ReturnUrl"] = returnUrl;
        return View(new LoginViewModel());
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await signInManager.PasswordSignInAsync(model.Email, model.Password, model.RememberMe, lockoutOnFailure: true);
        if (result.Succeeded)
        {
            return RedirectToLocalOrRoleHome(returnUrl);
        }

        if (result.IsLockedOut)
        {
            ModelState.AddModelError(string.Empty, "This account has been locked out. Please try again later.");
            return View(model);
        }

        // Deliberately generic: never reveals whether the email exists.
        ModelState.AddModelError(string.Empty, "Invalid login attempt.");
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await signInManager.SignOutAsync();
        return RedirectToAction(nameof(HomeController.Index), "Home");
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult AccessDenied()
    {
        return View();
    }

    private IActionResult RedirectToLocalOrRoleHome(string? returnUrl)
    {
        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return RedirectToRoleHome();
    }

    private IActionResult RedirectToRoleHome()
    {
        // Unit browsing (Applicant) lands in Epic 3; Applicants still go to Home until then.
        // HttpContext.User reflects the just-established principal synchronously after
        // SignInAsync/PasswordSignInAsync within the same request, so this is safe here.
        if (User.IsInRole("PropertyManager"))
        {
            return RedirectToAction(nameof(PropertiesController.Index), "Properties");
        }

        return RedirectToAction(nameof(HomeController.Index), "Home");
    }
}
