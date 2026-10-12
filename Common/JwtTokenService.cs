using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Api.Services.Barber.Domain;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Api.Services.Barber.Common;

public class JwtTokenService(IOptions<JwtOptions> options)
{
    private readonly JwtOptions _options = options.Value;

    public (string Token, DateTimeOffset ExpiresAt) Create(Usuario usuario, Guid salonId) =>
        Write(
        [
            new(JwtRegisteredClaimNames.Sub, usuario.Id.ToString()),
            new(ClaimTypes.NameIdentifier, usuario.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, usuario.Email),
            new(ClaimTypes.Email, usuario.Email),
            new(CurrentUser.ProfesionalIdClaim, usuario.ProfesionalId.ToString()),
            new(CurrentUser.SalonIdClaim, salonId.ToString()),
            new(CurrentUser.RolClaim, usuario.Rol),
            new(ClaimTypes.Role, usuario.Rol)
        ], _options.ExpiresMinutes);

    public (string Token, DateTimeOffset ExpiresAt) CreateCliente(ClienteCuenta cuenta) =>
        Write(
        [
            new(JwtRegisteredClaimNames.Sub, cuenta.Id.ToString()),
            new(ClaimTypes.NameIdentifier, cuenta.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, cuenta.Email),
            new(ClaimTypes.Email, cuenta.Email),
            new(CurrentCliente.ClienteIdClaim, cuenta.ClienteId.ToString()),
            new(CurrentUser.RolClaim, CurrentCliente.Rol),
            new(ClaimTypes.Role, CurrentCliente.Rol)
        ], _options.ClienteExpiresMinutes);

    private (string Token, DateTimeOffset ExpiresAt) Write(List<Claim> claims, int minutes)
    {
        var expires = DateTimeOffset.UtcNow.AddMinutes(minutes);
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Key));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            expires: expires.UtcDateTime,
            signingCredentials: creds);

        return (new JwtSecurityTokenHandler().WriteToken(token), expires);
    }
}
