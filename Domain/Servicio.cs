namespace Api.Services.Barber.Domain;

public class Servicio
{
    public Guid Id { get; set; }
    public Guid SalonId { get; set; }
    public string Nombre { get; set; } = default!;
    public string? Detalle { get; set; }
    public int DuracionMin { get; set; }
    public int PrecioCentavos { get; set; }
    public bool Activo { get; set; } = true;
    public int Orden { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public Salon Salon { get; set; } = default!;
}
