namespace Api.Services.Barber.Common;

public static class PersonNames
{
    /// <summary>Primer nombre (antes del primer espacio). Vacío si no hay texto.</summary>
    public static string First(string? full)
    {
        if (string.IsNullOrWhiteSpace(full)) return "";
        return full.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];
    }
}
