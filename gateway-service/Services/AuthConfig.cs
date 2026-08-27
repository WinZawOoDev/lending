namespace gateway_service.Services;

public sealed class JwtOptions
{
    public string Key { get; set; } = "";
    public string Issuer { get; set; } = "";
    public string Audience { get; set; } = "";
}

public sealed class DevClientOptions
{
    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";
}

public sealed class AuthConfig
{
    public JwtOptions Jwt { get; set; } = new();
    public DevClientOptions DevClient { get; set; } = new();
}