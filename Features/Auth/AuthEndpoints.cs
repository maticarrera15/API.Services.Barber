using System.Security.Claims;
using System.Text.RegularExpressions;
using Api.Services.Barber.Common;
using Api.Services.Barber.Data;
using Api.Services.Barber.Domain;
using Microsoft.EntityFrameworkCore;

namespace Api.Services.Barber.Features.Auth;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth").WithTags("Auth");

        group.MapPost("/register", Registrar);
        group.MapPost("/login", Login);
        group.MapGet("/me", Me).RequireAuthorization();

        return app;
    }

    private static async Task<IResult> Registrar(
        RegistrarRequest body,
        BarberDbContext db,
        JwtTokenService tokens,
        CancellationToken ct)
    {
        var email = body.Email?.Trim().ToLowerInvariant() ?? "";
        var password = body.Password ?? "";
        var nombre = body.Nombre?.Trim() ?? "";

        if (string.IsNullOrWhiteSpace(nombre)) return HttpErrors.Validation("El nombre es obligatorio.");
        if (!email.Contains('@')) return HttpErrors.Validation("El email no es válido.");
        if (password.Length < 8) return HttpErrors.Validation("La contraseña debe tener al menos 8 caracteres.");

        if (await db.Usuarios.AnyAsync(u => u.Email == email, ct))
            return HttpErrors.Conflict("Ya existe un usuario con ese email.");

        var profesional = await db.Profesionales.FirstOrDefaultAsync(p => p.Email == email, ct);
        if (profesional is null)
        {
            var salonNombre = string.IsNullOrWhiteSpace(body.NombreSalon) ? $"{nombre} Barber" : body.NombreSalon.Trim();
            var salon = new Domain.Salon
            {
                Id = Guid.NewGuid(),
                Nombre = salonNombre,
                Slug = Slugify(salonNombre),
                Ciudad = body.Ciudad?.Trim(),
                Telefono = body.Telefono?.Trim(),
                Email = email
            };

            profesional = new Profesional
            {
                Id = Guid.NewGuid(),
                SalonId = salon.Id,
                Salon = salon,
                Nombre = nombre,
                Rol = "Barber",
                Email = email,
                Telefono = body.Telefono?.Trim(),
                Iniciales = Iniciales(nombre)
            };

            db.Salones.Add(salon);
            db.Profesionales.Add(profesional);
            AgregarHorarioDefault(db, salon.Id);
        }
        else if (!profesional.Activo)
        {
            return HttpErrors.Validation("Ese profesional no está activo.");
        }

        var usuario = new Usuario
        {
            Id = Guid.NewGuid(),
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            ProfesionalId = profesional.Id,
            Rol = "profesional",
            Activo = true
        };

        db.Usuarios.Add(usuario);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.Is(PostgresErrors.UniqueViolation))
        {
            return HttpErrors.Conflict("Ese email o profesional ya tiene usuario.");
        }

        var (token, expires) = tokens.Create(usuario, profesional.SalonId);
        return Results.Created("/api/auth/me", new AuthResponse(token, expires, usuario.ToMe(profesional)));
    }

    private static async Task<IResult> Login(
        LoginRequest body,
        BarberDbContext db,
        JwtTokenService tokens,
        CancellationToken ct)
    {
        var email = body.Email?.Trim().ToLowerInvariant() ?? "";
        var usuario = await db.Usuarios
            .Include(u => u.Profesional)
            .FirstOrDefaultAsync(u => u.Email == email, ct);

        if (usuario is null || !usuario.Activo || !usuario.Profesional.Activo)
            return HttpErrors.Unauthorized("Email o contraseña incorrectos.");

        if (!BCrypt.Net.BCrypt.Verify(body.Password ?? "", usuario.PasswordHash))
            return HttpErrors.Unauthorized("Email o contraseña incorrectos.");

        var (token, expires) = tokens.Create(usuario, usuario.Profesional.SalonId);
        return Results.Ok(new AuthResponse(token, expires, usuario.ToMe(usuario.Profesional)));
    }

    private static async Task<IResult> Me(ClaimsPrincipal user, BarberDbContext db, CancellationToken ct)
    {
        var denied = CurrentUser.Require(user, out var principal);
        if (denied is not null) return denied;

        var usuario = await db.Usuarios.AsNoTracking()
            .Include(u => u.Profesional)
            .FirstOrDefaultAsync(u => u.Id == principal.UsuarioId && u.Activo, ct);

        return usuario is null
            ? HttpErrors.Unauthorized("Sesión inválida.")
            : Results.Ok(usuario.ToMe(usuario.Profesional));
    }

    private static void AgregarHorarioDefault(BarberDbContext db, Guid salonId)
    {
        for (short d = 0; d <= 6; d++)
        {
            db.HorariosLaborales.Add(new HorarioLaboral
            {
                Id = Guid.NewGuid(),
                SalonId = salonId,
                DiaSemana = d,
                Activo = d is >= 2 and <= 6,
                HoraApertura = new TimeOnly(9, 0),
                HoraCierre = new TimeOnly(20, 0),
                PausaInicio = new TimeOnly(13, 0),
                PausaFin = new TimeOnly(15, 0),
                SlotMinutos = 45
            });
        }
    }

    private static string Slugify(string value)
    {
        var n = value.Trim().ToLowerInvariant();
        n = n.Replace("á", "a").Replace("é", "e").Replace("í", "i").Replace("ó", "o").Replace("ú", "u").Replace("ñ", "n");
        n = Regex.Replace(n, @"[^a-z0-9]+", "-").Trim('-');
        return string.IsNullOrEmpty(n) ? $"salon-{Guid.NewGuid():N}"[..16] : n;
    }

    private static string Iniciales(string nombre)
    {
        var parts = nombre.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return "PR";
        if (parts.Length == 1) return parts[0][..Math.Min(2, parts[0].Length)].ToUpperInvariant();
        return $"{char.ToUpperInvariant(parts[0][0])}{char.ToUpperInvariant(parts[^1][0])}";
    }
}

public record RegistrarRequest(
    string Nombre,
    string Email,
    string Password,
    string? NombreSalon,
    string? Ciudad,
    string? Telefono);

public record LoginRequest(string Email, string Password);

public record AuthResponse(string Token, DateTimeOffset ExpiresAt, MeDto Usuario);

public record MeDto(
    Guid UsuarioId,
    Guid ProfesionalId,
    Guid SalonId,
    string Email,
    string Nombre,
    string Rol);

file static class AuthMapping
{
    public static MeDto ToMe(this Usuario u, Profesional p) =>
        new(u.Id, p.Id, p.SalonId, u.Email, p.Nombre, u.Rol);
}
