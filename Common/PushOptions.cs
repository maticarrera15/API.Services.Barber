namespace Api.Services.Barber.Common;

/// <summary>
/// Claves VAPID para Web Push. Generarlas con <c>npx web-push generate-vapid-keys</c>
/// y cargarlas como Push__VapidPublicKey / Push__VapidPrivateKey / Push__Subject.
/// </summary>
public class PushOptions
{
    public const string SectionName = "Push";

    public string? VapidPublicKey { get; set; }
    public string? VapidPrivateKey { get; set; }

    /// <summary><c>mailto:</c> o URL de contacto que los servicios de push usan si hay problemas.</summary>
    public string Subject { get; set; } = "mailto:soporte@navaja.app";

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(VapidPublicKey) && !string.IsNullOrWhiteSpace(VapidPrivateKey);
}
