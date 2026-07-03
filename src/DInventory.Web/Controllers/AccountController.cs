using System.Security.Claims;
using DInventory.Application.Audit;
using DInventory.Application.Auth;
using DInventory.Application.Common.Interfaces;
using DInventory.Application.Common.Models;
using DInventory.Web.Common;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DInventory.Web.Controllers;

public class AccountController : Controller
{
    private readonly IAuthService _authService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAuditLogService _auditLogService;

    public AccountController(IAuthService authService, ICurrentUserService currentUserService, IAuditLogService auditLogService)
    {
        _authService = authService;
        _currentUserService = currentUserService;
        _auditLogService = auditLogService;
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToLocal(returnUrl);
        }

        ViewData["ReturnUrl"] = returnUrl;
        return View(new LoginRequest());
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginRequest model, string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        var result = await _authService.LoginAsync(model, ipAddress);

        if (!result.Succeeded || result.User is null || result.Tokens is null)
        {
            await _auditLogService.LogAsync(null, model.Username, "LOGIN_FAILED", "Users", null, ipAddress: ipAddress);
            ModelState.AddModelError(string.Empty, result.Error ?? "Invalid username or password.");
            return View(model);
        }

        await SignInWithClaimsAsync(result.User, result.User.RoleName ?? "Staff", model.RememberMe);
        SetAuthCookies(result.Tokens);

        await _auditLogService.LogAsync(result.User.UserId, result.User.Username, "LOGIN", "Users", result.User.UserId.ToString(), ipAddress: ipAddress);

        return RedirectToLocal(returnUrl);
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        var refreshToken = Request.Cookies[AuthCookies.RefreshToken];
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        var currentUser = _currentUserService.GetCurrentUser();

        if (!string.IsNullOrEmpty(refreshToken))
        {
            await _authService.LogoutAsync(refreshToken, ipAddress);
        }

        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        ClearAuthCookies();

        await _auditLogService.LogAsync(currentUser.UserId, currentUser.Username, "LOGOUT", "Users", currentUser.UserId.ToString(), ipAddress: ipAddress);

        return RedirectToAction(nameof(Login));
    }

    /// <summary>Silently rotates the access/refresh token pair. Called by site.js shortly before the access token expires.</summary>
    [AllowAnonymous]
    [HttpPost("~/api/auth/refresh")]
    public async Task<IActionResult> RefreshToken()
    {
        var refreshToken = Request.Cookies[AuthCookies.RefreshToken];
        if (string.IsNullOrEmpty(refreshToken))
        {
            return Unauthorized(new { error = "No active session." });
        }

        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        var result = await _authService.RefreshTokenAsync(refreshToken, ipAddress);

        if (!result.Succeeded || result.User is null || result.Tokens is null)
        {
            ClearAuthCookies();
            return Unauthorized(new { error = result.Error ?? "Session expired." });
        }

        await SignInWithClaimsAsync(result.User, result.User.RoleName ?? "Staff", true);
        SetAuthCookies(result.Tokens);

        return Ok(new { accessTokenExpiresAt = result.Tokens.AccessTokenExpiresAt });
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult ForgotPassword() => View();

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var resetUrlBase = Url.Action(nameof(ResetPassword), "Account", null, Request.Scheme) ?? "/Account/ResetPassword";
        await _authService.ForgotPasswordAsync(model.Email, resetUrlBase);

        ViewData["Submitted"] = true;
        return View(model);
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult ResetPassword(string token, string email)
    {
        return View(new ResetPasswordRequest { Token = token });
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await _authService.ResetPasswordAsync(model);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Unable to reset password.");
            return View(model);
        }

        TempData["StatusMessage"] = "Your password has been reset. Please log in with your new password.";
        return RedirectToAction(nameof(Login));
    }

    [Authorize]
    [HttpGet]
    public IActionResult ChangePassword() => View();

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest model)
    {
        var currentUser = _currentUserService.GetCurrentUser();
        model.UserId = currentUser.UserId;

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await _authService.ChangePasswordAsync(model);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Unable to change password.");
            return View(model);
        }

        TempData["StatusMessage"] = "Your password has been updated.";
        return RedirectToAction(nameof(ChangePassword));
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult AccessDenied() => View();

    private async Task SignInWithClaimsAsync(DInventory.Domain.Entities.User user, string roleName, bool isPersistent)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.UserId.ToString()),
            new(ClaimTypes.Name, user.Username),
            new("fullName", user.FullName),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Role, roleName),
            new("roleId", user.RoleId.ToString())
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, new AuthenticationProperties
        {
            IsPersistent = isPersistent,
            ExpiresUtc = isPersistent ? DateTimeOffset.UtcNow.AddDays(7) : DateTimeOffset.UtcNow.AddHours(8)
        });
    }

    private void SetAuthCookies(TokenPair tokens)
    {
        Response.Cookies.Append(AuthCookies.AccessToken, tokens.AccessToken, new CookieOptions
        {
            HttpOnly = false,
            Secure = Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            Expires = tokens.AccessTokenExpiresAt
        });

        Response.Cookies.Append(AuthCookies.RefreshToken, tokens.RefreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = Request.IsHttps,
            SameSite = SameSiteMode.Strict,
            Expires = tokens.RefreshTokenExpiresAt
        });
    }

    private void ClearAuthCookies()
    {
        Response.Cookies.Delete(AuthCookies.AccessToken);
        Response.Cookies.Delete(AuthCookies.RefreshToken);
    }

    private IActionResult RedirectToLocal(string? returnUrl)
    {
        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return RedirectToAction("Index", "Dashboard");
    }
}
