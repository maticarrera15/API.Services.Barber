using System.Security.Claims;
using Api.Services.Barber.Common;
using Api.Services.Barber.Data;
using Api.Services.Barber.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Api.Services.Barber.Features.Push;

public static class PushEndpoints
{
    public static IEndpointRouteBuilder MapPushEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/push").WithTags("Push");

        group.MapGet("/clave-publica", ClavePublica).AllowAnonymous();
        group.MapPost("/suscripciones", Suscribir).RequireAuthorization();
        group.MapDelete("/suscripciones", Desuscribir).RequireAuthorization();
        group.MapPost("/prueba", EnviarPrueba).RequireAuthorization();

        return app;
    }

    private static IResult ClavePublica(IOptions<PushOptions> options)
    {
        var o = options.Value;
        return Results.Ok(new PushClavePublicaDto(o.IsConfigured, o.IsConfigured ? o.VapidPublicKey : null));
    }

    /// <summary>Alta idempotente por endpoint: si el dispositivo ya estaba (incluso de otro profesional), se reasigna.</summary>
    private static async Task<IResult> Suscribir(
        GuardarSuscripcionRequest body,
        HttpContext http,
        ClaimsPrincipal user,
        BarberDbContext db,
        CancellationToken ct)
    {
        var denied = CurrentUser.Require(user, out var principal);
        if (denied is not null) return denied;

        if (!Uri.TryCreate(body.Endpoint, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
            return HttpErrors.Validation("Endpoint de suscripción inválido.");
        if (string.IsNullOrWhiteSpace(body.P256dh) || string.IsNullOrWhiteSpace(body.Auth))
            return HttpErrors.Validation("Faltan las claves de la suscripción.");

        var userAgent = http.Request.Headers.UserAgent.ToString();
        if (userAgent.Length > 300) userAgent = userAgent[..300];

        var existente = await db.PushSuscripciones.FirstOrDefaultAsync(s => s.Endpoint == body.Endpoint, ct);
        if (existente is null)
        {
            db.PushSuscripciones.Add(new PushSuscripcion
            {
                Id = Guid.NewGuid(),
                ProfesionalId = principal.ProfesionalId,
                Endpoint = body.Endpoint,
                P256dh = body.P256dh,
                Auth = body.Auth,
                UserAgent = userAgent,
                CreatedAt = DateTimeOffset.UtcNow
            });
        }
        else
        {
            existente.ProfesionalId = principal.ProfesionalId;
            existente.P256dh = body.P256dh;
            existente.Auth = body.Auth;
            existente.UserAgent = userAgent;
        }

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.Is(PostgresErrors.UniqueViolation))
        {
            // Dos altas simultáneas del mismo dispositivo: la otra ya lo guardó.
        }

        return Results.NoContent();
    }

    private static async Task<IResult> Desuscribir(string endpoint, ClaimsPrincipal user, BarberDbContext db, CancellationToken ct)
    {
        var denied = CurrentUser.Require(user, out var principal);
        if (denied is not null) return denied;

        await db.PushSuscripciones
            .Where(s => s.Endpoint == endpoint && s.ProfesionalId == principal.ProfesionalId)
            .ExecuteDeleteAsync(ct);

        return Results.NoContent();
    }

    private static async Task<IResult> EnviarPrueba(ClaimsPrincipal user, BarberDbContext db, PushSender sender, CancellationToken ct)
    {
        var denied = CurrentUser.Require(user, out var principal);
        if (denied is not null) return denied;

        if (!sender.IsConfigured)
            return HttpErrors.Conflict("Las notificaciones push no están configuradas en el servidor.");

        var resultado = await sender.EnviarAProfesionalAsync(
            db,
            principal.ProfesionalId,
            new PushPayload("Notificaciones activadas", "Así te vamos a avisar cuando un cliente saque un turno.", "/turnos", "prueba"),
            ct);

        if (resultado.Enviadas == 0)
            return HttpErrors.Conflict("No hay dispositivos activos para este usuario. Activá las notificaciones de nuevo.");

        return Results.Ok(resultado);
    }
}

public record PushClavePublicaDto(bool Habilitado, string? ClavePublica);

public record GuardarSuscripcionRequest(string Endpoint, string P256dh, string Auth);
