namespace Api.Services.Barber.Common;

public static class PasswordPolicy
{
    public const int MinLength = 8;
    // BCrypt solo usa los primeros 72 bytes.
    public const int MaxLength = 64;

    public static string? Validate(string? password)
    {
        if (string.IsNullOrEmpty(password) || password.Length < MinLength)
            return $"La contraseña debe tener al menos {MinLength} caracteres.";
        if (password.Length > MaxLength)
            return $"La contraseña no puede superar los {MaxLength} caracteres.";
        if (!password.Any(char.IsLetter) || !password.Any(char.IsDigit))
            return "La contraseña debe combinar letras y números.";
        if (password.Any(char.IsWhiteSpace))
            return "La contraseña no puede tener espacios.";
        return null;
    }
}
