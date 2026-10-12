using System.Security.Claims;
using Api.Services.Barber.Common;
using Api.Services.Barber.Data;
using Api.Services.Barber.Domain;
using Microsoft.EntityFrameworkCore;

namespace Api.Services.Barber.Features.Auth;

public static class ClienteAuthEndpoints
{
    public const string RateLimitPolicy = "auth";

    // Hash de relleno: el login tarda lo mismo exista o no la cuenta (no revela emails registrados).
    private static readonly Lazy<string> DummyHash = new(() => BCrypt.Net.BCrypt.HashPassword("cuenta-inexistente-0"));

    public static IEndpointRouteBuilder MapClienteAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth/cliente").WithTags("Auth cliente");

        group.MapPost("/registro", Registrar).RequireRateLimiting(RateLimitPolicy);
        group.MapPost("/login", Login).RequireRateLimiting(RateLimitPolicy);
        group.MapGet("/me", Me).RequireAuthorization();

        return app;
    }

    private static async Task<IResult> Registrar(
        RegistrarClienteRequest body,
        BarberDbContext db,
        JwtTokenService tokens,
        CancellationToken ct)
    {
        var invalido = ContactValidation.ValidateCliente(body.Nombre, body.Email, body.Telefono);
        if (invalido is not null) return invalido;

        var passwordError = PasswordPolicy.Validate(body.Password);
        if (passwordError is not null) return HttpErrors.Validation(passwordError);
        if (body.Password != body.ConfirmarPassword) return HttpErrors.Validation("Las contraseñas no coinciden.");

        var email = body.Email.Trim().ToLowerInvariant();
        if (await db.ClienteCuentas.AnyAsync(c => c.Email == email, ct))
            return HttpErrors.Conflict("Ya existe una cuenta con ese email. Probá iniciar sesión.");
        if (await db.Usuarios.AnyAsync(u => u.Email == email, ct))
            return HttpErrors.Conflict("Ese email pertenece a un profesional. Usá otro email para tu cuenta de cliente.");

        var cliente = await db.Clientes.FirstOrDefaultAsync(c => c.Email == email, ct);
        if (cliente is null)
        {
            cliente = new Cliente { Id = Guid.NewGuid(), Email = email };
            db.Clientes.Add(cliente);
        }
        cliente.Nombre = body.Nombre.Trim();
        cliente.Telefono = body.Telefono?.Trim();

        var cuenta = new ClienteCuenta
        {
            Id = Guid.NewGuid(),
            ClienteId = cliente.Id,
            Cliente = cliente,
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(body.Password),
            UltimoLoginAt = DateTimeOffset.UtcNow
        };
        db.ClienteCuentas.Add(cuenta);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.Is(PostgresErrors.UniqueViolation))
        {
            return HttpErrors.Conflict("Ya existe una cuenta con ese email. Probá iniciar sesión.");
        }

        var (token, expires) = tokens.CreateCliente(cuenta);
        return Results.Created("/api/auth/cliente/me", new ClienteAuthResponse(token, expires, cuenta.ToMe()));
    }

    private static async Task<IResult> Login(
        LoginRequest body,
        BarberDbContext db,
        JwtTokenService tokens,
        CancellationToken ct)
    {
        var email = body.Email?.Trim().ToLowerInvariant() ?? "";
        var cuenta = await db.ClienteCuentas
            .Include(c => c.Cliente)
            .FirstOrDefaultAsync(c => c.Email == email, ct);

        var hash = cuenta?.PasswordHash ?? DummyHash.Value;
        var passwordOk = BCrypt.Net.BCrypt.Verify(body.Password ?? "", hash);
        if (cuenta is null || cuenta.PasswordHash is null || !cuenta.Activo || !passwordOk)
            return HttpErrors.Unauthorized("Email o contraseña incorrectos.");

        cuenta.UltimoLoginAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);

        var (token, expires) = tokens.CreateCliente(cuenta);
        return Results.Ok(new ClienteAuthResponse(token, expires, cuenta.ToMe()));
    }

    private static async Task<IResult> Me(ClaimsPrincipal user, BarberDbContext db, CancellationToken ct)
    {
        var denied = CurrentCliente.Require(user, out var principal);
        if (denied is not null) return denied;

        var cuenta = await db.ClienteCuentas.AsNoTracking()
            .Include(c => c.Cliente)
            .FirstOrDefaultAsync(c => c.Id == principal.CuentaId && c.Activo, ct);

        return cuenta is null
            ? HttpErrors.Unauthorized("Sesión inválida.")
            : Results.Ok(cuenta.ToMe());
    }
}

public record RegistrarClienteRequest(
    string Email,
    string Password,
    string ConfirmarPassword,
    string Nombre,
    string? Telefono);

public record ClienteAuthResponse(string Token, DateTimeOffset ExpiresAt, ClienteMeDto Cliente);

public record ClienteMeDto(
    Guid CuentaId,
    Guid ClienteId,
    string Email,
    string Nombre,
    string? Telefono,
    bool EmailVerificado);

internal static class ClienteAuthMapping
{
    public static ClienteMeDto ToMe(this ClienteCuenta c) =>
        new(c.Id, c.ClienteId, c.Email, c.Cliente.Nombre, c.Cliente.Telefono, c.EmailVerificadoAt is not null);
}
