using Api.Services.Barber.Common;
using Api.Services.Barber.Data;
using Api.Services.Barber.Domain;
using Microsoft.EntityFrameworkCore;

namespace Api.Services.Barber.Features.Email;

public record RecordatoriosResultado(int Programados, int Enviados, int Fallidos, int Omitidos, int SiguenPendientes);

/// <summary>
/// Lo dispara el cron cada pocos minutos: programa los recordatorios de las próximas 3 h
/// y reintenta los mails que quedaron pendientes (reinicio del proceso o error temporal de Resend).
/// </summary>
public class RecordatoriosJob(BarberDbContext db, EmailDispatcher dispatcher, ILogger<RecordatoriosJob> logger)
{
    public static readonly TimeSpan Anticipacion = TimeSpan.FromHours(3);

    // El cron corta a los 30 s y Resend admite ~2 envíos por segundo.
    private const int MaxEnviosPorCorrida = 20;
    private static readonly TimeSpan PausaEntreEnvios = TimeSpan.FromMilliseconds(600);

    public async Task<RecordatoriosResultado> EjecutarAsync(CancellationToken ct)
    {
        var ahora = DateTimeOffset.UtcNow;
        var nuevos = await ProgramarAsync(ahora, ct);

        // Las pendientes recientes las está mandando el worker: solo se reintentan las que quedaron colgadas.
        var colgadasDesde = ahora.AddMinutes(-2);
        var colgadas = await db.NotificacionesEmail.AsNoTracking()
            .Where(n => n.Estado == NotificacionEmail.EstadoPendiente && n.CreatedAt < colgadasDesde)
            .OrderBy(n => n.CreatedAt)
            .Select(n => n.Id)
            .Take(MaxEnviosPorCorrida)
            .ToListAsync(ct);

        var aProcesar = nuevos.Concat(colgadas).Distinct().Take(MaxEnviosPorCorrida).ToList();
        int enviados = 0, fallidos = 0, omitidos = 0;
        for (var i = 0; i < aProcesar.Count; i++)
        {
            if (i > 0) await Task.Delay(PausaEntreEnvios, ct);
            switch (await dispatcher.ProcesarAsync(aProcesar[i], ct))
            {
                case NotificacionEmail.EstadoEnviado: enviados++; break;
                case NotificacionEmail.EstadoFallido: fallidos++; break;
                case NotificacionEmail.EstadoOmitido: omitidos++; break;
            }
        }

        var siguenPendientes = await db.NotificacionesEmail.CountAsync(n => n.Estado == NotificacionEmail.EstadoPendiente, ct);
        if (aProcesar.Count > 0)
        {
            logger.LogInformation(
                "Job recordatorios: {Programados} programados, {Enviados} enviados, {Fallidos} fallidos, {Omitidos} omitidos",
                nuevos.Count, enviados, fallidos, omitidos);
        }

        return new RecordatoriosResultado(nuevos.Count, enviados, fallidos, omitidos, siguenPendientes);
    }

    /// <summary>Crea la fila 'recordatorio' de cada turno confirmado que empieza en las próximas 3 h.</summary>
    private async Task<List<Guid>> ProgramarAsync(DateTimeOffset ahora, CancellationToken ct)
    {
        var limite = ahora + Anticipacion;
        var candidatos = await db.Turnos.AsNoTracking()
            .Where(t => t.EstadoId == EstadoTurno.Confirmado
                && t.Inicio > ahora
                && t.Inicio <= limite
                && t.Salon.MailRecordatorio
                && t.Cliente.EmailRebotadoAt == null
                && !db.NotificacionesEmail.Any(n => n.TurnoId == t.Id && n.Tipo == NotificacionEmail.TipoRecordatorio))
            .OrderBy(t => t.Inicio)
            .Select(t => new { t.Id, t.Inicio, t.ConfirmadoAt, Email = t.Cliente.Email })
            .Take(100)
            .ToListAsync(ct);

        var pendientes = new List<Guid>();
        foreach (var c in candidatos)
        {
            // Confirmado dentro de las 3 h previas: el mail de confirmación ya cumple como recordatorio.
            var confirmadoTarde = c.ConfirmadoAt is { } confirmado && confirmado > c.Inicio - Anticipacion;
            var fila = new NotificacionEmail
            {
                Id = Guid.NewGuid(),
                TurnoId = c.Id,
                Tipo = NotificacionEmail.TipoRecordatorio,
                Destinatario = c.Email.Trim().ToLowerInvariant(),
                Estado = confirmadoTarde ? NotificacionEmail.EstadoOmitido : NotificacionEmail.EstadoPendiente,
                Error = confirmadoTarde ? "Se confirmó con menos de 3 h de anticipación." : null
            };

            db.NotificacionesEmail.Add(fila);
            try
            {
                await db.SaveChangesAsync(ct);
                if (!confirmadoTarde) pendientes.Add(fila.Id);
            }
            catch (DbUpdateException ex) when (ex.Is(PostgresErrors.UniqueViolation))
            {
                // Otra corrida del cron la creó en paralelo.
            }
            finally
            {
                db.Entry(fila).State = EntityState.Detached;
            }
        }

        return pendientes;
    }
}
