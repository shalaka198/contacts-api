using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Contacts.Application.Interfaces;

namespace Contacts.Api.Controllers;

/// <summary>
/// Issues JWT tokens. In a real system this delegates to an IdP (e.g. Auth0, Cognito).
/// This controller exists only to make the demo self-contained and runnable without an IdP.
/// </summary>
[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/auth")]
[AllowAnonymous]
[Produces("application/json")]
public sealed class AuthController : ControllerBase
{
    private readonly IJwtTokenService _jwtService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        IJwtTokenService jwtService,
        IConfiguration configuration,
        ILogger<AuthController> logger)
    {
        _jwtService = jwtService;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Returns a short-lived JWT for the given credentials.
    /// Credentials are validated against configured demo users only.
    /// Production systems should delegate to an external Identity Provider.
    /// </summary>
    [HttpPost("token")]
    [ProducesResponseType(typeof(TokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public IActionResult Token([FromBody] TokenRequest request)
    {
        // Demo credential check — replace with your IdP integration
        var users = _configuration.GetSection("DemoUsers").Get<List<DemoUser>>() ?? [];

        var match = users.FirstOrDefault(u =>
            u.Email.Equals(request.Email, StringComparison.OrdinalIgnoreCase) &&
            u.Password == request.Password);

        if (match is null)
        {
            _logger.LogWarning("Failed login attempt for email {Email}", request.Email);
            return Problem(
                type: "https://httpstatuses.com/401",
                title: "Unauthorised",
                detail: "Invalid email or password.",
                statusCode: StatusCodes.Status401Unauthorized);
        }

        var token = _jwtService.GenerateToken(match.Id, match.Email, match.Roles);

        _logger.LogInformation("Token issued for user {UserId}", match.Id);

        return Ok(new TokenResponse(token, "Bearer", 3600));
    }
}

public sealed record TokenRequest(string Email, string Password);
public sealed record TokenResponse(string AccessToken, string TokenType, int ExpiresIn);

// Used only for the demo configuration section
internal sealed record DemoUser(string Id, string Email, string Password, string[] Roles);
