using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SAPS.Web.Models.Catalogo;
using SAPS.Web.Models.Pedidos;

namespace SAPS.Web.Data;

internal static class PedidoConfiguration
{
    public static void ConfigurarPedidos(this ModelBuilder builder)
    {
        // ---------- soda.tb_Pedido ----------
        builder.Entity<Pedido>(e =>
        {
            e.ToTable("tb_Pedido", ApplicationDbContext.EsquemaSoda, t =>
            {
                t.HasCheckConstraint("CK_Pedido_Total", "[Total] > 0");
            });
            e.HasKey(p => p.IdPedido).HasName("PK_Pedido");
            e.Property(p => p.IdPedido).HasColumnName("idPedido");
            e.Property(p => p.IdUsuarioRegistro).HasColumnName("idUsuario");   // FK a AspNetUsers.Id (nvarchar(450))
            e.Property(p => p.CodigoColaborador).HasMaxLength(50).IsUnicode(false).IsRequired();
            e.Property(p => p.NombreColaborador).HasMaxLength(150).IsUnicode(false).IsRequired();
            e.Property(p => p.TipoComida).HasMaxLength(50).IsUnicode(false).IsRequired();
            e.Property(p => p.Observaciones).HasMaxLength(500).IsUnicode(false);

            e.HasIndex(p => p.TokenRegistro).IsUnique().HasDatabaseName("UQ_Pedido_TokenRegistro");
            e.HasIndex(p => new { p.CodigoColaborador, p.FechaRegistroUtc })
             .HasDatabaseName("IX_Pedido_CodigoColaborador_FechaRegistroUtc");
            e.HasIndex(p => p.IdUsuarioRegistro).HasDatabaseName("IX_Pedido_idUsuario");

            e.HasOne<IdentityUser>().WithMany()
             .HasForeignKey(p => p.IdUsuarioRegistro)
             .HasConstraintName("FK_Pedido_Usuario")
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ---------- soda.tb_DetallePedido ----------
        builder.Entity<DetallePedido>(e =>
        {
            e.ToTable("tb_DetallePedido", ApplicationDbContext.EsquemaSoda, t =>
            {
                t.HasCheckConstraint("CK_DetallePedido_Cantidad", "[Cantidad] > 0");
                t.HasCheckConstraint("CK_DetallePedido_PrecioUnitario", "[PrecioUnitario] > 0");
                t.HasCheckConstraint("CK_DetallePedido_Subtotal", "[Subtotal] = CAST([Cantidad] AS bigint) * [PrecioUnitario]");
                t.HasCheckConstraint("CK_DetallePedido_Articulo", "([idPrecio] IS NOT NULL AND [idBebida] IS NULL) OR ([idPrecio] IS NULL AND [idBebida] IS NOT NULL)");
            });
            e.HasKey(d => d.IdDetallePedido).HasName("PK_DetallePedido");
            e.Property(d => d.IdDetallePedido).HasColumnName("idDetallePedido");
            e.Property(d => d.IdPedido).HasColumnName("idPedido");
            e.Property(d => d.IdPrecio).HasColumnName("idPrecio");
            e.Property(d => d.IdBebida).HasColumnName("idBebida");
            e.Property(d => d.NombreArticulo).HasMaxLength(100).IsUnicode(false).IsRequired();
            e.Property(d => d.NombreTamano).HasMaxLength(30).IsUnicode(false);

            e.HasIndex(d => d.IdPedido).HasDatabaseName("IX_DetallePedido_idPedido");
            e.HasIndex(d => d.IdPrecio).HasDatabaseName("IX_DetallePedido_idPrecio");
            e.HasIndex(d => d.IdBebida).HasDatabaseName("IX_DetallePedido_idBebida");

            e.HasOne(d => d.Pedido).WithMany(p => p.Detalles)
             .HasForeignKey(d => d.IdPedido)
             .HasConstraintName("FK_DetallePedido_Pedido")
             .OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Precio>().WithMany()
             .HasForeignKey(d => d.IdPrecio)
             .HasConstraintName("FK_DetallePedido_Precio")
             .OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Bebida>().WithMany()
             .HasForeignKey(d => d.IdBebida)
             .HasConstraintName("FK_DetallePedido_Bebida")
             .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
