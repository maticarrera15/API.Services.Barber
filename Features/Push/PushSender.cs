using System.Net;
using System.Text.Json;
using Api.Services.Barber.Common;
using Api.Services.Barber.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using WebPush;

namespace Api.Services.Barber.Features.Push;

/// <summary>Lo que recibe el service worker (<c>public/sw.js</c>) en el evento push.</summary>
public record PushPayload(string Title, string Body, string Url = "/turnos", string? Tag = null);

public record PushEnvioResultado(int Enviadas, int Eliminadas, int Fallidas);

public class PushSender(HttpClient http, IOptions<PushOptions> options, ILogger<PushSender> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly WebPushClient _client = new(http);
    private readonly PushOptions _options = options.Value;

    public bool IsConfigured => _options.IsConfigured;

    /// <summary>
    /// Envía a todos los dispositivos del profesional. Las suscripciones vencidas (404/410)
    /// se borran: el navegador ya no las va a aceptar nunca más.
    /// </summary>
    public async Task<PushEnvioResultado> EnviarAProfesionalAsync(
        BarberDbContext db,
        Guid profesionalId,
        PushPayload payload,
        CancellationToken ct)
    {
        if (!IsConfigured) return new PushEnvioResultado(0, 0, 0);

        var suscripciones = await db.PushSuscripciones.AsNoTracking()
            .Where(s => s.ProfesionalId == profesionalId)
            .ToListAsync(ct);
        if (suscripciones.Count == 0) return new PushEnvioResultado(0, 0, 0);

        var vapid = new VapidDetails(_options.Subject, _options.VapidPublicKey, _options.VapidPrivateKey);
        var json = JsonSerializer.Serialize(payload, JsonOptions);
        var vencidas = new List<Guid>();
        var enviadas = 0;
        var fallidas = 0;

        foreach (var s in suscripciones)
        {
            try
            {
                await _client.SendNotificationAsync(new PushSubscription(s.Endpoint, s.P256dh, s.Auth), json, vapid, ct);
                enviadas++;
            }
            catch (WebPushException ex) when (ex.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Gone)
            {
                vencidas.Add(s.Id);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                fallidas++;
                logger.LogWarning(ex, "No se pudo enviar push a la suscripción {SuscripcionId}", s.Id);
            }
        }

        if (vencidas.Count > 0)
        {
            await db.PushSuscripciones.Where(s => vencidas.Contains(s.Id)).ExecuteDeleteAsync(ct);
        }

        return new PushEnvioResultado(enviadas, vencidas.Count, fallidas);
    }
}
