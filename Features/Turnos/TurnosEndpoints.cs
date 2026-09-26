using System.Security.Claims;
using Api.Services.Barber.Common;
using Api.Services.Barber.Data;
using Api.Services.Barber.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Api.Services.Barber.Features.Turnos;

public static class TurnosEndpoints
{
    public static IEndpointRouteBuilder MapTurnosEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/turnos").WithTags("Turnos");

        group.MapGet("/", Listar).RequireAuthorization();
        group.MapGet("/{id:guid}", Obtener).RequireAuthorization();
        group.MapPost("/", Crear);
        group.MapPut("/{id:guid}", Actualizar).RequireAuthorization();
        group.MapDelete("/{id:guid}", Cancelar).RequireAuthorization();

        return app;
    }

    private static async Task<IResult> Listar(
        ClaimsPrincipal user,
        BarberDbContext db,
        DateOnly? desde,
        DateOnly? hasta,
        short? estadoId,
        CancellationToken ct = default)
    {
        var denied = CurrentUser.Require(user, out var principal);
        if (denied is not null) return denied;

        var query = db.Turnos.AsNoTracking()
            .Include(t => t.Cliente)
            .Include(t => t.Servicio)
            .Include(t => t.Profesional)
            .Include(t => t.Estado)
            .Where(t => t.ProfesionalId == principal.ProfesionalId);

        if (desde.HasValue)
        {
            var from = SalonTime.ToInstant(desde.Value, TimeOnly.MinValue);
            query = query.Where(t => t.Inicio >= from);
        }

        if (hasta.HasValue)
        {
            var to = SalonTime.ToInstant(hasta.Value.AddDays(1), TimeOnly.MinValue);
            query = query.Where(t => t.Inicio < to);
        }

        if (estadoId.HasValue)
            query = query.Where(t => t.EstadoId == estadoId);

        var items = await query.OrderBy(t => t.Inicio).ToListAsync(ct);
        return Results.Ok(items.Select(t => t.ToDto()));
    }

    private static async Task<IResult> Obtener(Guid id, ClaimsPrincipal user, BarberDbContext db, CancellationToken ct)
    {
        var denied = CurrentUser.Require(user, out var principal);
        if (denied is not null) return denied;

        var item = await db.Turnos.AsNoTracking()
            .Include(t => t.Cliente)
            .Include(t => t.Servicio)
            .Include(t => t.Profesional)
            .Include(t => t.Estado)
            .FirstOrDefaultAsync(t => t.Id == id && t.ProfesionalId == principal.ProfesionalId, ct);

        return item is null ? HttpErrors.NotFound("Turno no encontrado.") : Results.Ok(item.ToDto());
    }

    private static async Task<IResult> Crear(CrearTurnoRequest body, BarberDbContext db, IOptions<AppOptions> options, CancellationToken ct)
    {
        var validacion = ContactValidation.ValidateCliente(body.ClienteNombre, body.ClienteEmail, body.ClienteTelefono);
        if (validacion is not null) return validacion;

        var profesionalId = body.ProfesionalId ?? options.Value.DefaultProfesionalId;
        var profesional = await db.Profesionales.FirstOrDefaultAsync(p => p.Id == profesionalId && p.Activo, ct);
        if (profesional is null) return HttpErrors.NotFound("Profesional no encontrado.");

        var servicio = await db.Servicios.FirstOrDefaultAsync(s => s.Id == body.ServicioId && s.Activo, ct);
        if (servicio is null) return HttpErrors.NotFound("Servicio no encontrado.");

        var cliente = await ObtenerOCrearCliente(db, body.ClienteNombre, body.ClienteEmail, body.ClienteTelefono, ct);
        var inicio = SalonTime.ToInstant(body.Fecha, body.Hora);
        var fin = inicio.AddMinutes(servicio.DuracionMin);
        if (fin <= inicio) return HttpErrors.Validation("El rango horario es inválido.");

        var dow = (short)body.Fecha.DayOfWeek;
        var fijosDia = await db.TurnosFijos.AsNoTracking()
            .Where(f => f.ProfesionalId == profesionalId && f.Activo && f.DiaSemana == dow)
            .ToListAsync(ct);
        if (fijosDia.Any(f =>
            {
                var fInicio = SalonTime.ToInstant(body.Fecha, f.Hora);
                var fFin = fInicio.AddMinutes(f.DuracionMin);
                return inicio < fFin && fin > fInicio;
            }))
        {
            return HttpErrors.Conflict("Ese horario está reservado por un cliente fijo semanal.");
        }

        var turno = new Turno
        {
            Id = Guid.NewGuid(),
            SalonId = profesional.SalonId,
            ClienteId = cliente.Id,
            ProfesionalId = profesional.Id,
            ServicioId = servicio.Id,
            EstadoId = EstadoTurno.Pendiente,
            Inicio = inicio,
            Fin = fin,
            PrecioCentavos = servicio.PrecioCentavos,
            DuracionMin = servicio.DuracionMin,
            Notas = body.Notas?.Trim(),
            Origen = body.Origen ?? OrigenTurno.web
        };

        db.Turnos.Add(turno);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.Is(PostgresErrors.ExclusionViolation))
        {
            return HttpErrors.Conflict("Ese horario ya está ocupado.");
        }

        await db.Entry(turno).Reference(t => t.Cliente).LoadAsync(ct);
        await db.Entry(turno).Reference(t => t.Servicio).LoadAsync(ct);
        await db.Entry(turno).Reference(t => t.Profesional).LoadAsync(ct);
        await db.Entry(turno).Reference(t => t.Estado).LoadAsync(ct);

        return Results.Created($"/api/turnos/{turno.Id}", turno.ToDto());
    }

    private static async Task<IResult> Actualizar(Guid id, ClaimsPrincipal user, ActualizarTurnoRequest body, BarberDbContext db, CancellationToken ct)
    {
        var denied = CurrentUser.Require(user, out var principal);
        if (denied is not null) return denied;

        var turno = await db.Turnos
            .Include(t => t.Cliente)
            .Include(t => t.Servicio)
            .Include(t => t.Profesional)
            .Include(t => t.Estado)
            .FirstOrDefaultAsync(t => t.Id == id && t.ProfesionalId == principal.ProfesionalId, ct);

        if (turno is null) return HttpErrors.NotFound("Turno no encontrado.");

        if (body.ServicioId.HasValue && body.ServicioId != turno.ServicioId)
        {
            var servicio = await db.Servicios.FirstOrDefaultAsync(s => s.Id == body.ServicioId && s.Activo, ct);
            if (servicio is null) return HttpErrors.NotFound("Servicio no encontrado.");
            turno.ServicioId = servicio.Id;
            turno.PrecioCentavos = servicio.PrecioCentavos;
            turno.DuracionMin = servicio.DuracionMin;
            turno.Servicio = servicio;
        }

        if (body.Fecha.HasValue && body.Hora.HasValue)
        {
            turno.Inicio = SalonTime.ToInstant(body.Fecha.Value, body.Hora.Value);
            turno.Fin = turno.Inicio.AddMinutes(turno.DuracionMin);
        }
        else if (body.ServicioId.HasValue)
        {
            turno.Fin = turno.Inicio.AddMinutes(turno.DuracionMin);
        }

        if (body.EstadoId.HasValue && body.EstadoId != turno.EstadoId)
        {
            var estado = await db.EstadosTurno.FirstOrDefaultAsync(e => e.Id == body.EstadoId, ct);
            if (estado is null) return HttpErrors.Validation("Estado inválido.");
            turno.EstadoId = estado.Id;
            turno.Estado = estado;
            if (estado.Id == EstadoTurno.Confirmado) turno.ConfirmadoAt ??= DateTimeOffset.UtcNow;
            if (estado.Id == EstadoTurno.Cancelado) turno.CanceladoAt ??= DateTimeOffset.UtcNow;
        }

        if (body.Notas is not null) turno.Notas = body.Notas.Trim();

        if (!string.IsNullOrWhiteSpace(body.ClienteNombre)) turno.Cliente.Nombre = body.ClienteNombre.Trim();
        if (!string.IsNullOrWhiteSpace(body.ClienteTelefono)) turno.Cliente.Telefono = body.ClienteTelefono.Trim();

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.Is(PostgresErrors.ExclusionViolation))
        {
            return HttpErrors.Conflict("Ese horario ya está ocupado.");
        }

        return Results.Ok(turno.ToDto());
    }

    private static async Task<IResult> Cancelar(Guid id, ClaimsPrincipal user, BarberDbContext db, CancellationToken ct)
    {
        var denied = CurrentUser.Require(user, out var principal);
        if (denied is not null) return denied;

        var turno = await db.Turnos
            .Include(t => t.Cliente)
            .Include(t => t.Servicio)
            .Include(t => t.Profesional)
            .Include(t => t.Estado)
            .FirstOrDefaultAsync(t => t.Id == id && t.ProfesionalId == principal.ProfesionalId, ct);

        if (turno is null) return HttpErrors.NotFound("Turno no encontrado.");

        turno.EstadoId = EstadoTurno.Cancelado;
        turno.CanceladoAt ??= DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        await db.Entry(turno).Reference(t => t.Estado).LoadAsync(ct);
        return Results.Ok(turno.ToDto());
    }

    private static IResult? ValidarCliente(string? nombre, string? email, string? telefono = null) =>
        ContactValidation.ValidateCliente(nombre, email, telefono);

    private static async Task<Cliente> ObtenerOCrearCliente(
        BarberDbContext db,
        string nombre,
        string email,
        string? telefono,
        CancellationToken ct)
    {
        var normalized = email.Trim().ToLowerInvariant();
        var existente = await db.Clientes.FirstOrDefaultAsync(c => c.Email == normalized, ct);
        if (existente is not null)
        {
            existente.Nombre = nombre.Trim();
            if (!string.IsNullOrWhiteSpace(telefono)) existente.Telefono = telefono.Trim();
            return existente;
        }

        var cliente = new Cliente
        {
            Id = Guid.NewGuid(),
            Nombre = nombre.Trim(),
            Email = normalized,
            Telefono = telefono?.Trim()
        };
        db.Clientes.Add(cliente);
        return cliente;
    }
}

public record CrearTurnoRequest(
    DateOnly Fecha,
    TimeOnly Hora,
    Guid ServicioId,
    string ClienteNombre,
    string ClienteEmail,
    string? ClienteTelefono,
    Guid? ProfesionalId,
    string? Notas,
    OrigenTurno? Origen);

public record ActualizarTurnoRequest(
    DateOnly? Fecha,
    TimeOnly? Hora,
    Guid? ServicioId,
    short? EstadoId,
    string? Notas,
    string? ClienteNombre,
    string? ClienteTelefono);

public record TurnoDto(
    Guid Id,
    Guid SalonId,
    Guid ClienteId,
    Guid ProfesionalId,
    Guid ServicioId,
    short EstadoId,
    string Estado,
    string EstadoCodigo,
    DateOnly Fecha,
    TimeOnly Hora,
    DateTimeOffset Inicio,
    DateTimeOffset Fin,
    int PrecioCentavos,
    int DuracionMin,
    string? Notas,
    OrigenTurno Origen,
    string ClienteNombre,
    string ClienteEmail,
    string? ClienteTelefono,
    string ServicioNombre,
    string ProfesionalNombre);

file static class TurnoMapping
{
    public static TurnoDto ToDto(this Turno t) =>
        new(
            t.Id,
            t.SalonId,
            t.ClienteId,
            t.ProfesionalId,
            t.ServicioId,
            t.EstadoId,
            t.Estado.Nombre,
            t.Estado.Codigo,
            SalonTime.ToFecha(t.Inicio),
            SalonTime.ToHora(t.Inicio),
            t.Inicio,
            t.Fin,
            t.PrecioCentavos,
            t.DuracionMin,
            t.Notas,
            t.Origen,
            t.Cliente.Nombre,
            t.Cliente.Email,
            t.Cliente.Telefono,
            t.Servicio.Nombre,
            t.Profesional.Nombre);
}
