namespace Api.Services.Barber.Domain;

public class EstadoTurno
{
    public const short Pendiente = 1;
    public const short Confirmado = 2;
    public const short Cancelado = 3;
    public const short Completado = 4;
    public const short NoAsistio = 5;

    public short Id { get; set; }
    public string Codigo { get; set; } = default!;
    public string Nombre { get; set; } = default!;
    public bool EsFinal { get; set; }
    public bool BloqueaSlot { get; set; }
}
