using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SAPS.Web.Models.Identity;

namespace SAPS.Web.Data;

internal static class EstadoContrasenaConfiguration
{
    public static void ConfigurarEstadoContrasena(this ModelBuilder builder)
    {
        // ---------- dbo.tb_EstadoContrasena ----------
        builder.Entity<EstadoContrasenaUsuario>(e =>
        {
            e.ToTable("tb_EstadoContrasena");
            e.HasKey(x => x.IdUsuario).HasName("PK_EstadoContrasena");
            e.Property(x => x.IdUsuario).HasColumnName("idUsuario").HasMaxLength(450);   // mismo tipo que AspNetUsers.Id
            e.Property(x => x.FechaUltimoCambio).HasColumnType("datetime2").IsRequired();

            e.HasOne<IdentityUser>().WithOne()
             .HasForeignKey<EstadoContrasenaUsuario>(x => x.IdUsuario)
             .HasConstraintName("FK_EstadoContrasena_Usuario")
             .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
