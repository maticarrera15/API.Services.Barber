namespace Api.Services.Barber.Common;

/// <summary>
/// Endpoints que llama un cron externo (cron-job.org). Sin <see cref="Secret"/> quedan deshabilitados.
/// En Render: Jobs__Secret (un valor largo y aleatorio).
/// </summary>
public class JobsOptions
{
    public const string SectionName = "Jobs";
    public const string SecretHeader = "X-Job-Secret";

    public string? Secret { get; set; }
}
