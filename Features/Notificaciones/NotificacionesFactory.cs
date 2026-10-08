using Api.Services.Barber.Common;
using Api.Services.Barber.Data;
using Api.Services.Barber.Domain;
using Microsoft.EntityFrameworkCore;

namespace Api.Services.Barber.Features.Notificaciones;

public static class NotificacionesFactory
{
    public static Notificacion TurnoSolicitado(Turno turno, string clienteNombre, string servicioNombre) => new()
    {
        Id = Guid.NewGuid(),
        SalonId = turno.SalonId,
        ProfesionalId = turno.ProfesionalId,
        TurnoId = turno.Id,
        Tipo = Notificacion.TipoTurnoSolicitado,
        Titulo = "Nuevo turno para confirmar",
        Detalle = Recortar($"{clienteNombre} · {servicioNombre} · {SalonTime.FormatCorto(turno.Inicio)}", 240),
        CreatedAt = DateTimeOffset.UtcNow
    };

    /// <summary>Al confirmar o cancelar un turno, sus avisos dejan de estar pendientes.</summary>
    public static Task<int> MarcarLeidasPorTurnoAsync(BarberDbContext db, Guid turnoId, CancellationToken ct) =>
        db.Notificaciones
            .Where(n => n.TurnoId == turnoId && n.LeidaAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.LeidaAt, DateTimeOffset.UtcNow), ct);

    private static string Recortar(string value, int max) => value.Length <= max ? value : value[..max];
}
