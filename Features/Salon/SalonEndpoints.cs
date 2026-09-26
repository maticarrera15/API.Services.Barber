using System.Security.Claims;
using Api.Services.Barber.Common;
using Api.Services.Barber.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Api.Services.Barber.Features.Salon;

public static class SalonEndpoints
{
    public static IEndpointRouteBuilder MapSalonEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/salon").WithTags("Salon");

        group.MapGet("/", ObtenerPublico);
        group.MapPut("/", Actualizar).RequireAuthorization();

        return app;
    }

    private static async Task<IResult> ObtenerPublico(
        BarberDbContext db,
        IOptions<AppOptions> options,
        string? slug,
        CancellationToken ct)
    {
        var salon = string.IsNullOrWhiteSpace(slug)
            ? await db.Salones.AsNoTracking().FirstOrDefaultAsync(s => s.Id == options.Value.DefaultSalonId, ct)
            : await db.Salones.AsNoTracking().FirstOrDefaultAsync(s => s.Slug == slug, ct);

        return salon is null ? HttpErrors.NotFound("Salón no encontrado.") : Results.Ok(salon.ToDto());
    }

    private static async Task<IResult> Actualizar(
        ClaimsPrincipal user,
        ActualizarSalonRequest body,
        BarberDbContext db,
        CancellationToken ct)
    {
        var denied = CurrentUser.Require(user, out var principal);
        if (denied is not null) return denied;

        var salon = await db.Salones.FirstOrDefaultAsync(s => s.Id == principal.SalonId, ct);
        if (salon is null) return HttpErrors.NotFound("Salón no encontrado.");

        if (!string.IsNullOrWhiteSpace(body.Nombre)) salon.Nombre = body.Nombre.Trim();
        if (body.Direccion is not null) salon.Direccion = body.Direccion.Trim();
        if (body.Ciudad is not null) salon.Ciudad = body.Ciudad.Trim();
        if (body.Telefono is not null) salon.Telefono = body.Telefono.Trim();
        if (body.Email is not null) salon.Email = body.Email.Trim();
        if (!string.IsNullOrWhiteSpace(body.Timezone)) salon.Timezone = body.Timezone.Trim();

        var profesional = await db.Profesionales.FirstOrDefaultAsync(p => p.Id == principal.ProfesionalId, ct);
        if (profesional is not null)
        {
            if (!string.IsNullOrWhiteSpace(body.NombreProfesional)) profesional.Nombre = body.NombreProfesional.Trim();
            if (body.Telefono is not null) profesional.Telefono = body.Telefono.Trim();
        }

        await db.SaveChangesAsync(ct);
        return Results.Ok(salon.ToDto());
    }
}

public record ActualizarSalonRequest(
    string? Nombre,
    string? Direccion,
    string? Ciudad,
    string? Telefono,
    string? Email,
    string? Timezone,
    string? NombreProfesional);

public record SalonDto(
    Guid Id,
    string Nombre,
    string Slug,
    string? Direccion,
    string? Ciudad,
    string? Telefono,
    string? Email,
    string Timezone);

file static class SalonMapping
{
    public static SalonDto ToDto(this Domain.Salon s) =>
        new(s.Id, s.Nombre, s.Slug, s.Direccion, s.Ciudad, s.Telefono, s.Email, s.Timezone);
}
