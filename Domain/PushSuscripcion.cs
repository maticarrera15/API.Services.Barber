namespace Api.Services.Barber.Domain;

public class PushSuscripcion
{
    public Guid Id { get; set; }
    public Guid ProfesionalId { get; set; }
    public string Endpoint { get; set; } = default!;
    public string P256dh { get; set; } = default!;
    public string Auth { get; set; } = default!;
    public string? UserAgent { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
