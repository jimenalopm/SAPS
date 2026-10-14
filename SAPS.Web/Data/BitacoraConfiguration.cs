using Microsoft.EntityFrameworkCore;
using SAPS.Web.Models.Auditoria;

namespace SAPS.Web.Data;

internal static class BitacoraConfiguration
{
    public static void ConfigurarBitacora(this ModelBuilder builder)
    {
        // ---------- soda.tb_Bitacora ----------
        // Sin llave foránea hacia AspNetUsers: un intento de login fallido puede ser de una cuenta que
        // no existe, y el log debe conservarse aunque cambien las cuentas. Sin CHECK sobre Modulo ni
        // TipoEvento, para poder agregar eventos nuevos sin alterar la tabla (HU-004 criterio 8).
        builder.Entity<Bitacora>(e =>
        {
            // tr_BitacoraSoloLectura (migración BloquearBitacoraSoloLectura) rechaza UPDATE y DELETE.
            // HasTrigger evita que EF use OUTPUT directo al insertar, que falla en tablas con triggers.
            e.ToTable("tb_Bitacora", ApplicationDbContext.EsquemaSoda, t => t.HasTrigger("tr_BitacoraSoloLectura"));
            e.HasKey(b => b.IdBitacora).HasName("PK_Bitacora");
            e.Property(b => b.IdBitacora).HasColumnName("idBitacora");

            e.Property(b => b.FechaHoraUtc).HasColumnType("datetime2").HasDefaultValueSql("SYSUTCDATETIME()");
            e.Property(b => b.Modulo).HasMaxLength(30).IsUnicode(false).IsRequired();
            e.Property(b => b.TipoEvento).HasMaxLength(50).IsUnicode(false).IsRequired();
            e.Property(b => b.Recurso).HasMaxLength(100).IsUnicode(false).IsRequired();
            e.Property(b => b.IdRegistro).HasMaxLength(50).IsUnicode(false);
            e.Property(b => b.EsExitoso);

            e.Property(b => b.IdUsuario).HasColumnName("idUsuario").HasMaxLength(450);   // mismo tipo que AspNetUsers.Id
            e.Property(b => b.NombreUsuario).HasMaxLength(256).IsUnicode(false);
            e.Property(b => b.Rol).HasMaxLength(50).IsUnicode(false);
            e.Property(b => b.UsuarioBd).HasMaxLength(128).IsUnicode(false).IsRequired().HasDefaultValueSql("SUSER_SNAME()");

            e.Property(b => b.DireccionIp).HasMaxLength(45).IsUnicode(false);   // 45 caracteres alcanzan para IPv6
            e.Property(b => b.Dispositivo).HasMaxLength(200).IsUnicode(false);
            e.Property(b => b.ValorAnterior).HasMaxLength(500).IsUnicode(false);
            e.Property(b => b.ValorNuevo).HasMaxLength(500).IsUnicode(false);
            e.Property(b => b.Descripcion).HasMaxLength(300).IsUnicode(false);

            // La pantalla muestra primero lo más reciente y filtra por tipo de evento (HU-015 criterio 6).
            e.HasIndex(b => b.FechaHoraUtc).IsDescending().HasDatabaseName("IX_Bitacora_FechaHoraUtc");
            e.HasIndex(b => new { b.TipoEvento, b.FechaHoraUtc }).IsDescending(false, true)
             .HasDatabaseName("IX_Bitacora_TipoEvento_FechaHoraUtc");
            e.HasIndex(b => b.IdUsuario).HasDatabaseName("IX_Bitacora_idUsuario");
        });
    }
}
