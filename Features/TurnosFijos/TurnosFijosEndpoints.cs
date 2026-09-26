using System.Security.Claims;
using Api.Services.Barber.Common;
using Api.Services.Barber.Data;
using Api.Services.Barber.Domain;
using Microsoft.EntityFrameworkCore;

namespace Api.Services.Barber.Features.TurnosFijos;

public static class TurnosFijosEndpoints
{
    public static IEndpointRouteBuilder MapTurnosFijosEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/turnos-fijos").WithTags("TurnosFijos").RequireAuthorization();

        group.MapGet("/", Listar);
        group.MapPost("/", Crear);
        group.MapPut("/{id:guid}", Actualizar);
        group.MapDelete("/{id:guid}", Eliminar);

        return app;
    }

    private static async Task<IResult> Listar(ClaimsPrincipal user, BarberDbContext db, bool soloActivos = true, CancellationToken ct = default)
    {
        var denied = CurrentUser.Require(user, out var principal);
        if (denied is not null) return denied;

        var query = db.TurnosFijos.AsNoTracking()
            .Include(t => t.Servicio)
            .Where(t => t.ProfesionalId == principal.ProfesionalId);

        if (soloActivos) query = query.Where(t => t.Activo);

        var items = await query
            .OrderBy(t => t.DiaSemana)
            .ThenBy(t => t.Hora)
            .ToListAsync(ct);

        return Results.Ok(items.Select(ToDto));
    }

    private static async Task<IResult> Crear(ClaimsPrincipal user, GuardarTurnoFijoRequest body, BarberDbContext db, CancellationToken ct)
    {
        var denied = CurrentUser.Require(user, out var principal);
        if (denied is not null) return denied;

        var validacion = ValidarBody(body);
        if (validacion is not null) return validacion;

        if (body.ServicioId.HasValue)
        {
            var servicioOk = await db.Servicios.AnyAsync(
                s => s.Id == body.ServicioId && s.SalonId == principal.SalonId && s.Activo, ct);
            if (!servicioOk) return HttpErrors.NotFound("Servicio no encontrado.");
        }

        var conflicto = await db.TurnosFijos.AnyAsync(
            t => t.ProfesionalId == principal.ProfesionalId
                && t.Activo
                && t.DiaSemana == body.DiaSemana
                && t.Hora == body.Hora,
            ct);
        if (conflicto) return HttpErrors.Conflict("Ya hay un turno fijo en ese día y horario.");

        var entity = new TurnoFijo
        {
            Id = Guid.NewGuid(),
            SalonId = principal.SalonId,
            ProfesionalId = principal.ProfesionalId,
            ServicioId = body.ServicioId,
            ClienteNombre = body.ClienteNombre.Trim(),
            ClienteEmail = body.ClienteEmail.Trim().ToLowerInvariant(),
            ClienteTelefono = body.ClienteTelefono?.Trim(),
            DiaSemana = body.DiaSemana,
            Hora = body.Hora,
            DuracionMin = body.DuracionMin ?? 45,
            Activo = true,
            Notas = body.Notas?.Trim()
        };

        db.TurnosFijos.Add(entity);
        await db.SaveChangesAsync(ct);
        await db.Entry(entity).Reference(t => t.Servicio).LoadAsync(ct);
        return Results.Created($"/api/turnos-fijos/{entity.Id}", ToDto(entity));
    }

    private static async Task<IResult> Actualizar(Guid id, ClaimsPrincipal user, GuardarTurnoFijoRequest body, BarberDbContext db, CancellationToken ct)
    {
        var denied = CurrentUser.Require(user, out var principal);
        if (denied is not null) return denied;

        var entity = await db.TurnosFijos
            .Include(t => t.Servicio)
            .FirstOrDefaultAsync(t => t.Id == id && t.ProfesionalId == principal.ProfesionalId, ct);
        if (entity is null) return HttpErrors.NotFound("Turno fijo no encontrado.");

        var validacion = ValidarBody(body);
        if (validacion is not null) return validacion;

        if (body.ServicioId.HasValue)
        {
            var servicioOk = await db.Servicios.AnyAsync(
                s => s.Id == body.ServicioId && s.SalonId == principal.SalonId && s.Activo, ct);
            if (!servicioOk) return HttpErrors.NotFound("Servicio no encontrado.");
        }

        var conflicto = await db.TurnosFijos.AnyAsync(
            t => t.Id != id
                && t.ProfesionalId == principal.ProfesionalId
                && t.Activo
                && t.DiaSemana == body.DiaSemana
                && t.Hora == body.Hora,
            ct);
        if (conflicto) return HttpErrors.Conflict("Ya hay un turno fijo en ese día y horario.");

        entity.ServicioId = body.ServicioId;
        entity.ClienteNombre = body.ClienteNombre.Trim();
        entity.ClienteEmail = body.ClienteEmail.Trim().ToLowerInvariant();
        entity.ClienteTelefono = body.ClienteTelefono?.Trim();
        entity.DiaSemana = body.DiaSemana;
        entity.Hora = body.Hora;
        entity.DuracionMin = body.DuracionMin ?? entity.DuracionMin;
        entity.Notas = body.Notas?.Trim();
        if (body.Activo.HasValue) entity.Activo = body.Activo.Value;

        await db.SaveChangesAsync(ct);
        await db.Entry(entity).Reference(t => t.Servicio).LoadAsync(ct);
        return Results.Ok(ToDto(entity));
    }

    private static async Task<IResult> Eliminar(Guid id, ClaimsPrincipal user, BarberDbContext db, CancellationToken ct)
    {
        var denied = CurrentUser.Require(user, out var principal);
        if (denied is not null) return denied;

        var entity = await db.TurnosFijos.FirstOrDefaultAsync(
            t => t.Id == id && t.ProfesionalId == principal.ProfesionalId, ct);
        if (entity is null) return HttpErrors.NotFound("Turno fijo no encontrado.");

        entity.Activo = false;
        await db.SaveChangesAsync(ct);
        return Results.Ok(new { id = entity.Id, activo = false });
    }

    private static IResult? ValidarBody(GuardarTurnoFijoRequest body)
    {
        if (body.DiaSemana is < 0 or > 6)
            return HttpErrors.Validation("El día de la semana debe estar entre 0 (domingo) y 6 (sábado).");
        if (body.DuracionMin is <= 0)
            return HttpErrors.Validation("La duración debe ser mayor a 0.");

        return ContactValidation.ValidateCliente(body.ClienteNombre, body.ClienteEmail, body.ClienteTelefono);
    }

    private static TurnoFijoDto ToDto(TurnoFijo t) => new(
        t.Id,
        t.SalonId,
        t.ProfesionalId,
        t.ServicioId,
        t.Servicio?.Nombre,
        t.ClienteNombre,
        t.ClienteEmail,
        t.ClienteTelefono,
        t.DiaSemana,
        t.Hora,
        t.DuracionMin,
        t.Activo,
        t.Notas);
}

public record GuardarTurnoFijoRequest(
    string ClienteNombre,
    string ClienteEmail,
    string? ClienteTelefono,
    short DiaSemana,
    TimeOnly Hora,
    Guid? ServicioId,
    int? DuracionMin,
    string? Notas,
    bool? Activo);

public record TurnoFijoDto(
    Guid Id,
    Guid SalonId,
    Guid ProfesionalId,
    Guid? ServicioId,
    string? ServicioNombre,
    string ClienteNombre,
    string ClienteEmail,
    string? ClienteTelefono,
    short DiaSemana,
    TimeOnly Hora,
    int DuracionMin,
    bool Activo,
    string? Notas);
