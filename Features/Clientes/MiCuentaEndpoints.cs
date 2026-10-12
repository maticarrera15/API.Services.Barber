using System.Security.Claims;
using Api.Services.Barber.Common;
using Api.Services.Barber.Data;
using Api.Services.Barber.Features.Auth;
using Api.Services.Barber.Features.Turnos;
using Microsoft.EntityFrameworkCore;

namespace Api.Services.Barber.Features.Clientes;

/// <summary>Lo que ve y edita el cliente con su sesión iniciada.</summary>
public static class MiCuentaEndpoints
{
    public static IEndpointRouteBuilder MapMiCuentaEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/cliente").WithTags("Cuenta cliente").RequireAuthorization();

        group.MapGet("/turnos", MisTurnos);
        group.MapPut("/perfil", ActualizarPerfil);
        group.MapPut("/password", CambiarPassword).RequireRateLimiting(ClienteAuthEndpoints.RateLimitPolicy);

        return app;
    }

    private static async Task<IResult> MisTurnos(ClaimsPrincipal user, BarberDbContext db, CancellationToken ct)
    {
        var denied = CurrentCliente.Require(user, out var principal);
        if (denied is not null) return denied;

        var items = await db.Turnos.AsNoTracking()
            .Include(t => t.Cliente)
            .Include(t => t.Servicio)
            .Include(t => t.Profesional)
            .Include(t => t.Estado)
            .Where(t => t.CuentaId == principal.CuentaId)
            .OrderByDescending(t => t.Inicio)
            .Take(100)
            .ToListAsync(ct);

        return Results.Ok(items.Select(t => t.ToDto()));
    }

    private static async Task<IResult> ActualizarPerfil(
        ActualizarPerfilRequest body,
        ClaimsPrincipal user,
        BarberDbContext db,
        CancellationToken ct)
    {
        var denied = CurrentCliente.Require(user, out var principal);
        if (denied is not null) return denied;

        var cuenta = await db.ClienteCuentas
            .Include(c => c.Cliente)
            .FirstOrDefaultAsync(c => c.Id == principal.CuentaId && c.Activo, ct);
        if (cuenta is null) return HttpErrors.Unauthorized("Sesión inválida.");

        var invalido = ContactValidation.ValidateCliente(body.Nombre, cuenta.Email, body.Telefono);
        if (invalido is not null) return invalido;

        cuenta.Cliente.Nombre = body.Nombre.Trim();
        cuenta.Cliente.Telefono = body.Telefono?.Trim();
        await db.SaveChangesAsync(ct);

        return Results.Ok(cuenta.ToMe());
    }

    private static async Task<IResult> CambiarPassword(
        CambiarPasswordRequest body,
        ClaimsPrincipal user,
        BarberDbContext db,
        CancellationToken ct)
    {
        var denied = CurrentCliente.Require(user, out var principal);
        if (denied is not null) return denied;

        var cuenta = await db.ClienteCuentas.FirstOrDefaultAsync(c => c.Id == principal.CuentaId && c.Activo, ct);
        if (cuenta is null) return HttpErrors.Unauthorized("Sesión inválida.");

        if (cuenta.PasswordHash is null || !BCrypt.Net.BCrypt.Verify(body.PasswordActual ?? "", cuenta.PasswordHash))
            return HttpErrors.Validation("La contraseña actual no es correcta.");

        var error = PasswordPolicy.Validate(body.PasswordNueva);
        if (error is not null) return HttpErrors.Validation(error);
        if (body.PasswordNueva != body.ConfirmarPassword) return HttpErrors.Validation("Las contraseñas no coinciden.");
        if (body.PasswordNueva == body.PasswordActual) return HttpErrors.Validation("La nueva contraseña tiene que ser distinta de la actual.");

        cuenta.PasswordHash = BCrypt.Net.BCrypt.HashPassword(body.PasswordNueva);
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }
}

public record ActualizarPerfilRequest(string Nombre, string? Telefono);

public record CambiarPasswordRequest(string PasswordActual, string PasswordNueva, string ConfirmarPassword);
