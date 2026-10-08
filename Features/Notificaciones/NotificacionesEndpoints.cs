using System.Security.Claims;
using Api.Services.Barber.Common;
using Api.Services.Barber.Data;
using Microsoft.EntityFrameworkCore;

namespace Api.Services.Barber.Features.Notificaciones;

public static class NotificacionesEndpoints
{
    public static IEndpointRouteBuilder MapNotificacionesEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/notificaciones").WithTags("Notificaciones").RequireAuthorization();

        group.MapGet("/resumen", Resumen);
        group.MapGet("/", Listar);
        group.MapPost("/{id:guid}/leida", MarcarLeida);
        group.MapPost("/leer-todas", LeerTodas);

        return app;
    }

    /// <summary>
    /// Consulta liviana para la campanita. Devuelve ETag: si nada cambió responde 304 sin cuerpo
    /// (el navegador revalida solo gracias a Cache-Control: no-cache).
    /// </summary>
    private static async Task<IResult> Resumen(HttpContext http, ClaimsPrincipal user, BarberDbContext db, CancellationToken ct)
    {
        var denied = CurrentUser.Require(user, out var principal);
        if (denied is not null) return denied;

        var stats = await db.Notificaciones.AsNoTracking()
            .Where(n => n.ProfesionalId == principal.ProfesionalId)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                NoLeidas = g.Count(n => n.LeidaAt == null),
                UltimaCreada = g.Max(n => (DateTimeOffset?)n.CreatedAt),
                UltimaLeida = g.Max(n => n.LeidaAt)
            })
            .FirstOrDefaultAsync(ct);

        var noLeidas = stats?.NoLeidas ?? 0;
        var version = $"{noLeidas}-{stats?.UltimaCreada?.UtcTicks ?? 0}-{stats?.UltimaLeida?.UtcTicks ?? 0}";
        var etag = $"\"{version}\"";

        http.Response.Headers.CacheControl = "private, no-cache";
        http.Response.Headers.ETag = etag;

        if (http.Request.Headers.IfNoneMatch.Any(v => v == etag))
            return Results.StatusCode(StatusCodes.Status304NotModified);

        return Results.Ok(new NotificacionesResumenDto(noLeidas, version));
    }

    private static async Task<IResult> Listar(ClaimsPrincipal user, BarberDbContext db, int limite = 30, CancellationToken ct = default)
    {
        var denied = CurrentUser.Require(user, out var principal);
        if (denied is not null) return denied;

        var items = await db.Notificaciones.AsNoTracking()
            .Include(n => n.Turno!).ThenInclude(t => t.Estado)
            .Where(n => n.ProfesionalId == principal.ProfesionalId)
            .OrderByDescending(n => n.CreatedAt)
            .Take(Math.Clamp(limite, 1, 100))
            .ToListAsync(ct);

        return Results.Ok(items.Select(n => new NotificacionDto(
            n.Id,
            n.Tipo,
            n.Titulo,
            n.Detalle,
            n.TurnoId,
            n.Turno?.Estado?.Codigo,
            n.Turno is null ? null : SalonTime.ToFecha(n.Turno.Inicio),
            n.Turno is null ? null : SalonTime.ToHora(n.Turno.Inicio),
            n.LeidaAt is not null,
            n.CreatedAt)));
    }

    private static async Task<IResult> MarcarLeida(Guid id, ClaimsPrincipal user, BarberDbContext db, CancellationToken ct)
    {
        var denied = CurrentUser.Require(user, out var principal);
        if (denied is not null) return denied;

        var updated = await db.Notificaciones
            .Where(n => n.Id == id && n.ProfesionalId == principal.ProfesionalId && n.LeidaAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.LeidaAt, DateTimeOffset.UtcNow), ct);

        return Results.Ok(new { id, actualizadas = updated });
    }

    private static async Task<IResult> LeerTodas(ClaimsPrincipal user, BarberDbContext db, CancellationToken ct)
    {
        var denied = CurrentUser.Require(user, out var principal);
        if (denied is not null) return denied;

        var updated = await db.Notificaciones
            .Where(n => n.ProfesionalId == principal.ProfesionalId && n.LeidaAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.LeidaAt, DateTimeOffset.UtcNow), ct);

        return Results.Ok(new { actualizadas = updated });
    }
}

public record NotificacionesResumenDto(int NoLeidas, string Version);

public record NotificacionDto(
    Guid Id,
    string Tipo,
    string Titulo,
    string? Detalle,
    Guid? TurnoId,
    string? TurnoEstadoCodigo,
    DateOnly? TurnoFecha,
    TimeOnly? TurnoHora,
    bool Leida,
    DateTimeOffset CreatedAt);
