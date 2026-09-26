namespace Api.Services.Barber.Common;

public class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "Api.Services.Barber";
    public string Audience { get; set; } = "TurnosBarber";
    public string Key { get; set; } = default!;
    public int ExpiresMinutes { get; set; } = 480;
}
