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
        Guid? id,
        string? slug,
        CancellationToken ct)
    {
        var salon = !string.IsNullOrWhiteSpace(slug)
            ? await db.Salones.AsNoTracking().FirstOrDefaultAsync(s => s.Slug == slug, ct)
            : await db.Salones.AsNoTracking().FirstOrDefaultAsync(s => s.Id == (id ?? options.Value.DefaultSalonId), ct);

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
        if (body.MailConfirmacion is not null) salon.MailConfirmacion = body.MailConfirmacion.Value;
        if (body.MailRecordatorio is not null) salon.MailRecordatorio = body.MailRecordatorio.Value;
        if (body.MailWhatsappBtn is not null) salon.MailWhatsappBtn = body.MailWhatsappBtn.Value;
        if (body.WspConfirmacion is not null) salon.WspConfirmacion = body.WspConfirmacion.Value;
        if (body.WspRecordatorio is not null) salon.WspRecordatorio = body.WspRecordatorio.Value;
        if (body.WspPlantillaConfirmacion is not null)
            salon.WspPlantillaConfirmacion = SalonMapping.ClipPlantilla(body.WspPlantillaConfirmacion);
        if (body.WspPlantillaRecordatorio is not null)
            salon.WspPlantillaRecordatorio = SalonMapping.ClipPlantilla(body.WspPlantillaRecordatorio);

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
    string? NombreProfesional,
    bool? MailConfirmacion,
    bool? MailRecordatorio,
    bool? MailWhatsappBtn,
    bool? WspConfirmacion,
    bool? WspRecordatorio,
    string? WspPlantillaConfirmacion,
    string? WspPlantillaRecordatorio);

public record SalonDto(
    Guid Id,
    string Nombre,
    string Slug,
    string? Direccion,
    string? Ciudad,
    string? Telefono,
    string? Email,
    string Timezone,
    bool MailConfirmacion,
    bool MailRecordatorio,
    bool MailWhatsappBtn,
    bool WspConfirmacion,
    bool WspRecordatorio,
    string? WspPlantillaConfirmacion,
    string? WspPlantillaRecordatorio);

file static class SalonMapping
{
    private const int MaxPlantilla = 1500;

    public static SalonDto ToDto(this Domain.Salon s) =>
        new(s.Id, s.Nombre, s.Slug, s.Direccion, s.Ciudad, s.Telefono, s.Email, s.Timezone,
            s.MailConfirmacion, s.MailRecordatorio, s.MailWhatsappBtn,
            s.WspConfirmacion, s.WspRecordatorio,
            s.WspPlantillaConfirmacion, s.WspPlantillaRecordatorio);

    /// <summary>Vacío → null (vuelve al default). Recorta a MaxPlantilla.</summary>
    public static string? ClipPlantilla(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var t = value.Trim();
        return t.Length <= MaxPlantilla ? t : t[..MaxPlantilla];
    }
}
