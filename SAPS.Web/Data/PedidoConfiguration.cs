using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SAPS.Web.Models.Catalogo;
using SAPS.Web.Models.Pedidos;

namespace SAPS.Web.Data;

internal static class PedidoConfiguration
{
    public static void ConfigurarPedidos(this ModelBuilder builder)
    {
        builder.Entity<Pedido>(e =>
        {
            e.ToTable("tb_Pedido", t =>
            {
                t.HasCheckConstraint("CK_Pedido_Total", "[Total] > 0");
            });
            e.HasKey(p => p.IdPedido);
            e.HasIndex(p => p.TokenRegistro).IsUnique();
            e.HasIndex(p => new { p.CodigoColaborador, p.FechaRegistroUtc });
            e.Property(p => p.CodigoColaborador).HasMaxLength(50).IsRequired();
            e.Property(p => p.NombreColaborador).HasMaxLength(150).IsRequired();
            e.Property(p => p.TipoComida).HasMaxLength(50).IsRequired();
            e.Property(p => p.Observaciones).HasMaxLength(500);
            e.HasOne<IdentityUser>().WithMany().HasForeignKey(p => p.IdUsuarioRegistro).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<DetallePedido>(e =>
        {
            e.ToTable("tb_DetallePedido", t =>
            {
                t.HasCheckConstraint("CK_DetallePedido_Cantidad", "[Cantidad] > 0");
                t.HasCheckConstraint("CK_DetallePedido_Precio", "[PrecioUnitario] > 0");
                t.HasCheckConstraint("CK_DetallePedido_Subtotal", "[Subtotal] = CAST([Cantidad] AS bigint) * [PrecioUnitario]");
                t.HasCheckConstraint("CK_DetallePedido_Articulo", "([IdPrecio] IS NOT NULL AND [IdBebida] IS NULL) OR ([IdPrecio] IS NULL AND [IdBebida] IS NOT NULL)");
            });
            e.HasKey(d => d.IdDetallePedido);
            e.Property(d => d.NombreArticulo).HasMaxLength(100).IsRequired();
            e.Property(d => d.NombreTamano).HasMaxLength(30);
            e.HasOne(d => d.Pedido).WithMany(p => p.Detalles).HasForeignKey(d => d.IdPedido).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Precio>().WithMany().HasForeignKey(d => d.IdPrecio).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Bebida>().WithMany().HasForeignKey(d => d.IdBebida).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
