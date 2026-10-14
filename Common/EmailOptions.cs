namespace Api.Services.Barber.Common;

/// <summary>
/// Mails al cliente vía Resend. Sin <see cref="ResendApiKey"/> no se envía nada.
/// En Render: Resend__ApiKey y Email__FromAddress.
/// </summary>
public class EmailOptions
{
    public const string SectionName = "Email";

    /// <summary>
    /// Sin dominio verificado en Resend solo funciona onboarding@resend.dev,
    /// y solo entrega al email dueño de la cuenta de Resend.
    /// </summary>
    public string FromAddress { get; set; } = "onboarding@resend.dev";

    public int MaxIntentos { get; set; } = 3;
}

public class ResendOptions
{
    public const string SectionName = "Resend";

    public string? ApiKey { get; set; }

    /// <summary>Signing secret del webhook (whsec_…). Sin él, el endpoint de webhooks queda deshabilitado.</summary>
    public string? WebhookSecret { get; set; }
}
