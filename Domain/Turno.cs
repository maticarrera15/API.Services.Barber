namespace Api.Services.Barber.Domain;

public class Turno
{
    public Guid Id { get; set; }
    public Guid SalonId { get; set; }
    public Guid ClienteId { get; set; }
    public Guid ProfesionalId { get; set; }
    public Guid ServicioId { get; set; }
    public short EstadoId { get; set; }
    public DateTimeOffset Inicio { get; set; }
    public DateTimeOffset Fin { get; set; }
    public int PrecioCentavos { get; set; }
    public int DuracionMin { get; set; }
    public string? Notas { get; set; }
    public OrigenTurno Origen { get; set; } = OrigenTurno.web;
    public DateTimeOffset? ConfirmadoAt { get; set; }
    public DateTimeOffset? CanceladoAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public Salon Salon { get; set; } = default!;
    public Cliente Cliente { get; set; } = default!;
    public Profesional Profesional { get; set; } = default!;
    public Servicio Servicio { get; set; } = default!;
    public EstadoTurno Estado { get; set; } = default!;
}
