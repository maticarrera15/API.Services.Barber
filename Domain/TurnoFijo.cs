namespace Api.Services.Barber.Domain;

/// <summary>
/// Reserva recurrente semanal (ej. Matías todos los viernes 19:00).
/// Bloquea el slot en la agenda sin materializar turnos puntuales.
/// </summary>
public class TurnoFijo
{
    public Guid Id { get; set; }
    public Guid SalonId { get; set; }
    public Guid ProfesionalId { get; set; }
    public Guid? ServicioId { get; set; }
    public string ClienteNombre { get; set; } = default!;
    public string ClienteEmail { get; set; } = default!;
    public string? ClienteTelefono { get; set; }
    /// <summary>0=domingo … 6=sábado (igual que DateTime.DayOfWeek / horario_laboral).</summary>
    public short DiaSemana { get; set; }
    public TimeOnly Hora { get; set; }
    public int DuracionMin { get; set; } = 45;
    public bool Activo { get; set; } = true;
    public string? Notas { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public Salon Salon { get; set; } = default!;
    public Profesional Profesional { get; set; } = default!;
    public Servicio? Servicio { get; set; }
}
