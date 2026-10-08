using Api.Services.Barber.Data;

namespace Api.Services.Barber.Features.Push;

public class PushWorker(PushQueue queue, IServiceScopeFactory scopes, ILogger<PushWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var trabajo in queue.LeerTodosAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var sender = scope.ServiceProvider.GetRequiredService<PushSender>();
                if (!sender.IsConfigured) continue;

                var db = scope.ServiceProvider.GetRequiredService<BarberDbContext>();
                var resultado = await sender.EnviarAProfesionalAsync(db, trabajo.ProfesionalId, trabajo.Payload, stoppingToken);

                if (resultado.Fallidas > 0 || resultado.Eliminadas > 0)
                {
                    logger.LogInformation(
                        "Push a {ProfesionalId}: {Enviadas} enviadas, {Eliminadas} vencidas, {Fallidas} fallidas",
                        trabajo.ProfesionalId, resultado.Enviadas, resultado.Eliminadas, resultado.Fallidas);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error enviando push a {ProfesionalId}", trabajo.ProfesionalId);
            }
        }
    }
}
