using System.Security.Claims;
using Api.Services.Barber.Common;
using Api.Services.Barber.Data;
using Api.Services.Barber.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Api.Services.Barber.Features.Horarios;

public static class HorariosEndpoints
{
    public static IEndpointRouteBuilder MapHorariosEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/horarios").WithTags("Horarios");

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
        Guid? profesionalId,
        CancellationToken ct = default)
    {
        var sid = salonId ?? options.Value.DefaultSalonId;
        var query = db.HorariosLaborales.AsNoTracking().Where(h => h.SalonId == sid);

        query = profesionalId.HasValue
            ? query.Where(h => h.ProfesionalId == profesionalId)
            : query.Where(h => h.ProfesionalId == null);

        var items = await query.OrderBy(h => h.DiaSemana).ToListAsync(ct);
        return Results.Ok(items.Select(h => h.ToDto()));
    }

    private static async Task<IResult> Obtener(Guid id, BarberDbContext db, CancellationToken ct)
    {
        var item = await db.HorariosLaborales.AsNoTracking().FirstOrDefaultAsync(h => h.Id == id, ct);
        return item is null ? HttpErrors.NotFound("Horario no encontrado.") : Results.Ok(item.ToDto());
    }

    private static async Task<IResult> Crear(ClaimsPrincipal user, GuardarHorarioRequest body, BarberDbContext db, CancellationToken ct)
    {
        var denied = CurrentUser.Require(user, out var principal);
        if (denied is not null) return denied;

        var error = Validar(body);
        if (error is not null) return error;

        var entity = new HorarioLaboral
        {
            Id = Guid.NewGuid(),
            SalonId = principal.SalonId,
            ProfesionalId = body.ProfesionalId ?? principal.ProfesionalId,
            DiaSemana = body.DiaSemana,
            Activo = body.Activo,
            HoraApertura = body.HoraApertura,
            HoraCierre = body.HoraCierre,
            PausaInicio = body.PausaInicio,
            PausaFin = body.PausaFin,
            SlotMinutos = body.SlotMinutos
        };

        db.HorariosLaborales.Add(entity);
        return await Guardar(db, entity, ct, created: true);
    }

    private static async Task<IResult> Actualizar(Guid id, ClaimsPrincipal user, GuardarHorarioRequest body, BarberDbContext db, CancellationToken ct)
    {
        var denied = CurrentUser.Require(user, out var principal);
        if (denied is not null) return denied;

        var error = Validar(body);
        if (error is not null) return error;

        var entity = await db.HorariosLaborales.FirstOrDefaultAsync(h => h.Id == id && h.SalonId == principal.SalonId, ct);
        if (entity is null) return HttpErrors.NotFound("Horario no encontrado.");

        entity.DiaSemana = body.DiaSemana;
        entity.Activo = body.Activo;
        entity.HoraApertura = body.HoraApertura;
        entity.HoraCierre = body.HoraCierre;
        entity.PausaInicio = body.PausaInicio;
        entity.PausaFin = body.PausaFin;
        entity.SlotMinutos = body.SlotMinutos;

        return await Guardar(db, entity, ct, created: false);
    }

    private static async Task<IResult> Eliminar(Guid id, ClaimsPrincipal user, BarberDbContext db, CancellationToken ct)
    {
        var denied = CurrentUser.Require(user, out var principal);
        if (denied is not null) return denied;

        var entity = await db.HorariosLaborales.FirstOrDefaultAsync(h => h.Id == id && h.SalonId == principal.SalonId, ct);
        if (entity is null) return HttpErrors.NotFound("Horario no encontrado.");

        db.HorariosLaborales.Remove(entity);
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static IResult? Validar(GuardarHorarioRequest body)
    {
        if (body.DiaSemana is < 0 or > 6) return HttpErrors.Validation("diaSemana debe estar entre 0 (domingo) y 6 (sábado).");
        if (body.HoraCierre <= body.HoraApertura) return HttpErrors.Validation("La hora de cierre debe ser posterior a la de apertura.");
        if (body.SlotMinutos <= 0) return HttpErrors.Validation("slotMinutos debe ser mayor a 0.");
        if (body.PausaInicio.HasValue != body.PausaFin.HasValue)
            return HttpErrors.Validation("La pausa requiere inicio y fin.");
        if (body.PausaInicio.HasValue && body.PausaFin <= body.PausaInicio)
            return HttpErrors.Validation("El fin de la pausa debe ser posterior al inicio.");
        if (body.PausaInicio.HasValue && (body.PausaInicio <= body.HoraApertura || body.PausaFin >= body.HoraCierre))
            return HttpErrors.Validation("La pausa debe quedar entre el primer turno de la mañana y el último de la tarde.");
        return null;
    }

    private static async Task<IResult> Guardar(BarberDbContext db, HorarioLaboral entity, CancellationToken ct, bool created)
    {
        try
        {
            await db.SaveChangesAsync(ct);
            return created
                ? Results.Created($"/api/horarios/{entity.Id}", entity.ToDto())
                : Results.Ok(entity.ToDto());
        }
        catch (DbUpdateException ex) when (ex.Is(PostgresErrors.UniqueViolation))
        {
            return HttpErrors.Conflict("Ya existe un horario para ese día (salón o profesional).");
        }
        catch (DbUpdateException ex) when (ex.Is(PostgresErrors.CheckViolation))
        {
            return HttpErrors.Validation("El horario no cumple las reglas de rango o pausa.");
        }
    }
}

public record GuardarHorarioRequest(
    short DiaSemana,
    bool Activo,
    TimeOnly HoraApertura,
    TimeOnly HoraCierre,
    TimeOnly? PausaInicio,
    TimeOnly? PausaFin,
    int SlotMinutos,
    Guid? SalonId,
    Guid? ProfesionalId);

public record HorarioDto(
    Guid Id,
    Guid SalonId,
    Guid? ProfesionalId,
    short DiaSemana,
    bool Activo,
    TimeOnly HoraApertura,
    TimeOnly HoraCierre,
    TimeOnly? PausaInicio,
    TimeOnly? PausaFin,
    int SlotMinutos);

file static class HorarioMapping
{
    public static HorarioDto ToDto(this HorarioLaboral h) =>
        new(h.Id, h.SalonId, h.ProfesionalId, h.DiaSemana, h.Activo,
            h.HoraApertura, h.HoraCierre, h.PausaInicio, h.PausaFin, h.SlotMinutos);
}
