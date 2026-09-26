using System.Security.Claims;
using Api.Services.Barber.Common;
using Api.Services.Barber.Data;
using Api.Services.Barber.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Api.Services.Barber.Features.Servicios;

public static class ServiciosEndpoints
{
    public static IEndpointRouteBuilder MapServiciosEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/servicios").WithTags("Servicios");

        group.MapGet("/", Listar);
        group.MapGet("/{id:guid}", Obtener);
        group.MapPost("/", Crear).RequireAuthorization();
        group.MapPut("/{id:guid}", Actualizar).RequireAuthorization();
        group.MapDelete("/{id:guid}", Eliminar).RequireAuthorization();

        return app;
    }

    private static async Task<IResult> Listar(
        BarberDbContext db,
        IOptions<AppOptions> options,
        Guid? salonId,
        bool soloActivos = true,
        CancellationToken ct = default)
    {
        var sid = salonId ?? options.Value.DefaultSalonId;
        var query = db.Servicios.AsNoTracking().Where(s => s.SalonId == sid);
        if (soloActivos) query = query.Where(s => s.Activo);

        var items = await query.OrderBy(s => s.Orden).ThenBy(s => s.Nombre).ToListAsync(ct);
        return Results.Ok(items.Select(s => s.ToDto()));
    }

    private static async Task<IResult> Obtener(Guid id, BarberDbContext db, CancellationToken ct)
    {
        var item = await db.Servicios.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id, ct);
        return item is null ? HttpErrors.NotFound("Servicio no encontrado.") : Results.Ok(item.ToDto());
    }

    private static async Task<IResult> Crear(ClaimsPrincipal user, GuardarServicioRequest body, BarberDbContext db, CancellationToken ct)
    {
        var denied = CurrentUser.Require(user, out var principal);
        if (denied is not null) return denied;
        if (string.IsNullOrWhiteSpace(body.Nombre)) return HttpErrors.Validation("El nombre es obligatorio.");
        if (body.DuracionMin <= 0) return HttpErrors.Validation("La duración debe ser mayor a 0.");
        if (body.PrecioCentavos < 0) return HttpErrors.Validation("El precio no puede ser negativo.");

        var entity = new Servicio
        {
            Id = Guid.NewGuid(),
            SalonId = principal.SalonId,
            Nombre = body.Nombre.Trim(),
            Detalle = body.Detalle?.Trim(),
            DuracionMin = body.DuracionMin,
            PrecioCentavos = body.PrecioCentavos,
            Activo = body.Activo ?? true,
            Orden = body.Orden ?? 0
        };

        db.Servicios.Add(entity);
        await db.SaveChangesAsync(ct);
        return Results.Created($"/api/servicios/{entity.Id}", entity.ToDto());
    }

    private static async Task<IResult> Actualizar(Guid id, ClaimsPrincipal user, GuardarServicioRequest body, BarberDbContext db, CancellationToken ct)
    {
        var denied = CurrentUser.Require(user, out var principal);
        if (denied is not null) return denied;

        var entity = await db.Servicios.FirstOrDefaultAsync(s => s.Id == id && s.SalonId == principal.SalonId, ct);
        if (entity is null) return HttpErrors.NotFound("Servicio no encontrado.");
        if (string.IsNullOrWhiteSpace(body.Nombre)) return HttpErrors.Validation("El nombre es obligatorio.");
        if (body.DuracionMin <= 0) return HttpErrors.Validation("La duración debe ser mayor a 0.");
        if (body.PrecioCentavos < 0) return HttpErrors.Validation("El precio no puede ser negativo.");

        entity.Nombre = body.Nombre.Trim();
        entity.Detalle = body.Detalle?.Trim();
        entity.DuracionMin = body.DuracionMin;
        entity.PrecioCentavos = body.PrecioCentavos;
        if (body.Activo.HasValue) entity.Activo = body.Activo.Value;
        if (body.Orden.HasValue) entity.Orden = body.Orden.Value;

        await db.SaveChangesAsync(ct);
        return Results.Ok(entity.ToDto());
    }

    private static async Task<IResult> Eliminar(Guid id, ClaimsPrincipal user, BarberDbContext db, CancellationToken ct)
    {
        var denied = CurrentUser.Require(user, out var principal);
        if (denied is not null) return denied;

        var entity = await db.Servicios.FirstOrDefaultAsync(s => s.Id == id && s.SalonId == principal.SalonId, ct);
        if (entity is null) return HttpErrors.NotFound("Servicio no encontrado.");

        entity.Activo = false;
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }
}

public record GuardarServicioRequest(
    string Nombre,
    string? Detalle,
    int DuracionMin,
    int PrecioCentavos,
    Guid? SalonId,
    bool? Activo,
    int? Orden);

public record ServicioDto(
    Guid Id,
    Guid SalonId,
    string Nombre,
    string? Detalle,
    int DuracionMin,
    int PrecioCentavos,
    bool Activo,
    int Orden);

file static class ServicioMapping
{
    public static ServicioDto ToDto(this Servicio s) =>
        new(s.Id, s.SalonId, s.Nombre, s.Detalle, s.DuracionMin, s.PrecioCentavos, s.Activo, s.Orden);
}
