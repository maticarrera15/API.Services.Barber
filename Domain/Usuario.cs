namespace Api.Services.Barber.Domain;

public class Usuario
{
    public Guid Id { get; set; }
    public string Email { get; set; } = default!;
    public string PasswordHash { get; set; } = default!;
    public Guid ProfesionalId { get; set; }
    public string Rol { get; set; } = "profesional";
    public bool Activo { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public Profesional Profesional { get; set; } = default!;
}
