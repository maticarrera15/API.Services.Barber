using System.Security.Claims;
using Api.Services.Barber.Common;
using Api.Services.Barber.Data;
using Api.Services.Barber.Domain;
using Microsoft.EntityFrameworkCore;

namespace Api.Services.Barber.Features.Bloqueos;

/// <summary>
/// Excepciones de agenda: bloquear una franja (ej. "ese día arranco a las 12" = bloqueo de apertura→12).
/// </summary>
public static class BloqueosEndpoints
{
    public static IEndpointRouteBuilder MapBloqueosEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/bloqueos").WithTags("Bloqueos").RequireAuthorization();
        group.MapGet("/", Listar);
        group.MapPost("/inicio-especial", CrearInicioEspecial);
        group.MapDelete("/{id:guid}", Eliminar);
        return app;
    }

    private static async Task<IResult> Listar(
        ClaimsPrincipal user,
        BarberDbContext db,
        DateOnly? desde,
        DateOnly? hasta,
        CancellationToken ct)
    {
        var denied = CurrentUser.Require(user, out var principal);
        if (denied is not null) return denied;

        var from = SalonTime.ToInstant(desde ?? SalonTime.Today(), TimeOnly.MinValue);
        var toDate = hasta ?? SalonTime.Today().AddDays(60);
        var to = SalonTime.ToInstant(toDate.AddDays(1), TimeOnly.MinValue);

        var items = await db.BloqueosAgenda.AsNoTracking()
            .Where(b => b.SalonId == principal.SalonId
                && (b.ProfesionalId == null || b.ProfesionalId == principal.ProfesionalId)
                && b.Inicio < to && b.Fin > from)
            .OrderBy(b => b.Inicio)
            .ToListAsync(ct);

        return Results.Ok(items.Select(ToDto));
    }

    /// <summary>
    /// Ese día la atención empieza a <paramref name="body.HoraInicio"/>: bloquea desde la apertura habitual hasta esa hora.
    /// </summary>
    private static async Task<IResult> CrearInicioEspecial(
        ClaimsPrincipal user,
        InicioEspecialRequest body,
        BarberDbContext db,
        CancellationToken ct)
    {
        var denied = CurrentUser.Require(user, out var principal);
        if (denied is not null) return denied;

        if (body.Fecha < SalonTime.Today())
            return HttpErrors.Validation("La fecha tiene que ser hoy o posterior.");

        var dow = (short)body.Fecha.DayOfWeek;
        var horario = await db.HorariosLaborales.AsNoTracking()
            .Where(h => h.SalonId == principal.SalonId
                && h.Activo
                && h.DiaSemana == dow
                && (h.ProfesionalId == principal.ProfesionalId || h.ProfesionalId == null))
            .OrderByDescending(h => h.ProfesionalId.HasValue)
            .FirstOrDefaultAsync(ct);

        if (horario is null)
            return HttpErrors.Validation("Ese día no es laborable en tu calendario habitual.");

        if (body.HoraInicio <= horario.HoraApertura)
            return HttpErrors.Validation($"La hora tiene que ser después de la apertura habitual ({horario.HoraApertura:HH\\:mm}).");

        if (body.HoraInicio >= horario.HoraCierre)
            return HttpErrors.Validation("La hora de inicio tiene que ser antes del cierre.");

        var dayStart = SalonTime.ToInstant(body.Fecha, TimeOnly.MinValue);
        var dayEnd = dayStart.AddDays(1);
        var viejos = await db.BloqueosAgenda
            .Where(b => b.SalonId == principal.SalonId
                && b.ProfesionalId == principal.ProfesionalId
                && b.Inicio >= dayStart && b.Inicio < dayEnd
                && b.Motivo != null && b.Motivo.StartsWith("Inicio a las"))
            .ToListAsync(ct);
        db.BloqueosAgenda.RemoveRange(viejos);

        var entity = new BloqueoAgenda
        {
            Id = Guid.NewGuid(),
            SalonId = principal.SalonId,
            ProfesionalId = principal.ProfesionalId,
            Inicio = SalonTime.ToInstant(body.Fecha, horario.HoraApertura),
            Fin = SalonTime.ToInstant(body.Fecha, body.HoraInicio),
            Motivo = $"Inicio a las {body.HoraInicio:HH\\:mm}"
        };
        db.BloqueosAgenda.Add(entity);
        await db.SaveChangesAsync(ct);
        return Results.Created($"/api/bloqueos/{entity.Id}", ToDto(entity));
    }

    private static async Task<IResult> Eliminar(Guid id, ClaimsPrincipal user, BarberDbContext db, CancellationToken ct)
    {
        var denied = CurrentUser.Require(user, out var principal);
        if (denied is not null) return denied;

        var entity = await db.BloqueosAgenda.FirstOrDefaultAsync(
            b => b.Id == id && b.SalonId == principal.SalonId, ct);
        if (entity is null) return HttpErrors.NotFound("Bloqueo no encontrado.");

        db.BloqueosAgenda.Remove(entity);
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static BloqueoDto ToDto(BloqueoAgenda b) =>
        new(b.Id, b.Inicio, b.Fin, b.Motivo, SalonTime.ToFecha(b.Inicio), SalonTime.ToHora(b.Inicio), SalonTime.ToHora(b.Fin));
}

public record InicioEspecialRequest(DateOnly Fecha, TimeOnly HoraInicio);

public record BloqueoDto(
    Guid Id,
    DateTimeOffset Inicio,
    DateTimeOffset Fin,
    string? Motivo,
    DateOnly Fecha,
    TimeOnly HoraInicio,
    TimeOnly HoraFin);
