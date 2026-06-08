using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using TaxMate.Model.Common;
using TaxMate.Model.DTO.Auth;
using TaxMate.Service.Interfaces;

namespace TaxMate.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private static readonly TimeSpan OAuthStateLifetime = TimeSpan.FromMinutes(10);

    private readonly IAuthService _authService;
    private readonly IUserProfileService _userProfileService;
    private readonly IGoogleOAuthService _googleOAuthService;
    private readonly IMemoryCache _cache;

    public AuthController(
        IAuthService authService,
        IUserProfileService userProfileService,
        IGoogleOAuthService googleOAuthService,
        IMemoryCache cache)
    {
        _authService = authService;
        _userProfileService = userProfileService;
        _googleOAuthService = googleOAuthService;
        _cache = cache;
    }

    [HttpPost("google")]
    [AllowAnonymous]
    public async Task<IActionResult> GoogleLogin(
        [FromBody] GoogleLoginRequest request,
        CancellationToken cancellationToken)
    {
        var response = await _authService.LoginWithGoogleAsync(request.IdToken, cancellationToken);
        return Ok(response);
    }

    [HttpGet("google/login")]
    [AllowAnonymous]
    public IActionResult GoogleLoginRedirect()
    {
        var state = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

        _cache.Set(GetOAuthStateCacheKey(state), true, OAuthStateLifetime);

        var authorizationUrl = _googleOAuthService.BuildAuthorizationUrl(state);
        return Redirect(authorizationUrl);
    }

    [HttpGet("google/callback")]
    [AllowAnonymous]
    public async Task<IActionResult> GoogleCallback(
        [FromQuery] string? code,
        [FromQuery] string? state,
        [FromQuery] string? error,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(error))
        {
            return Content(
                BuildHtmlPage("Google login failed", error),
                "text/html; charset=utf-8");
        }

        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(state))
        {
            return Content(
                BuildHtmlPage("Google login failed", "Missing authentication code or state."),
                "text/html; charset=utf-8");
        }

        if (!_cache.TryGetValue(GetOAuthStateCacheKey(state), out _))
        {
            return Content(
                BuildHtmlPage("Google login failed", "login session expired or invalid."),
                "text/html; charset=utf-8");
        }

        _cache.Remove(GetOAuthStateCacheKey(state));

        try
        {
            var idToken = await _googleOAuthService.ExchangeCodeForIdTokenAsync(code, cancellationToken);

            return Content(
                BuildTokenPage(idToken),
                "text/html; charset=utf-8");
        }
        catch (Exception ex)
        {
            return Content(
                BuildHtmlPage("Google login failed", ex.Message),
                "text/html; charset=utf-8");
        }
    }

    [HttpGet("verify-email")]
    [AllowAnonymous]
    public async Task<IActionResult> VerifyEmail(
        [FromQuery] string token,
        CancellationToken cancellationToken)
    {
        try
        {
            var email = await _authService.ConfirmEmailVerificationAsync(token, cancellationToken);
            return Content(
                BuildHtmlPage(
                    "Email verified successfully",
                    $"Email {email} has been activated. Login again with Google to receive access token."),
                "text/html; charset=utf-8");
        }
        catch (ArgumentException ex)
        {
            return Content(
                BuildHtmlPage("Xác minh email thất bại", ex.Message),
                "text/html; charset=utf-8");
        }
    }

    [HttpPost("verify-email")]
    [AllowAnonymous]
    public async Task<IActionResult> VerifyEmailToken(
        [FromBody] VerifyEmailRequest request,
        CancellationToken cancellationToken)
    {
        var response = await _authService.CompleteEmailVerificationAsync(request.Token, cancellationToken);
        return Ok(response);
    }

    [HttpPost("resend-verification")]
    [Authorize]
    public async Task<IActionResult> ResendVerification(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        await _authService.ResendVerificationEmailAsync(userId, cancellationToken);
        return Ok(new { message = "Email verification has been resend." });
    }

    [HttpGet("me")]
    [Authorize(Policy = AuthPolicies.ActiveAccountOnly)]
    public async Task<IActionResult> Me(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var user = await _authService.GetCurrentUserAsync(userId, cancellationToken);
        return Ok(user);
    }

    [HttpPut("profile")]
    [Authorize(Policy = AuthPolicies.ActiveAccountOnly)]
    public async Task<IActionResult> UpdateProfile(
        [FromBody] UpdateProfileRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var response = await _userProfileService.InitiateProfileUpdateAsync(
            userId,
            request.TaxCode,
            request.Phone,
            cancellationToken);
        return Ok(response);
    }

    [HttpPost("profile/verify-email")]
    [Authorize(Policy = AuthPolicies.ActiveAccountOnly)]
    public async Task<IActionResult> VerifyProfileEmail(
        [FromBody] VerifyProfileEmailRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var response = await _userProfileService.VerifyAndUpdateProfileAsync(
            userId,
            request.Otp,
            cancellationToken);
        return Ok(response);
    }

    [HttpPost("profile/resend-email")]
    [Authorize(Policy = AuthPolicies.ActiveAccountOnly)]
    public async Task<IActionResult> ResendProfileEmail(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var response = await _userProfileService.ResendProfileOtpAsync(userId, cancellationToken);
        return Ok(response);
    }

    private Guid GetUserId()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");

        if (sub is null || !Guid.TryParse(sub, out var userId))
        {
            throw new UnauthorizedAccessException("Token invalid.");
        }

        return userId;
    }

    private static string GetOAuthStateCacheKey(string state) => $"google-oauth-state:{state}";

    private static string BuildHtmlPage(string title, string message) =>
        $"""
        <!DOCTYPE html>
        <html lang="vi">
        <head>
            <meta charset="utf-8" />
            <title>{title}</title>
        </head>
        <body>
            <h1>{title}</h1>
            <p>{message}</p>
        </body>
        </html>
        """;

    private static string BuildTokenPage(string idToken)
    {
        var encodedToken = WebUtility.HtmlEncode(idToken);
        var requestBody = $$"""
            {
              "idToken": "{{encodedToken}}"
            }
            """;
        return $$"""
            <!DOCTYPE html>
            <html lang="en">
            <head>
                <meta charset="utf-8" />
                <title>Google login successful</title>
                <style>
                    body { font-family: Arial, sans-serif; line-height: 1.5; margin: 2rem; }
                    pre {
                        background: #f6f8fa;
                        border: 1px solid #d0d7de;
                        border-radius: 6px;
                        max-width: 100%;
                        overflow-wrap: anywhere;
                        padding: 1rem;
                        white-space: pre-wrap;
                        word-break: break-word;
                    }
                </style>
            </head>
            <body>
                <h1>Google login successful</h1>
                
                <pre>{{requestBody}}</pre>
            </body>
            </html>
            """;
    }
}
