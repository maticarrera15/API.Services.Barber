using Api.Services.Barber.Data;
using Api.Services.Barber.Domain;
using Microsoft.EntityFrameworkCore;

namespace Api.Services.Barber.Features.Email;

public class EmailWorker(EmailQueue queue, IServiceScopeFactory scopes, ILogger<EmailWorker> logger) : BackgroundService
{
    private static readonly TimeSpan EsperaReintento = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RecuperarPendientesAsync(stoppingToken);

        await foreach (var id in queue.LeerTodosAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var dispatcher = scope.ServiceProvider.GetRequiredService<EmailDispatcher>();
                var estado = await dispatcher.ProcesarAsync(id, stoppingToken);

                // Sigue pendiente = error temporal con intentos disponibles (el dispatcher lo pasa a fallido al llegar al máximo).
                if (estado == NotificacionEmail.EstadoPendiente) ReencolarMasTarde(id, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error procesando notificacion_email {Id}", id);
            }
        }
    }

    /// <summary>La cola vive en memoria: al arrancar se retoman los mails que quedaron pendientes antes del reinicio.</summary>
    private async Task RecuperarPendientesAsync(CancellationToken ct)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<BarberDbContext>();
            var desde = DateTimeOffset.UtcNow.AddHours(-24);
            var ids = await db.NotificacionesEmail.AsNoTracking()
                .Where(n => n.Estado == NotificacionEmail.EstadoPendiente && n.CreatedAt > desde)
                .OrderBy(n => n.CreatedAt)
                .Select(n => n.Id)
                .Take(100)
                .ToListAsync(ct);
            foreach (var id in ids) queue.Encolar(id);
            if (ids.Count > 0) logger.LogInformation("Retomando {Cantidad} mails pendientes", ids.Count);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "No se pudieron retomar los mails pendientes");
        }
    }

    private void ReencolarMasTarde(Guid id, CancellationToken stoppingToken) =>
        _ = Task.Delay(EsperaReintento, stoppingToken)
            .ContinueWith(t => { if (!t.IsCanceled) queue.Encolar(id); }, TaskScheduler.Default);
}
