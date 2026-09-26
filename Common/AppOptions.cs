namespace Api.Services.Barber.Common;

public class AppOptions
{
    public const string SectionName = "Barber";

    public Guid DefaultSalonId { get; set; } = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public Guid DefaultProfesionalId { get; set; } = Guid.Parse("22222222-2222-2222-2222-222222222222");
}
