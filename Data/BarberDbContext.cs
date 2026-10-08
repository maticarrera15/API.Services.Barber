using Api.Services.Barber.Domain;
using Microsoft.EntityFrameworkCore;

namespace Api.Services.Barber.Data;

public class BarberDbContext(DbContextOptions<BarberDbContext> options) : DbContext(options)
{
    public DbSet<Salon> Salones => Set<Salon>();
    public DbSet<Profesional> Profesionales => Set<Profesional>();
    public DbSet<Cliente> Clientes => Set<Cliente>();
    public DbSet<Servicio> Servicios => Set<Servicio>();
    public DbSet<EstadoTurno> EstadosTurno => Set<EstadoTurno>();
    public DbSet<HorarioLaboral> HorariosLaborales => Set<HorarioLaboral>();
    public DbSet<BloqueoAgenda> BloqueosAgenda => Set<BloqueoAgenda>();
    public DbSet<Turno> Turnos => Set<Turno>();
    public DbSet<TurnoFijo> TurnosFijos => Set<TurnoFijo>();
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Notificacion> Notificaciones => Set<Notificacion>();
    public DbSet<PushSuscripcion> PushSuscripciones => Set<PushSuscripcion>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresEnum<OrigenTurno>("origen_turno");

        modelBuilder.Entity<Salon>(e =>
        {
            e.ToTable("salon");
            e.HasKey(x => x.Id);
        });

        modelBuilder.Entity<Profesional>(e =>
        {
            e.ToTable("profesional");
            e.HasOne(x => x.Salon).WithMany(x => x.Profesionales).HasForeignKey(x => x.SalonId);
        });

        modelBuilder.Entity<Cliente>(e =>
        {
            e.ToTable("cliente");
        });

        modelBuilder.Entity<Servicio>(e =>
        {
            e.ToTable("servicio");
            e.HasOne(x => x.Salon).WithMany(x => x.Servicios).HasForeignKey(x => x.SalonId);
        });

        modelBuilder.Entity<EstadoTurno>(e =>
        {
            e.ToTable("estado_turno");
            e.Property(x => x.Id).ValueGeneratedNever();
        });

        modelBuilder.Entity<HorarioLaboral>(e =>
        {
            e.ToTable("horario_laboral");
            e.HasOne(x => x.Salon).WithMany().HasForeignKey(x => x.SalonId);
            e.HasOne(x => x.Profesional).WithMany().HasForeignKey(x => x.ProfesionalId);
        });

        modelBuilder.Entity<BloqueoAgenda>(e =>
        {
            e.ToTable("bloqueo_agenda");
        });

        modelBuilder.Entity<Turno>(e =>
        {
            e.ToTable("turno");
            e.Property(x => x.Origen).HasColumnType("origen_turno");
            e.HasOne(x => x.Salon).WithMany().HasForeignKey(x => x.SalonId);
            e.HasOne(x => x.Cliente).WithMany().HasForeignKey(x => x.ClienteId);
            e.HasOne(x => x.Profesional).WithMany().HasForeignKey(x => x.ProfesionalId);
            e.HasOne(x => x.Servicio).WithMany().HasForeignKey(x => x.ServicioId);
            e.HasOne(x => x.Estado).WithMany().HasForeignKey(x => x.EstadoId);
        });

        modelBuilder.Entity<TurnoFijo>(e =>
        {
            e.ToTable("turno_fijo");
            e.HasOne(x => x.Salon).WithMany().HasForeignKey(x => x.SalonId);
            e.HasOne(x => x.Profesional).WithMany().HasForeignKey(x => x.ProfesionalId);
            e.HasOne(x => x.Servicio).WithMany().HasForeignKey(x => x.ServicioId);
        });

        modelBuilder.Entity<Usuario>(e =>
        {
            e.ToTable("usuario");
            e.HasIndex(x => x.Email).IsUnique();
            e.HasOne(x => x.Profesional).WithMany().HasForeignKey(x => x.ProfesionalId);
        });

        modelBuilder.Entity<Notificacion>(e =>
        {
            e.ToTable("notificacion");
            e.HasOne(x => x.Turno).WithMany().HasForeignKey(x => x.TurnoId);
        });

        modelBuilder.Entity<PushSuscripcion>(e =>
        {
            e.ToTable("push_suscripcion");
            e.Property(x => x.P256dh).HasColumnName("p256dh");
            e.HasIndex(x => x.Endpoint).IsUnique();
        });
    }
}
