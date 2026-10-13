namespace Api.Services.Barber.Domain;

public class NotificacionEmail
{
    public const string TipoConfirmacion = "confirmacion";
    public const string TipoRecordatorio = "recordatorio";

    public const string EstadoPendiente = "pendiente";
    public const string EstadoEnviado = "enviado";
    public const string EstadoFallido = "fallido";
    public const string EstadoOmitido = "omitido";

    public Guid Id { get; set; }
    public Guid TurnoId { get; set; }
    public string Tipo { get; set; } = default!;
    public string Destinatario { get; set; } = default!;
    public string Estado { get; set; } = EstadoPendiente;
    public short Intentos { get; set; }
    public string? ProveedorId { get; set; }
    public string? Error { get; set; }
    public DateTimeOffset? EnviadoAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public Turno Turno { get; set; } = default!;
}
