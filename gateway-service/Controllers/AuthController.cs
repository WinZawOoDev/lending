using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using gateway_service.Services;

namespace gateway_service.Controllers;

[ApiController]
[Route("auth")]
public class AuthController(ITokenIssuer tokenIssuer, IOptions<AuthConfig> auth) : ControllerBase
{
    [HttpPost("token")]
    public ActionResult<TokenResponse> Token([FromBody] TokenRequest request)
    {
        var cfg = auth.Value;

        if (request.ClientId != cfg.DevClient.ClientId || request.ClientSecret != cfg.DevClient.ClientSecret)
        {
            return Unauthorized(new { message = "Invalid client credentials." });
        }

        const int lifetimeSeconds = 3600;
        var token = tokenIssuer.Issue(
            request.ClientId,
            new[] { "lending.api" },
            TimeSpan.FromSeconds(lifetimeSeconds));

        return Ok(new TokenResponse(token, "Bearer", lifetimeSeconds));
    }
}

public sealed record TokenRequest(string ClientId, string ClientSecret);

public sealed record TokenResponse(string AccessToken, string TokenType, int ExpiresIn);