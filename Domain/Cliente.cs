namespace Api.Services.Barber.Domain;

public class Cliente
{
    public Guid Id { get; set; }
    public string Nombre { get; set; } = default!;
    public string Email { get; set; } = default!;
    public string? Telefono { get; set; }
    public Guid? AuthUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
