using System.Text.RegularExpressions;

namespace Api.Services.Barber.Common;

public static class ContactValidation
{
    private static readonly Regex EmailRegex = new(
        @"^[^@\s]+@[^@\s]+\.[^@\s]{2,}$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static IResult? ValidateCliente(string? nombre, string? email, string? telefono, bool telefonoObligatorio = true)
    {
        if (string.IsNullOrWhiteSpace(nombre))
            return HttpErrors.Validation("El nombre del cliente es obligatorio.");

        var emailError = ValidateEmail(email);
        if (emailError is not null) return HttpErrors.Validation(emailError);

        var phoneError = ValidatePhone(telefono, required: telefonoObligatorio);
        if (phoneError is not null) return HttpErrors.Validation(phoneError);

        return null;
    }

    public static string? ValidateEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email)) return "El email del cliente es obligatorio.";
        var trimmed = email.Trim();
        if (trimmed.Length > 120) return "El email es demasiado largo.";
        if (!EmailRegex.IsMatch(trimmed)) return "Ingresá un email válido (debe incluir @ y un dominio).";
        return null;
    }

    /// <summary>
    /// AR: 10 dígitos locales, o +54 / 54 9…. Internacional: 8–15 dígitos (E.164).
    /// </summary>
    public static string? ValidatePhone(string? telefono, bool required = true)
    {
        if (string.IsNullOrWhiteSpace(telefono))
            return required ? "El teléfono del cliente es obligatorio." : null;

        var raw = telefono.Trim();
        if (raw is "0000000000" or "0")
            return "Ingresá un teléfono real.";

        var hasPlus = raw.StartsWith('+');
        var digits = DigitsOnly(raw);
        if (digits.Length < 8) return "El teléfono es demasiado corto.";
        if (digits.Length > 15) return "El teléfono es demasiado largo.";

        if (LooksArgentine(digits, hasPlus))
        {
            var national = NormalizeArgentineNational(digits);
            if (national.Length is < 10 or > 11)
                return "Teléfono argentino inválido. Ej: 3515551234 o +54 9 351 555-1234.";
            return null;
        }

        // Otros países: aceptar longitud E.164.
        if (digits.Length is >= 8 and <= 15) return null;
        return "Ingresá un teléfono válido (Argentina o con código de país, ej. +1…).";
    }

    public static string DigitsOnly(string value) =>
        string.Concat(value.Where(char.IsDigit));

    private static bool LooksArgentine(string digits, bool hasPlus)
    {
        if (digits.StartsWith("54", StringComparison.Ordinal)) return true;
        if (hasPlus) return false;
        return digits.Length is 10 or 11 || digits.StartsWith("15", StringComparison.Ordinal);
    }

    private static string NormalizeArgentineNational(string digits)
    {
        var national = digits;
        if (national.StartsWith("54", StringComparison.Ordinal))
            national = national[2..];
        if (national.StartsWith('9') && national.Length >= 11)
            national = national[1..];
        if (national.StartsWith('0') && national.Length == 11)
            national = national[1..];
        return national;
    }
}
