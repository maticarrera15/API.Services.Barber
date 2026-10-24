using Api.Services.Barber.Common;
using Api.Services.Barber.Data;
using Api.Services.Barber.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Api.Services.Barber.Features.Email;

/// <summary>Crea, valida y envía los mails de turno. Scoped: usa el DbContext del request o del worker.</summary>
public class EmailDispatcher(
    BarberDbContext db,
    ResendEmailSender sender,
    EmailQueue queue,
    IOptions<EmailOptions> options,
    ILogger<EmailDispatcher> logger)
{
    public bool IsConfigured => sender.IsConfigured;

    /// <summary>
    /// Registra el mail (una sola vez por turno y tipo) y lo encola. No falla el request si algo sale mal:
    /// el turno ya quedó guardado y el mail es secundario.
    /// </summary>
    public async Task ProgramarAsync(Turno turno, string tipo, CancellationToken ct)
    {
        if (!sender.IsConfigured) return;

        // Preferencias del salón: si el tipo está apagado, no se crea la fila.
        var salon = turno.Salon ?? await db.Salones.AsNoTracking().FirstOrDefaultAsync(s => s.Id == turno.SalonId, ct);
        if (salon is null) return;
        if (tipo == NotificacionEmail.TipoConfirmacion && !salon.MailConfirmacion) return;
        if (tipo == NotificacionEmail.TipoRecordatorio && !salon.MailRecordatorio) return;

        NotificacionEmail? fila = null;
        try
        {
            var existente = await db.NotificacionesEmail.AsNoTracking()
                .AnyAsync(n => n.TurnoId == turno.Id && n.Tipo == tipo, ct);
            if (existente) return;

            var destinatario = turno.Cliente.Email.Trim().ToLowerInvariant();
            if (destinatario.EndsWith("@navaja.local", StringComparison.OrdinalIgnoreCase)) return;

            fila = new NotificacionEmail
            {
                Id = Guid.NewGuid(),
                TurnoId = turno.Id,
                Tipo = tipo,
                Destinatario = destinatario,
                Estado = NotificacionEmail.EstadoPendiente
            };
            db.NotificacionesEmail.Add(fila);
            await db.SaveChangesAsync(ct);
            queue.Encolar(fila.Id);
        }
        catch (Exception ex)
        {
            // Que la fila fallida no se reintente en el próximo SaveChanges del request.
            if (fila is not null) db.Entry(fila).State = EntityState.Detached;
            // UniqueViolation: otro request la registró en paralelo y ese se encarga del envío.
            if (!ex.Is(PostgresErrors.UniqueViolation))
                logger.LogError(ex, "No se pudo programar el mail {Tipo} del turno {TurnoId}", tipo, turno.Id);
        }
    }

    /// <summary>Envía una fila pendiente. Devuelve el estado en que quedó, o null si no había nada que hacer.</summary>
    public async Task<string?> ProcesarAsync(Guid id, CancellationToken ct)
    {
        if (!sender.IsConfigured) return null;

        var fila = await db.NotificacionesEmail
            .Include(n => n.Turno).ThenInclude(t => t.Cliente)
            .Include(n => n.Turno).ThenInclude(t => t.Servicio)
            .Include(n => n.Turno).ThenInclude(t => t.Profesional)
            .Include(n => n.Turno).ThenInclude(t => t.Salon)
            .FirstOrDefaultAsync(n => n.Id == id, ct);

        if (fila is null || fila.Estado != NotificacionEmail.EstadoPendiente) return null;

        var motivoOmision = MotivoOmision(fila);
        if (motivoOmision is not null)
        {
            fila.Estado = NotificacionEmail.EstadoOmitido;
            fila.Error = motivoOmision;
            await db.SaveChangesAsync(ct);
            return fila.Estado;
        }

        var turno = fila.Turno;
        var esRecordatorio = fila.Tipo == NotificacionEmail.TipoRecordatorio;
        var contenido = esRecordatorio
            ? TurnoEmailTemplates.Recordatorio(turno)
            : TurnoEmailTemplates.Confirmacion(turno);

        var resultado = await sender.EnviarAsync(new EmailMensaje(
            RemitenteNombre: turno.Salon.Nombre,
            Para: fila.Destinatario,
            Asunto: contenido.Asunto,
            Html: contenido.Html,
            Texto: contenido.Texto,
            ResponderA: string.IsNullOrWhiteSpace(turno.Salon.Email) ? null : turno.Salon.Email,
            Adjuntos: esRecordatorio ? [] : [new EmailAdjunto("turno.ics", contenido.Ics)],
            IdempotencyKey: $"notificacion-email-{fila.Id}"), ct);

        fila.Intentos++;
        if (resultado.Ok)
        {
            fila.Estado = NotificacionEmail.EstadoEnviado;
            fila.ProveedorId = resultado.ProveedorId;
            fila.EnviadoAt = DateTimeOffset.UtcNow;
            fila.Error = null;
        }
        else
        {
            fila.Error = resultado.Error;
            if (!resultado.Reintentable || fila.Intentos >= options.Value.MaxIntentos)
                fila.Estado = NotificacionEmail.EstadoFallido;
            logger.LogWarning("Mail {Tipo} del turno {TurnoId} no enviado (intento {Intento}): {Error}",
                fila.Tipo, fila.TurnoId, fila.Intentos, resultado.Error);
        }

        await db.SaveChangesAsync(ct);
        return fila.Estado;
    }

    private static string? MotivoOmision(NotificacionEmail fila)
    {
        var turno = fila.Turno;
        if (turno.EstadoId != EstadoTurno.Confirmado) return "El turno ya no está confirmado.";
        if (turno.Inicio <= DateTimeOffset.UtcNow) return "El turno ya pasó.";
        if (turno.Cliente.EmailRebotadoAt is not null) return "El email del cliente rebotó antes.";
        if (ContactValidation.ValidateEmail(fila.Destinatario) is not null) return "Email inválido.";
        if (fila.Destinatario.EndsWith("@navaja.local", StringComparison.OrdinalIgnoreCase))
            return "Cliente sin email real (reserva del profesional).";
        if (fila.Tipo == NotificacionEmail.TipoConfirmacion && !turno.Salon.MailConfirmacion)
            return "Mail de confirmación desactivado en preferencias.";
        if (fila.Tipo == NotificacionEmail.TipoRecordatorio && !turno.Salon.MailRecordatorio)
            return "Mail de recordatorio desactivado en preferencias.";
        return null;
    }
}
