namespace Api.Services.Barber.Domain;

public class ClienteCuenta
{
    public Guid Id { get; set; }
    public Guid ClienteId { get; set; }
    public string Email { get; set; } = default!;
    public string? PasswordHash { get; set; }
    public DateTimeOffset? EmailVerificadoAt { get; set; }
    public bool Activo { get; set; } = true;
    public DateTimeOffset? UltimoLoginAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public Cliente Cliente { get; set; } = default!;
}
