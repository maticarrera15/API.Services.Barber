using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Api.Services.Barber.Common;
using Api.Services.Barber.Data;
using Api.Services.Barber.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Api.Services.Barber.Features.Email;

/// <summary>
/// Avisos de Resend: rebotes y quejas de spam. Marca el email del cliente para no volver a escribirle,
/// que es lo que protege la reputación del remitente.
/// </summary>
public static class ResendWebhookEndpoints
{
    private static readonly TimeSpan ToleranciaFirma = TimeSpan.FromMinutes(5);

    public static IEndpointRouteBuilder MapResendWebhookEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/webhooks/resend", Recibir).WithTags("Webhooks");
        return app;
    }

    private static async Task<IResult> Recibir(
        HttpRequest request,
        IOptions<ResendOptions> options,
        BarberDbContext db,
        ILoggerFactory loggers,
        CancellationToken ct)
    {
        var secret = options.Value.WebhookSecret;
        if (string.IsNullOrWhiteSpace(secret)) return Results.NotFound();

        using var reader = new StreamReader(request.Body, Encoding.UTF8);
        var body = await reader.ReadToEndAsync(ct);

        if (!FirmaValida(request.Headers, body, secret))
            return HttpErrors.Unauthorized("Firma de webhook inválida.");

        string? tipo, emailId, para, bounceTipo, bounceMensaje;
        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            tipo = Texto(root, "type");
            var data = root.TryGetProperty("data", out var d) ? d : default;
            emailId = Texto(data, "email_id");
            para = data.ValueKind == JsonValueKind.Object
                && data.TryGetProperty("to", out var to)
                && to.ValueKind == JsonValueKind.Array
                && to.GetArrayLength() > 0
                    ? to[0].GetString()
                    : null;
            var bounce = data.ValueKind == JsonValueKind.Object && data.TryGetProperty("bounce", out var b) ? b : default;
            bounceTipo = Texto(bounce, "type");
            bounceMensaje = Texto(bounce, "message");
        }
        catch (JsonException)
        {
            return HttpErrors.Validation("JSON inválido.");
        }

        switch (tipo)
        {
            case "email.bounced":
                // Los rebotes temporales (casilla llena, servidor caído) no bloquean el email.
                var permanente = !string.Equals(bounceTipo, "Transient", StringComparison.OrdinalIgnoreCase);
                await MarcarAsync(db, emailId, para, $"Rebotó{(bounceMensaje is null ? "" : $": {bounceMensaje}")}", permanente, ct);
                break;
            case "email.complained":
                await MarcarAsync(db, emailId, para, "El cliente lo marcó como spam.", bloquearCliente: true, ct);
                break;
        }

        loggers.CreateLogger("ResendWebhook").LogInformation("Webhook Resend {Tipo} para {EmailId}", tipo, emailId);
        return Results.Ok();
    }

    private static async Task MarcarAsync(
        BarberDbContext db, string? emailId, string? para, string motivo, bool bloquearCliente, CancellationToken ct)
    {
        var ahora = DateTimeOffset.UtcNow;
        var fila = emailId is null
            ? null
            : await db.NotificacionesEmail
                .Include(n => n.Turno).ThenInclude(t => t.Cliente)
                .FirstOrDefaultAsync(n => n.ProveedorId == emailId, ct);

        if (fila is not null)
        {
            fila.Estado = NotificacionEmail.EstadoFallido;
            fila.Error = motivo.Length <= 500 ? motivo : motivo[..500];
            if (bloquearCliente) fila.Turno.Cliente.EmailRebotadoAt ??= ahora;
        }
        else if (bloquearCliente && !string.IsNullOrWhiteSpace(para))
        {
            var email = para.Trim().ToLowerInvariant();
            var clientes = await db.Clientes.Where(c => c.Email.ToLower() == email && c.EmailRebotadoAt == null).ToListAsync(ct);
            foreach (var c in clientes) c.EmailRebotadoAt = ahora;
        }

        await db.SaveChangesAsync(ct);
    }

    /// <summary>Resend firma con Svix: HMAC-SHA256 de "id.timestamp.body" con la clave en base64 tras "whsec_".</summary>
    private static bool FirmaValida(IHeaderDictionary headers, string body, string secret)
    {
        var id = headers["svix-id"].ToString();
        var timestamp = headers["svix-timestamp"].ToString();
        var firmas = headers["svix-signature"].ToString();
        if (id.Length == 0 || timestamp.Length == 0 || firmas.Length == 0) return false;

        if (!long.TryParse(timestamp, out var segundos)) return false;
        var enviado = DateTimeOffset.FromUnixTimeSeconds(segundos);
        if ((DateTimeOffset.UtcNow - enviado).Duration() > ToleranciaFirma) return false;

        byte[] clave;
        try
        {
            clave = Convert.FromBase64String(secret.StartsWith("whsec_", StringComparison.Ordinal) ? secret[6..] : secret);
        }
        catch (FormatException)
        {
            return false;
        }

        var esperada = Encoding.UTF8.GetBytes(
            Convert.ToBase64String(HMACSHA256.HashData(clave, Encoding.UTF8.GetBytes($"{id}.{timestamp}.{body}"))));

        foreach (var parte in firmas.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var coma = parte.IndexOf(',');
            if (coma < 0 || parte[..coma] != "v1") continue;
            if (CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(parte[(coma + 1)..]), esperada)) return true;
        }

        return false;
    }

    private static string? Texto(JsonElement el, string campo) =>
        el.ValueKind == JsonValueKind.Object && el.TryGetProperty(campo, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString()
            : null;
}
