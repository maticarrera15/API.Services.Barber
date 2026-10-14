using System.Security.Cryptography;
using System.Text;
using Api.Services.Barber.Common;
using Api.Services.Barber.Features.Email;
using Microsoft.Extensions.Options;

namespace Api.Services.Barber.Features.Jobs;

public static class JobsEndpoints
{
    public static IEndpointRouteBuilder MapJobsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/jobs").WithTags("Jobs");

        group.MapPost("/recordatorios", Recordatorios);

        return app;
    }

    private static async Task<IResult> Recordatorios(
        HttpRequest request,
        IOptions<JobsOptions> options,
        EmailDispatcher dispatcher,
        RecordatoriosJob job,
        CancellationToken ct)
    {
        var denied = Autorizar(request, options.Value);
        if (denied is not null) return denied;

        if (!dispatcher.IsConfigured)
            return Results.Ok(new { habilitado = false, detalle = "Falta Resend:ApiKey: no se envían mails." });

        var resultado = await job.EjecutarAsync(ct);
        return Results.Ok(new { habilitado = true, resultado });
    }

    private static IResult? Autorizar(HttpRequest request, JobsOptions options)
    {
        // Sin secreto configurado el endpoint no existe para afuera.
        if (string.IsNullOrWhiteSpace(options.Secret)) return Results.NotFound();

        var recibido = request.Headers[JobsOptions.SecretHeader].ToString();
        var ok = CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(recibido),
            Encoding.UTF8.GetBytes(options.Secret));
        return ok ? null : HttpErrors.Unauthorized("Secreto inválido.");
    }
}
