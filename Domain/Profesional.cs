namespace Api.Services.Barber.Domain;

public class Profesional
{
    public Guid Id { get; set; }
    public Guid SalonId { get; set; }
    public string Nombre { get; set; } = default!;
    public string? Rol { get; set; }
    public string? Email { get; set; }
    public string? Telefono { get; set; }
    public string? Iniciales { get; set; }
    public bool Activo { get; set; } = true;
    public Guid? AuthUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public Salon Salon { get; set; } = default!;
}
