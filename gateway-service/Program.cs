using System.Text;
using gateway_service.Middleware;
using gateway_service.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHealthChecks();

var authConfig = builder.Configuration.GetSection("Auth").Get<AuthConfig>()
    ?? throw new InvalidOperationException("Auth configuration is missing.");

const string PlaceholderJwtKey = "replace-me-with-a-secret-at-least-32-characters-long";
var jwtKey = authConfig.Jwt.Key;
if (string.IsNullOrWhiteSpace(jwtKey))
{
    throw new InvalidOperationException("Auth:Jwt:Key is missing.");
}
if (jwtKey.Length < 32)
{
    throw new InvalidOperationException("Auth:Jwt:Key must be at least 32 characters long.");
}
if (!builder.Environment.IsDevelopment() && jwtKey == PlaceholderJwtKey)
{
    throw new InvalidOperationException(
        "Auth:Jwt:Key is still the placeholder value. Replace it with a strong secret outside Development.");
}
if (string.IsNullOrWhiteSpace(authConfig.Jwt.Issuer))
{
    throw new InvalidOperationException("Auth:Jwt:Issuer is missing.");
}
if (string.IsNullOrWhiteSpace(authConfig.Jwt.Audience))
{
    throw new InvalidOperationException("Auth:Jwt:Audience is missing.");
}

builder.Services.AddSingleton(authConfig.Jwt);

builder.Services.Configure<AuthConfig>(builder.Configuration.GetSection("Auth"));

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = authConfig.Jwt.Issuer,
            ValidAudience = authConfig.Jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(authConfig.Jwt.Key)),
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("authenticated", policy => policy.RequireAuthenticatedUser());
});

builder.Services.AddSingleton<ITokenIssuer, TokenIssuer>();

builder.Services.AddControllers();

builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();

app.UseCorrelationId();

app.UseAuthentication();

app.UseAuthorization();

app.MapHealthChecks("/health");

app.MapReverseProxy();

if (app.Environment.IsDevelopment())
{
    app.MapControllers();
}

app.Run();