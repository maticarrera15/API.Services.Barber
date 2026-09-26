using Api.Services.Barber.Common;
using Api.Services.Barber.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Api.Services.Barber.Features.Profesionales;

public static class ProfesionalEndpoints
{
    public static IEndpointRouteBuilder MapProfesionalEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/profesional").WithTags("Profesional");
        group.MapGet("/", ObtenerPublico);
        return app;
    }

    private static async Task<IResult> ObtenerPublico(
        BarberDbContext db,
        IOptions<AppOptions> options,
        Guid? id,
        CancellationToken ct)
    {
        var pid = id ?? options.Value.DefaultProfesionalId;
        var pro = await db.Profesionales.AsNoTracking()
            .Include(p => p.Salon)
            .FirstOrDefaultAsync(p => p.Id == pid && p.Activo, ct);

        return pro is null
            ? HttpErrors.NotFound("Profesional no encontrado.")
            : Results.Ok(new ProfesionalPublicoDto(
                pro.Id,
                pro.SalonId,
                pro.Nombre,
                pro.Rol,
                pro.Email,
                pro.Telefono,
                pro.Iniciales,
                pro.Salon.Nombre));
    }
}

public record ProfesionalPublicoDto(
    Guid Id,
    Guid SalonId,
    string Nombre,
    string? Rol,
    string? Email,
    string? Telefono,
    string? Iniciales,
    string SalonNombre);
