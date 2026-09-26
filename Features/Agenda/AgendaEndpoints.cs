using System.Security.Claims;
using Api.Services.Barber.Common;
using Api.Services.Barber.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Api.Services.Barber.Features.Agenda;

public static class AgendaEndpoints
{
    public static IEndpointRouteBuilder MapAgendaEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/agenda").WithTags("Agenda");

        group.MapGet("/mia", ListarMia).RequireAuthorization();
        group.MapGet("/mia/{fecha}", ObtenerMia).RequireAuthorization();
        group.MapGet("/", ListarPublica);
        group.MapGet("/{fecha}", ObtenerPublica);

        return app;
    }

    private static async Task<IResult> ListarPublica(
        BarberDbContext db,
        IOptions<AppOptions> options,
        Guid? profesionalId,
        DateOnly? desde,
        DateOnly? hasta,
        CancellationToken ct = default)
    {
        var semana = SalonTime.Semana(SalonTime.Today());
        var from = desde ?? semana.Desde;
        var to = hasta ?? semana.Hasta;
        if (to < from) return HttpErrors.Validation("hasta no puede ser anterior a desde.");

        var pid = profesionalId ?? options.Value.DefaultProfesionalId;
        var dias = await Construir(db, pid, from, to, incluirDetalle: false, ct);
        return Results.Ok(new AgendaRangoDto(pid, from, to, dias));
    }

    private static async Task<IResult> ObtenerPublica(
        DateOnly fecha,
        BarberDbContext db,
        IOptions<AppOptions> options,
        Guid? profesionalId,
        CancellationToken ct = default)
    {
        var pid = profesionalId ?? options.Value.DefaultProfesionalId;
        var dias = await Construir(db, pid, fecha, fecha, incluirDetalle: false, ct);
        var dia = dias.FirstOrDefault();
        return dia is null ? HttpErrors.NotFound("No se pudo armar la agenda de ese día.") : Results.Ok(dia);
    }

    private static async Task<IResult> ListarMia(
        ClaimsPrincipal user,
        BarberDbContext db,
        DateOnly? desde,
        DateOnly? hasta,
        CancellationToken ct = default)
    {
        var denied = CurrentUser.Require(user, out var principal);
        if (denied is not null) return denied;

        var semana = SalonTime.Semana(SalonTime.Today());
        var from = desde ?? semana.Desde;
        var to = hasta ?? semana.Hasta;
        if (to < from) return HttpErrors.Validation("hasta no puede ser anterior a desde.");

        var dias = await Construir(db, principal.ProfesionalId, from, to, incluirDetalle: true, ct);
        return Results.Ok(new AgendaRangoDto(principal.ProfesionalId, from, to, dias));
    }

    private static async Task<IResult> ObtenerMia(
        DateOnly fecha,
        ClaimsPrincipal user,
        BarberDbContext db,
        CancellationToken ct = default)
    {
        var denied = CurrentUser.Require(user, out var principal);
        if (denied is not null) return denied;

        var dias = await Construir(db, principal.ProfesionalId, fecha, fecha, incluirDetalle: true, ct);
        var dia = dias.FirstOrDefault();
        return dia is null ? HttpErrors.NotFound("No se pudo armar la agenda de ese día.") : Results.Ok(dia);
    }

    private static async Task<List<AgendaDiaDto>> Construir(
        BarberDbContext db,
        Guid profesionalId,
        DateOnly desde,
        DateOnly hasta,
        bool incluirDetalle,
        CancellationToken ct)
    {
        var profesional = await db.Profesionales.AsNoTracking().FirstOrDefaultAsync(p => p.Id == profesionalId, ct);
        if (profesional is null) return [];

        var salonId = profesional.SalonId;
        var rangoInicio = SalonTime.ToInstant(desde, TimeOnly.MinValue);
        var rangoFin = SalonTime.ToInstant(hasta.AddDays(1), TimeOnly.MinValue);

        var horarios = await db.HorariosLaborales.AsNoTracking()
            .Where(h => h.SalonId == salonId && (h.ProfesionalId == profesionalId || h.ProfesionalId == null))
            .ToListAsync(ct);

        var bloqueos = await db.BloqueosAgenda.AsNoTracking()
            .Where(b => b.SalonId == salonId
                && (b.ProfesionalId == null || b.ProfesionalId == profesionalId)
                && b.Inicio < rangoFin && b.Fin > rangoInicio)
            .ToListAsync(ct);

        var turnos = await db.Turnos.AsNoTracking()
            .Include(t => t.Estado)
            .Include(t => t.Cliente)
            .Include(t => t.Servicio)
            .Where(t => t.ProfesionalId == profesionalId && t.Inicio < rangoFin && t.Fin > rangoInicio)
            .ToListAsync(ct);

        var fijos = await db.TurnosFijos.AsNoTracking()
            .Include(f => f.Servicio)
            .Where(f => f.ProfesionalId == profesionalId && f.Activo)
            .ToListAsync(ct);

        var dias = new List<AgendaDiaDto>();
        for (var fecha = desde; fecha <= hasta; fecha = fecha.AddDays(1))
        {
            var dow = (short)fecha.DayOfWeek;
            var horario = horarios.FirstOrDefault(h => h.DiaSemana == dow && h.ProfesionalId == profesionalId)
                ?? horarios.FirstOrDefault(h => h.DiaSemana == dow && h.ProfesionalId == null);

            var dayStart = SalonTime.ToInstant(fecha, TimeOnly.MinValue);
            var dayEnd = SalonTime.ToInstant(fecha.AddDays(1), TimeOnly.MinValue);
            var bloqueo = bloqueos.FirstOrDefault(b => b.Inicio < dayEnd && b.Fin > dayStart);

            if (horario is null || !horario.Activo)
            {
                dias.Add(new AgendaDiaDto(fecha, dow, false, "Día no laborable", []));
                continue;
            }

            if (bloqueo is not null)
            {
                dias.Add(new AgendaDiaDto(fecha, dow, false, bloqueo.Motivo ?? "Agenda bloqueada", []));
                continue;
            }

            var fijosDia = fijos.Where(f => f.DiaSemana == dow).ToList();
            var slots = new List<AgendaSlotDto>();
            var step = Math.Max(5, horario.SlotMinutos);
            var t = horario.HoraApertura;
            while (t < horario.HoraCierre)
            {
                // El slot debe caber completo dentro de la franja (y no cruzar la medianoche).
                var finLocal = t.AddMinutes(step);
                if (finLocal > horario.HoraCierre || finLocal <= t) break;

                // Si el slot pisa la pausa, la grilla de la tarde arranca exactamente al terminar el receso.
                if (horario.PausaInicio is { } pi && horario.PausaFin is { } pf && t < pf && finLocal > pi)
                {
                    t = pf;
                    continue;
                }

                var slotInicio = SalonTime.ToInstant(fecha, t);
                var slotFin = slotInicio.AddMinutes(step);
                var ocupante = turnos.FirstOrDefault(x =>
                    x.Estado is { BloqueaSlot: true }
                    && x.Inicio < slotFin
                    && x.Fin > slotInicio);

                var fijo = fijosDia.FirstOrDefault(f =>
                {
                    var fInicio = SalonTime.ToInstant(fecha, f.Hora);
                    var fFin = fInicio.AddMinutes(f.DuracionMin);
                    return fInicio < slotFin && fFin > slotInicio;
                });

                var ocupado = ocupante is not null || fijo is not null;
                slots.Add(new AgendaSlotDto(
                    t,
                    ocupado,
                    incluirDetalle ? ocupante?.Id ?? fijo?.Id : null,
                    incluirDetalle ? ocupante?.Cliente?.Nombre ?? fijo?.ClienteNombre : null,
                    incluirDetalle ? ocupante?.Servicio?.Nombre ?? fijo?.Servicio?.Nombre ?? (fijo is not null ? "Turno fijo" : null) : null,
                    incluirDetalle ? ocupante?.Estado?.Codigo ?? (fijo is not null ? "Fijo" : null) : null));

                t = finLocal;
            }

            dias.Add(new AgendaDiaDto(fecha, dow, true, null, slots));
        }

        return dias;
    }
}

public record AgendaRangoDto(Guid ProfesionalId, DateOnly Desde, DateOnly Hasta, IReadOnlyList<AgendaDiaDto> Dias);

public record AgendaDiaDto(
    DateOnly Fecha,
    short DiaSemana,
    bool Laborable,
    string? Motivo,
    IReadOnlyList<AgendaSlotDto> Slots);

public record AgendaSlotDto(
    TimeOnly Hora,
    bool Ocupado,
    Guid? TurnoId,
    string? ClienteNombre,
    string? ServicioNombre,
    string? EstadoCodigo);
