using System.Net;
using System.Net.Sockets;

namespace Api.Services.Barber.Common;

/// <summary>
/// Handler para llamadas salientes (Resend, servicios de push).
/// .NET 8 prueba las IPs de a una y sin límite por IP: en redes con IPv6 roto se cuelga hasta el timeout total.
/// Acá se prueba IPv4 primero y cada IP tiene su propio límite.
/// </summary>
public static class OutboundHttp
{
    private static readonly TimeSpan TimeoutPorIp = TimeSpan.FromSeconds(5);

    public static SocketsHttpHandler CreateHandler() => new()
    {
        PooledConnectionLifetime = TimeSpan.FromMinutes(5),
        ConnectCallback = ConectarAsync
    };

    private static async ValueTask<Stream> ConectarAsync(SocketsHttpConnectionContext context, CancellationToken ct)
    {
        var host = context.DnsEndPoint.Host;
        var direcciones = IPAddress.TryParse(host, out var literal)
            ? [literal]
            : await Dns.GetHostAddressesAsync(host, ct);

        Exception? ultimo = null;
        foreach (var ip in direcciones.OrderBy(a => a.AddressFamily == AddressFamily.InterNetwork ? 0 : 1))
        {
            var socket = new Socket(ip.AddressFamily, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
            using var limite = CancellationTokenSource.CreateLinkedTokenSource(ct);
            limite.CancelAfter(TimeoutPorIp);
            try
            {
                await socket.ConnectAsync(new IPEndPoint(ip, context.DnsEndPoint.Port), limite.Token);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                socket.Dispose();
                ultimo = ex;
            }
        }

        throw new HttpRequestException($"No se pudo conectar a {host}.", ultimo);
    }
}
