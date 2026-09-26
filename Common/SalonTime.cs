namespace Api.Services.Barber.Common;

public static class SalonTime
{
    public static TimeZoneInfo Zone { get; } = Resolve();

    /// <summary>
    /// Convierte fecha+hora local del salón a instante UTC (Npgsql exige Offset=0 en timestamptz).
    /// </summary>
    public static DateTimeOffset ToInstant(DateOnly fecha, TimeOnly hora)
    {
        var local = fecha.ToDateTime(hora, DateTimeKind.Unspecified);
        var withOffset = new DateTimeOffset(local, Zone.GetUtcOffset(local));
        return withOffset.ToUniversalTime();
    }

    public static DateOnly ToFecha(DateTimeOffset instant) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, Zone).DateTime);

    public static TimeOnly ToHora(DateTimeOffset instant) =>
        TimeOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, Zone).DateTime);

    public static DateOnly Today() =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, Zone).DateTime);

    /// <summary>Semana domingo–sábado que contiene <paramref name="fecha"/>.</summary>
    public static (DateOnly Desde, DateOnly Hasta) Semana(DateOnly fecha)
    {
        var dow = (int)fecha.DayOfWeek;
        var desde = fecha.AddDays(-dow);
        return (desde, desde.AddDays(6));
    }

    private static TimeZoneInfo Resolve()
    {
        foreach (var id in new[] { "America/Argentina/Cordoba", "Argentina Standard Time" })
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(id);
            }
            catch (TimeZoneNotFoundException)
            {
            }
        }

        return TimeZoneInfo.Local;
    }
}
