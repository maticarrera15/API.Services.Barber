using System.Threading.Channels;

namespace Api.Services.Barber.Features.Push;

public record PushTrabajo(Guid ProfesionalId, PushPayload Payload);

/// <summary>
/// Cola en memoria: la reserva responde sin esperar a los servicios de push.
/// Si el proceso se reinicia se pierden los pendientes, pero la notificación ya quedó en la tabla
/// y la campanita la muestra igual.
/// </summary>
public class PushQueue
{
    private readonly Channel<PushTrabajo> _channel = Channel.CreateBounded<PushTrabajo>(
        new BoundedChannelOptions(500)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        });

    public void Encolar(PushTrabajo trabajo) => _channel.Writer.TryWrite(trabajo);

    public IAsyncEnumerable<PushTrabajo> LeerTodosAsync(CancellationToken ct) => _channel.Reader.ReadAllAsync(ct);
}
