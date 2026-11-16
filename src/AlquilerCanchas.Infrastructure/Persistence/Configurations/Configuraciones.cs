using AlquilerCanchas.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlquilerCanchas.Infrastructure.Persistence.Configurations;

public class ClienteConfiguracion : IEntityTypeConfiguration<Cliente>
{
    public void Configure(EntityTypeBuilder<Cliente> b)
    {
        b.ToTable("Clientes");
        b.Property(x => x.Nombre).HasMaxLength(80).IsRequired();
        b.Property(x => x.Apellido).HasMaxLength(80).IsRequired();
        b.Property(x => x.Email).HasMaxLength(150).IsRequired();
        b.Property(x => x.Telefono).HasMaxLength(30).IsRequired();

        // El servicio ya valida el duplicado para dar un error claro; el índice único es la red
        // de seguridad ante dos altas simultáneas.
        b.HasIndex(x => x.Email).IsUnique();
    }
}

public class CanchaConfiguracion : IEntityTypeConfiguration<Cancha>
{
    public void Configure(EntityTypeBuilder<Cancha> b)
    {
        b.ToTable("Canchas");
        b.Property(x => x.Nombre).HasMaxLength(80).IsRequired();
        b.Property(x => x.TipoDeporte).HasMaxLength(50).IsRequired();
        b.Property(x => x.PrecioPorHora).HasPrecision(10, 2);
    }
}

public class ReservaConfiguracion : IEntityTypeConfiguration<Reserva>
{
    public void Configure(EntityTypeBuilder<Reserva> b)
    {
        b.ToTable("Reservas");
        b.Property(x => x.Fecha).HasColumnType("date");
        b.Property(x => x.HoraInicio).HasColumnType("time(0)");
        b.Property(x => x.HoraFin).HasColumnType("time(0)");

        // Texto legible en la base ("Confirmada") en lugar de un número que obliga a consultar el enum.
        b.Property(x => x.Estado).HasConversion<string>().HasMaxLength(20);

        b.HasOne(x => x.Cliente).WithMany(c => c.Reservas)
            .HasForeignKey(x => x.ClienteId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Cancha).WithMany(c => c.Reservas)
            .HasForeignKey(x => x.CanchaId).OnDelete(DeleteBehavior.Restrict);

        // La consulta de superposición filtra siempre por cancha y día.
        b.HasIndex(x => new { x.CanchaId, x.Fecha });
    }
}

public class PagoConfiguracion : IEntityTypeConfiguration<Pago>
{
    public void Configure(EntityTypeBuilder<Pago> b)
    {
        b.ToTable("Pagos");
        b.Property(x => x.Monto).HasPrecision(10, 2);
        b.Property(x => x.Fecha).HasColumnType("date");
        b.Property(x => x.MetodoPago).HasConversion<string>().HasMaxLength(20);

        b.HasOne(x => x.Reserva).WithMany(r => r.Pagos)
            .HasForeignKey(x => x.ReservaId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class UsuarioConfiguracion : IEntityTypeConfiguration<Usuario>
{
    public void Configure(EntityTypeBuilder<Usuario> b)
    {
        b.ToTable("Usuarios");
        b.Property(x => x.Email).HasMaxLength(150).IsRequired();
        b.Property(x => x.PasswordHash).HasMaxLength(200).IsRequired();
        b.Property(x => x.Rol).HasConversion<string>().HasMaxLength(20);
        b.HasIndex(x => x.Email).IsUnique();
    }
}
