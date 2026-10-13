using System.Threading.Channels;

namespace Api.Services.Barber.Features.Email;

/// <summary>
/// Cola en memoria de ids de notificacion_email: la API responde sin esperar a Resend.
/// Si el proceso se reinicia, la fila queda 'pendiente' en la tabla y el job de recordatorios la reintenta.
/// </summary>
public class EmailQueue
{
    private readonly Channel<Guid> _channel = Channel.CreateBounded<Guid>(
        new BoundedChannelOptions(500)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        });

    public void Encolar(Guid notificacionEmailId) => _channel.Writer.TryWrite(notificacionEmailId);

    public IAsyncEnumerable<Guid> LeerTodosAsync(CancellationToken ct) => _channel.Reader.ReadAllAsync(ct);
}
