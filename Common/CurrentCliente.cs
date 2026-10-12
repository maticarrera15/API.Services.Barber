using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Api.Services.Barber.Common;

public record ClientePrincipal(Guid CuentaId, Guid ClienteId, string Email);

public static class CurrentCliente
{
    public const string Rol = "cliente";
    public const string ClienteIdClaim = "cliente_id";

    public static ClientePrincipal? Get(ClaimsPrincipal user)
    {
        if (user.Identity?.IsAuthenticated != true) return null;
        if (user.FindFirstValue(CurrentUser.RolClaim) != Rol) return null;

        var cuentaId = user.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? user.FindFirstValue(ClaimTypes.NameIdentifier);
        var clienteId = user.FindFirstValue(ClienteIdClaim);
        var email = user.FindFirstValue(JwtRegisteredClaimNames.Email) ?? user.FindFirstValue(ClaimTypes.Email) ?? "";

        return Guid.TryParse(cuentaId, out var cid) && Guid.TryParse(clienteId, out var clid)
            ? new ClientePrincipal(cid, clid, email)
            : null;
    }

    public static IResult? Require(ClaimsPrincipal user, out ClientePrincipal principal)
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
