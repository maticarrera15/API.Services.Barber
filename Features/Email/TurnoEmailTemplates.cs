using System.Net;
using System.Text;
using Api.Services.Barber.Common;
using Api.Services.Barber.Domain;

namespace Api.Services.Barber.Features.Email;

public record EmailContenido(string Asunto, string Html, string Texto, byte[] Ics);

/// <summary>
/// Plantillas de mail al cliente. HTML con tablas y estilos inline: es lo único que respetan Gmail y Outlook.
/// Requiere el turno con Cliente, Servicio, Profesional y Salon cargados.
/// </summary>
public static class TurnoEmailTemplates
{
    private const string Primary = "#1a7565";
    private const string Fondo = "#f6f5f0";
    private const string Texto = "#1a211d";
    private const string Suave = "#5f6b64";
    private const string Borde = "#dde3df";

    public static EmailContenido Confirmacion(Turno t)
    {
        var d = Datos.De(t);
        var asunto = $"✅ Turno confirmado · {d.FechaCorta}";
        var intro = $"Tu turno en <b>{E(d.Salon)}</b> quedó confirmado. Te esperamos.";
        var introTexto = $"Tu turno en {d.Salon} quedó confirmado. Te esperamos.";
        const string pie = "Adjuntamos el turno para que lo agregues a tu calendario. Si no podés venir, avisanos con tiempo así liberamos el horario.";
        return Armar(t, d, asunto, "Turno confirmado", intro, introTexto, pie);
    }

    public static EmailContenido Recordatorio(Turno t)
    {
        var d = Datos.De(t);
        // 3 h antes de un turno de madrugada todavía es el día anterior.
        var cuando = SalonTime.ToFecha(t.Inicio) == SalonTime.Today() ? "Hoy" : "Mañana";
        var asunto = $"⏰ {cuando} a las {d.Hora} tenés turno en {d.Salon}";
        var intro = $"Te recordamos que en un rato tenés turno en <b>{E(d.Salon)}</b>.";
        var introTexto = $"Te recordamos que en un rato tenés turno en {d.Salon}.";
        const string pie = "Si no llegás a tiempo o no podés venir, avisanos por WhatsApp así liberamos el horario.";
        return Armar(t, d, asunto, "Recordatorio de turno", intro, introTexto, pie);
    }

    private static EmailContenido Armar(Turno t, Datos d, string asunto, string titulo, string introHtml, string introTexto, string pie)
    {
        var html = Html(d, titulo, introHtml, pie);
        var texto = TextoPlano(d, introTexto, pie);
        return new EmailContenido(asunto, html, texto, Ics(t, d));
    }

    private sealed record Datos(
        string Nombre,
        string Salon,
        string Fecha,
        string FechaCorta,
        string Hora,
        string Servicio,
        string Profesional,
        string Precio,
        string? Direccion,
        string? MapaUrl,
        string? WhatsappUrl,
        string? Telefono)
    {
        public static Datos De(Turno t)
        {
            var direccion = string.Join(", ", new[] { t.Salon.Direccion, t.Salon.Ciudad }.Where(s => !string.IsNullOrWhiteSpace(s)));
            var wa = ContactValidation.ToWhatsappNumber(t.Salon.Telefono);
            return new Datos(
                Nombre: t.Cliente.Nombre.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "",
                Salon: t.Salon.Nombre,
                Fecha: SalonTime.FormatFechaLarga(t.Inicio),
                FechaCorta: SalonTime.FormatCorto(t.Inicio),
                Hora: SalonTime.ToHora(t.Inicio).ToString("HH\\:mm"),
                Servicio: t.Servicio.Nombre,
                Profesional: PersonNames.First(t.Profesional.Nombre),
                Precio: FormatPrecio(t.PrecioCentavos),
                Direccion: direccion.Length > 0 ? direccion : null,
                MapaUrl: direccion.Length > 0
                    ? $"https://www.google.com/maps/search/?api=1&query={Uri.EscapeDataString($"{t.Salon.Nombre}, {direccion}")}"
                    : null,
                WhatsappUrl: t.Salon.MailWhatsappBtn && wa is not null
                    ? $"https://api.whatsapp.com/send?phone={wa}"
                    : null,
                Telefono: t.Salon.MailWhatsappBtn ? t.Salon.Telefono : null);
        }
    }

    private static string Html(Datos d, string titulo, string introHtml, string pie)
    {
        var filas = new StringBuilder();
        Fila(filas, "Día", d.Fecha);
        Fila(filas, "Hora", $"{d.Hora} h");
        Fila(filas, "Servicio", d.Servicio);
        Fila(filas, "Te atiende", d.Profesional);
        Fila(filas, "Precio", d.Precio);
        if (d.Direccion is not null) Fila(filas, "Dirección", d.Direccion);

        var botones = new StringBuilder();
        if (d.MapaUrl is not null) Boton(botones, d.MapaUrl, "Cómo llegar", primario: true);
        if (d.WhatsappUrl is not null) Boton(botones, d.WhatsappUrl, "Escribinos por WhatsApp", primario: false);

        return $$"""
<!doctype html>
<html lang="es">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<meta name="color-scheme" content="light">
<title>{{E(titulo)}}</title>
</head>
<body style="margin:0;padding:0;background:{{Fondo}};font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,Helvetica,Arial,sans-serif;color:{{Texto}};">
<table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="background:{{Fondo}};padding:24px 12px;">
<tr><td align="center">
<table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="max-width:520px;background:#ffffff;border:1px solid {{Borde}};border-radius:20px;overflow:hidden;">
<tr><td style="height:6px;background:{{Primary}};font-size:0;line-height:0;">&nbsp;</td></tr>
<tr><td style="padding:28px 28px 8px;">
<p style="margin:0;font-size:11px;letter-spacing:3px;text-transform:uppercase;color:{{Primary}};font-weight:600;">{{E(titulo)}}</p>
<h1 style="margin:10px 0 0;font-family:Georgia,'Times New Roman',serif;font-size:28px;line-height:1.2;font-weight:normal;">¡Hola{{(d.Nombre.Length > 0 ? " " + E(d.Nombre) : "")}}!</h1>
<p style="margin:12px 0 0;font-size:15px;line-height:1.6;color:{{Suave}};">{{introHtml}}</p>
</td></tr>
<tr><td style="padding:20px 28px 0;">
<table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="background:{{Fondo}};border-radius:14px;">
<tr><td style="padding:20px;">
<p style="margin:0;font-family:Georgia,'Times New Roman',serif;font-size:34px;line-height:1;color:{{Primary}};">{{E(d.Hora)}}</p>
<p style="margin:6px 0 0;font-size:15px;font-weight:600;">{{E(d.Fecha)}}</p>
</td></tr>
</table>
</td></tr>
<tr><td style="padding:16px 28px 0;">
<table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="font-size:14px;">
{{filas}}
</table>
</td></tr>
<tr><td style="padding:24px 28px 4px;">
{{botones}}
</td></tr>
<tr><td style="padding:16px 28px 28px;">
<p style="margin:0;font-size:13px;line-height:1.6;color:{{Suave}};">{{E(pie)}}</p>
</td></tr>
</table>
<p style="margin:16px 0 0;font-size:12px;color:{{Suave}};">{{E(d.Salon)}}{{(d.Direccion is null ? "" : " · " + E(d.Direccion))}}</p>
</td></tr>
</table>
</body>
</html>
""";
    }

    private static void Fila(StringBuilder sb, string label, string valor) =>
        sb.Append($"""
<tr>
<td style="padding:10px 0;border-bottom:1px solid {Borde};color:{Suave};">{E(label)}</td>
<td align="right" style="padding:10px 0;border-bottom:1px solid {Borde};font-weight:600;">{E(valor)}</td>
</tr>
""");

    private static void Boton(StringBuilder sb, string url, string texto, bool primario)
    {
        var estilo = primario
            ? $"background:{Primary};color:#ffffff;border:1px solid {Primary};"
            : $"background:#ffffff;color:{Texto};border:1px solid {Borde};";
        sb.Append($"""
<a href="{E(url)}" target="_blank" style="display:inline-block;margin:0 8px 8px 0;padding:11px 18px;border-radius:999px;font-size:14px;font-weight:600;text-decoration:none;{estilo}">{E(texto)}</a>
""");
    }

    private static string TextoPlano(Datos d, string intro, string pie)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"¡Hola{(d.Nombre.Length > 0 ? " " + d.Nombre : "")}!");
        sb.AppendLine();
        sb.AppendLine(intro);
        sb.AppendLine();
        sb.AppendLine($"Día: {d.Fecha}");
        sb.AppendLine($"Hora: {d.Hora} h");
        sb.AppendLine($"Servicio: {d.Servicio}");
        sb.AppendLine($"Te atiende: {d.Profesional}");
        sb.AppendLine($"Precio: {d.Precio}");
        if (d.Direccion is not null) sb.AppendLine($"Dirección: {d.Direccion}");
        if (d.MapaUrl is not null) sb.AppendLine($"Cómo llegar: {d.MapaUrl}");
        if (d.Telefono is not null) sb.AppendLine($"WhatsApp: {d.Telefono}");
        sb.AppendLine();
        sb.AppendLine(pie);
        return sb.ToString();
    }

    private static byte[] Ics(Turno t, Datos d)
    {
        static string Utc(DateTimeOffset v) => v.UtcDateTime.ToString("yyyyMMdd'T'HHmmss'Z'");
        var lineas = new List<string>
        {
            "BEGIN:VCALENDAR",
            "VERSION:2.0",
            "PRODID:-//Navaja//Turnos//ES",
            "CALSCALE:GREGORIAN",
            "METHOD:PUBLISH",
            "BEGIN:VEVENT",
            $"UID:turno-{t.Id}@navaja",
            $"DTSTAMP:{Utc(DateTimeOffset.UtcNow)}",
            $"DTSTART:{Utc(t.Inicio)}",
            $"DTEND:{Utc(t.Fin)}",
            $"SUMMARY:{IcsTexto($"{d.Servicio} en {d.Salon}")}",
            $"DESCRIPTION:{IcsTexto($"Te atiende {d.Profesional}.")}",
        };
        if (d.Direccion is not null) lineas.Add($"LOCATION:{IcsTexto(d.Direccion)}");
        lineas.AddRange(
        [
            "BEGIN:VALARM",
            "ACTION:DISPLAY",
            "TRIGGER:-PT1H",
            $"DESCRIPTION:{IcsTexto($"Turno en {d.Salon}")}",
            "END:VALARM",
            "END:VEVENT",
            "END:VCALENDAR"
        ]);
        return Encoding.UTF8.GetBytes(string.Join("\r\n", lineas) + "\r\n");
    }

    private static string IcsTexto(string v) =>
        v.Replace("\\", "\\\\").Replace(";", "\\;").Replace(",", "\\,").Replace("\r", "").Replace("\n", "\\n");

    private static string FormatPrecio(int centavos)
    {
        var pesos = centavos / 100m;
        return "$ " + pesos.ToString("#,0", System.Globalization.CultureInfo.InvariantCulture).Replace(",", ".");
    }

    private static string E(string v) => WebUtility.HtmlEncode(v);
}
