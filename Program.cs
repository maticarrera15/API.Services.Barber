using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Api.Services.Barber.Common;
using Api.Services.Barber.Data;
using Api.Services.Barber.Domain;
using Api.Services.Barber.Features.Agenda;
using Api.Services.Barber.Features.Auth;
using Api.Services.Barber.Features.Clientes;
using Api.Services.Barber.Features.Email;
using Api.Services.Barber.Features.Horarios;
using Api.Services.Barber.Features.Jobs;
using Api.Services.Barber.Features.Notificaciones;
using Api.Services.Barber.Features.Profesionales;
using Api.Services.Barber.Features.Push;
using Api.Services.Barber.Features.Salon;
using Api.Services.Barber.Features.Servicios;
using Api.Services.Barber.Features.Turnos;
using Api.Services.Barber.Features.TurnosFijos;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<AppOptions>(builder.Configuration.GetSection(AppOptions.SectionName));
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.AddSingleton<JwtTokenService>();
builder.Services.Configure<PushOptions>(builder.Configuration.GetSection(PushOptions.SectionName));
builder.Services.AddHttpClient<PushSender>(c => c.Timeout = TimeSpan.FromSeconds(10))
    .ConfigurePrimaryHttpMessageHandler(OutboundHttp.CreateHandler);
builder.Services.AddSingleton<PushQueue>();
builder.Services.AddHostedService<PushWorker>();
builder.Services.Configure<EmailOptions>(builder.Configuration.GetSection(EmailOptions.SectionName));
builder.Services.Configure<ResendOptions>(builder.Configuration.GetSection(ResendOptions.SectionName));
builder.Services.AddHttpClient<ResendEmailSender>(c => c.Timeout = TimeSpan.FromSeconds(15))
    .ConfigurePrimaryHttpMessageHandler(OutboundHttp.CreateHandler);
builder.Services.AddSingleton<EmailQueue>();
builder.Services.AddScoped<EmailDispatcher>();
builder.Services.AddHostedService<EmailWorker>();
builder.Services.AddScoped<RecordatoriosJob>();
builder.Services.Configure<JobsOptions>(builder.Configuration.GetSection(JobsOptions.SectionName));

builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
    ?? throw new InvalidOperationException("Falta la sección Jwt en appsettings.");
if (string.IsNullOrWhiteSpace(jwt.Key) || jwt.Key.Length < 32)
    throw new InvalidOperationException("Jwt:Key debe tener al menos 32 caracteres.");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
            ClockSkew = TimeSpan.FromMinutes(1)
        };
        // Token inválido/expirado no debe tumbar endpoints públicos (agenda, servicios, reservar).
        options.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = context =>
            {
                context.NoResult();
                return Task.CompletedTask;
            }
        };
    });
builder.Services.AddAuthorization();

// Render (y la mayoría de los hosts) ponen un proxy adelante: la IP real viene en X-Forwarded-For.
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor;
    o.ForwardLimit = 1;
    o.KnownNetworks.Clear();
    o.KnownProxies.Clear();
});

builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy(ClienteAuthEndpoints.RateLimitPolicy, context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "desconocida",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
    o.OnRejected = async (context, ct) =>
    {
        context.HttpContext.Response.ContentType = "application/problem+json";
        await context.HttpContext.Response.WriteAsJsonAsync(new
        {
            title = "Demasiados intentos",
            status = StatusCodes.Status429TooManyRequests,
            detail = "Hiciste muchos intentos seguidos. Esperá un minuto y volvé a probar."
        }, ct);
    };
});

builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(o =>
{
    o.SwaggerDoc("v1", new() { Title = "Api.Services.Barber", Version = "v1" });
    o.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Pegá el token. Swagger agrega el prefijo Bearer."
    });
    o.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Falta ConnectionStrings:DefaultConnection.");

var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
dataSourceBuilder.MapEnum<OrigenTurno>("origen_turno");
var dataSource = dataSourceBuilder.Build();

builder.Services.AddDbContext<BarberDbContext>(options =>
{
    options.UseNpgsql(dataSource).UseSnakeCaseNamingConvention();
});

var origins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>()
    ?? ["http://localhost:3000"];

builder.Services.AddCors(options =>
{
    options.AddPolicy("front", policy =>
        policy.WithOrigins(origins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .SetPreflightMaxAge(TimeSpan.FromHours(2)));
});

var app = builder.Build();

app.UseForwardedHeaders();

app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        var feature = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>();
        var ex = feature?.Error;
        var env = context.RequestServices.GetRequiredService<IHostEnvironment>();
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new
        {
            title = "Error interno",
            status = 500,
            detail = env.IsDevelopment() ? ex?.Message : null,
            type = env.IsDevelopment() ? ex?.GetType().FullName : null,
            trace = env.IsDevelopment() ? ex?.StackTrace?.Split('\n').Take(8).ToArray() : null
        });
    });
});

if (app.Environment.IsDevelopment() ||
    app.Environment.IsEnvironment("Staging"))
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseHttpsRedirection();
}

app.UseCors("front");
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }))
    .WithTags("Health");

app.MapAuthEndpoints();
app.MapClienteAuthEndpoints();
app.MapMiCuentaEndpoints();
app.MapSalonEndpoints();
app.MapProfesionalEndpoints();
app.MapServiciosEndpoints();
app.MapHorariosEndpoints();
app.MapTurnosEndpoints();
app.MapTurnosFijosEndpoints();
app.MapAgendaEndpoints();
app.MapNotificacionesEndpoints();
app.MapPushEndpoints();
app.MapJobsEndpoints();
app.MapResendWebhookEndpoints();

app.Run();
