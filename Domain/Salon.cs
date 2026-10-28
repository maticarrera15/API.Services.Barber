namespace Api.Services.Barber.Domain;

public class Salon
{
    public Guid Id { get; set; }
    public string Nombre { get; set; } = default!;
    public string Slug { get; set; } = default!;
    public string? Descripcion { get; set; }
    public string? Direccion { get; set; }
    public string? Ciudad { get; set; }
    public string? Telefono { get; set; }
    public string? Email { get; set; }
    public string Timezone { get; set; } = "America/Argentina/Cordoba";
    public bool Activo { get; set; } = true;
    /// <summary>Mail de confirmación al cliente al pasar el turno a Confirmado.</summary>
    public bool MailConfirmacion { get; set; } = true;
    /// <summary>Mail de recordatorio ~3 h antes del turno.</summary>
    public bool MailRecordatorio { get; set; } = true;
    /// <summary>Incluye el botón de WhatsApp en la plantilla de mail.</summary>
    public bool MailWhatsappBtn { get; set; } = false;
    /// <summary>Ofrecer aviso por WhatsApp al confirmar (y link en la tarjeta).</summary>
    public bool WspConfirmacion { get; set; } = true;
    /// <summary>En agenda, botón para mandar recordatorio por WhatsApp a turnos confirmados.</summary>
    public bool WspRecordatorio { get; set; } = true;
    /// <summary>Plantilla WhatsApp de confirmación. Null = texto por defecto.</summary>
    public string? WspPlantillaConfirmacion { get; set; }
    /// <summary>Plantilla WhatsApp de recordatorio. Null = texto por defecto.</summary>
    public string? WspPlantillaRecordatorio { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public ICollection<Profesional> Profesionales { get; set; } = new List<Profesional>();
    public ICollection<Servicio> Servicios { get; set; } = new List<Servicio>();
}
