using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Api.Services.Barber.Common;
using Microsoft.Extensions.Options;

namespace Api.Services.Barber.Features.Email;

public record EmailAdjunto(string Nombre, byte[] Contenido);

public record EmailMensaje(
    string RemitenteNombre,
    string Para,
    string Asunto,
    string Html,
    string Texto,
    string? ResponderA,
    IReadOnlyList<EmailAdjunto> Adjuntos,
    string IdempotencyKey);

public record EmailResultado(bool Ok, string? ProveedorId, string? Error, bool Reintentable);

/// <summary>Envío por la API HTTP de Resend (no SMTP: Render restringe esos puertos).</summary>
public class ResendEmailSender(HttpClient http, IOptions<ResendOptions> resend, IOptions<EmailOptions> email)
{
    private static readonly JsonSerializerOptions Json = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public bool IsConfigured => !string.IsNullOrWhiteSpace(resend.Value.ApiKey);

    public async Task<EmailResultado> EnviarAsync(EmailMensaje mensaje, CancellationToken ct)
    {
        if (!IsConfigured) return new EmailResultado(false, null, "Resend no está configurado.", false);

        var remitente = $"{SinComillas(mensaje.RemitenteNombre)} <{email.Value.FromAddress}>";
        var payload = new
        {
            from = remitente,
            to = new[] { mensaje.Para },
            subject = mensaje.Asunto,
            html = mensaje.Html,
            text = mensaje.Texto,
            reply_to = mensaje.ResponderA,
            attachments = mensaje.Adjuntos.Count == 0
                ? null
                : mensaje.Adjuntos.Select(a => new { filename = a.Nombre, content = Convert.ToBase64String(a.Contenido) }).ToArray()
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.resend.com/emails")
        {
            Content = JsonContent.Create(payload, options: Json)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", resend.Value.ApiKey);
        request.Headers.TryAddWithoutValidation("Idempotency-Key", mensaje.IdempotencyKey);

        try
        {
            using var response = await http.SendAsync(request, ct);
            var body = await response.Content.ReadAsStringAsync(ct);

            if (response.IsSuccessStatusCode)
            {
                return new EmailResultado(true, LeerCampo(body, "id"), null, false);
            }

            var detalle = LeerCampo(body, "message") ?? body;
            var reintentable = response.StatusCode == HttpStatusCode.TooManyRequests || (int)response.StatusCode >= 500;
            return new EmailResultado(false, null, $"{(int)response.StatusCode}: {Recortar(detalle)}", reintentable);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            return new EmailResultado(false, null, Recortar(ex.Message), true);
        }
    }

    private static string? LeerCampo(string json, string campo)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.TryGetProperty(campo, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string SinComillas(string nombre) => nombre.Replace("\"", "").Replace("<", "").Replace(">", "").Trim();

    private static string Recortar(string texto) => texto.Length <= 500 ? texto : texto[..500];
}
