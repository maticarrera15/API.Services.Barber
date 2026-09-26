namespace Api.Services.Barber.Domain;

public class HorarioLaboral
{
    public Guid Id { get; set; }
    public Guid SalonId { get; set; }
    public Guid? ProfesionalId { get; set; }
    public short DiaSemana { get; set; }
    public bool Activo { get; set; } = true;
    public TimeOnly HoraApertura { get; set; }
    public TimeOnly HoraCierre { get; set; }
    public TimeOnly? PausaInicio { get; set; }
    public TimeOnly? PausaFin { get; set; }
    public int SlotMinutos { get; set; } = 45;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public Salon Salon { get; set; } = default!;
    public Profesional? Profesional { get; set; }
}
