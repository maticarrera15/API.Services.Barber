using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;

namespace Api.Services.Barber.Common;

public record ProfesionalPrincipal(Guid UsuarioId, Guid ProfesionalId, Guid SalonId, string Email, string Rol);

public static class CurrentUser
{
    public const string ProfesionalIdClaim = "profesional_id";
    public const string SalonIdClaim = "salon_id";
    public const string RolClaim = "rol";

    public static ProfesionalPrincipal? Get(ClaimsPrincipal user)
    {
        if (user.Identity?.IsAuthenticated != true) return null;

        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? user.FindFirstValue(JwtRegisteredClaimNames.Sub);
        var profesionalId = user.FindFirstValue(ProfesionalIdClaim);
        var salonId = user.FindFirstValue(SalonIdClaim);
        var email = user.FindFirstValue(ClaimTypes.Email)
            ?? user.FindFirstValue(JwtRegisteredClaimNames.Email)
            ?? "";
        var rol = user.FindFirstValue(RolClaim) ?? user.FindFirstValue(ClaimTypes.Role) ?? "profesional";

        if (!Guid.TryParse(userId, out var uid)
            || !Guid.TryParse(profesionalId, out var pid)
            || !Guid.TryParse(salonId, out var sid))
            return null;

        return new ProfesionalPrincipal(uid, pid, sid, email, rol);
    }

    public static IResult? Require(ClaimsPrincipal user, out ProfesionalPrincipal principal)
    {
        var parsed = Get(user);
        if (parsed is null)
        {
            principal = null!;
            return Results.Unauthorized();
        }

        principal = parsed;
        return null;
    }
}
