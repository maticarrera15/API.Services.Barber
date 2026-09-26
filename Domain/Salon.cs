namespace Api.Services.Barber.Domain;

public class Salon
{
    public Guid Id { get; set; }
    public string Nombre { get; set; } = default!;
    public string Slug { get; set; } = default!;
    public string? Direccion { get; set; }
    public string? Ciudad { get; set; }
    public string? Telefono { get; set; }
    public string? Email { get; set; }
    public string Timezone { get; set; } = "America/Argentina/Cordoba";
    public bool Activo { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public ICollection<Profesional> Profesionales { get; set; } = new List<Profesional>();
    public ICollection<Servicio> Servicios { get; set; } = new List<Servicio>();
}
