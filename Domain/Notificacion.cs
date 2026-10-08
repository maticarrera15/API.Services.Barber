namespace Api.Services.Barber.Domain;

public class Notificacion
{
    public const string TipoTurnoSolicitado = "turno_solicitado";

    public Guid Id { get; set; }
    public Guid SalonId { get; set; }
    public Guid ProfesionalId { get; set; }
    public Guid? TurnoId { get; set; }
    public string Tipo { get; set; } = default!;
    public string Titulo { get; set; } = default!;
    public string? Detalle { get; set; }
    public DateTimeOffset? LeidaAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public Turno? Turno { get; set; }
}
