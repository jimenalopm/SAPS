using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SAPS.Web.Models.Identity;
using SAPS.Web.Models.Rrhh;

namespace SAPS.Web.Data;

internal static class VinculoUsuarioColaboradorConfiguration
{
    public static void ConfigurarVinculoUsuarioColaborador(this ModelBuilder builder)
    {
        // ---------- dbo.tb_UsuarioColaborador ----------
        builder.Entity<VinculoUsuarioColaborador>(e =>
        {
            e.ToTable("tb_UsuarioColaborador");
            e.HasKey(x => x.IdUsuario).HasName("PK_UsuarioColaborador");
            e.Property(x => x.IdUsuario).HasColumnName("idUsuario").HasMaxLength(450);   // mismo tipo que AspNetUsers.Id
            e.Property(x => x.IdColaborador).HasColumnName("idColaborador");
            e.Property(x => x.FechaVinculacion).HasColumnType("datetime2").IsRequired();

            e.HasOne<IdentityUser>().WithOne()
             .HasForeignKey<VinculoUsuarioColaborador>(x => x.IdUsuario)
             .HasConstraintName("FK_UsuarioColaborador_Usuario")
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne<ColaboradorRrhh>().WithOne()
             .HasForeignKey<VinculoUsuarioColaborador>(x => x.IdColaborador)
             .HasConstraintName("FK_UsuarioColaborador_Colaborador")
             .OnDelete(DeleteBehavior.Restrict);

            // 1 a 1: un colaborador no puede estar vinculado a más de una cuenta.
            e.HasIndex(x => x.IdColaborador).IsUnique().HasDatabaseName("UQ_UsuarioColaborador_idColaborador");
        });
    }
}
