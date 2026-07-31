namespace RemoteControlLAN.Gateway.Options;
public sealed class JwtOptions { public string Issuer { get; set; } = "RemoteControlLAN"; public string Audience { get; set; } = "RemoteControlLAN.WebClient"; public string Key { get; set; } = string.Empty; public int AccessTokenMinutes { get; set; } = 120; }
