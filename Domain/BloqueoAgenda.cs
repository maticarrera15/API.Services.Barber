namespace Api.Services.Barber.Domain;

public class BloqueoAgenda
{
    public Guid Id { get; set; }
    public Guid SalonId { get; set; }
    public Guid? ProfesionalId { get; set; }
    public DateTimeOffset Inicio { get; set; }
    public DateTimeOffset Fin { get; set; }
    public string? Motivo { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
