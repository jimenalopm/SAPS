using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SAPS.Web.Models.Catalogo;
using SAPS.Web.Models.Pedidos;

namespace SAPS.Web.Data;

/// <summary>
/// Mapeo alineado al documento IS-011 Registro de estándares:
/// EBD02 tablas tb_ | EBD03/EBD04 llaves id&lt;Tabla&gt; | EBD07 IX_&lt;Tabla&gt;_&lt;Campo&gt;
/// EBD08 PK_/FK_/CK_/UQ_ | EBD10 campos PascalCase, booleanos Es/Esta/Tiene
/// EBD11 VARCHAR para textos | EBD13 esquema por módulo (soda / rrhh).
/// Las tablas de ASP.NET Identity se dejan en dbo con sus nombres del framework.
/// Los nombres de las propiedades C# no cambian; solo el nombre en la base de datos.
/// </summary>
public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext(options)
{
    public const string EsquemaSoda = "soda";
    public const string EsquemaRrhh = "rrhh";

    public DbSet<Categoria> Categorias => Set<Categoria>();
    public DbSet<Tamano> Tamanos => Set<Tamano>();
    public DbSet<Producto> Productos => Set<Producto>();
    public DbSet<Precio> Precios => Set<Precio>();
    public DbSet<Bebida> Bebidas => Set<Bebida>();

    public DbSet<Pedido> Pedidos => Set<Pedido>();
    public DbSet<DetallePedido> DetallesPedido => Set<DetallePedido>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ConfigurarPedidos();

        // ---------- soda.tb_Categoria ----------
        builder.Entity<Categoria>(entity =>
        {
            entity.ToTable("tb_Categoria", EsquemaSoda);
            entity.HasKey(e => e.IdCategoria).HasName("PK_Categoria");
            entity.Property(e => e.IdCategoria).HasColumnName("idCategoria");
            entity.Property(e => e.NombreCategoria).HasColumnName("Nombre").HasMaxLength(50).IsUnicode(false).IsRequired();
            entity.Property(e => e.Activo).HasColumnName("EstaActivo").HasDefaultValue(true);

            entity.HasIndex(e => e.NombreCategoria)
                  .IsUnique()
                  .HasDatabaseName("UQ_Categoria_Nombre");
        });

        // ---------- soda.tb_Tamano ----------
        builder.Entity<Tamano>(entity =>
        {
            entity.ToTable("tb_Tamano", EsquemaSoda);
            entity.HasKey(e => e.IdTamano).HasName("PK_Tamano");
            entity.Property(e => e.IdTamano).HasColumnName("idTamano");
            entity.Property(e => e.NombreTamano).HasColumnName("Nombre").HasMaxLength(30).IsUnicode(false).IsRequired();
            entity.Property(e => e.Activo).HasColumnName("EstaActivo").HasDefaultValue(true);

            entity.HasIndex(e => e.NombreTamano)
                  .IsUnique()
                  .HasDatabaseName("UQ_Tamano_Nombre");
        });

        // ---------- soda.tb_Producto ----------
        builder.Entity<Producto>(entity =>
        {
            entity.ToTable("tb_Producto", EsquemaSoda);
            entity.HasKey(e => e.IdProducto).HasName("PK_Producto");
            entity.Property(e => e.IdProducto).HasColumnName("idProducto");
            entity.Property(e => e.NombreProducto).HasColumnName("Nombre").HasMaxLength(100).IsUnicode(false).IsRequired();
            entity.Property(e => e.IdCategoria).HasColumnName("idCategoria");
            entity.Property(e => e.EsEspecial).HasColumnName("EsEspecial").HasDefaultValue(false);
            entity.Property(e => e.RequiereTamano).HasColumnName("TieneTamano").HasDefaultValue(false);
            entity.Property(e => e.Activo).HasColumnName("EstaActivo").HasDefaultValue(true);

            entity.HasOne(e => e.Categoria)
                  .WithMany(c => c.Productos)
                  .HasForeignKey(e => e.IdCategoria)
                  .HasConstraintName("FK_Producto_Categoria")
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => e.IdCategoria).HasDatabaseName("IX_Producto_idCategoria");
        });

        // ---------- soda.tb_Precio ----------
        builder.Entity<Precio>(entity =>
        {
            entity.ToTable("tb_Precio", EsquemaSoda, t =>
            {
                t.HasCheckConstraint("CK_Precio_Monto", "[Monto] > 0");
                t.HasCheckConstraint("CK_Precio_Vigencia",
                    "[FechaVigenciaHasta] IS NULL OR [FechaVigenciaHasta] >= [FechaVigenciaDesde]");
            });

            entity.HasKey(e => e.IdPrecio).HasName("PK_Precio");
            entity.Property(e => e.IdPrecio).HasColumnName("idPrecio");
            entity.Property(e => e.IdProducto).HasColumnName("idProducto");
            entity.Property(e => e.IdTamano).HasColumnName("idTamano");
            // EBD10: no se repite el nombre de la tabla dentro de sus campos (antes: precio).
            entity.Property(e => e.MontoPrecio).HasColumnName("Monto");
            entity.Property(e => e.FechaVigenciaDesde).HasColumnName("FechaVigenciaDesde");
            entity.Property(e => e.FechaVigenciaHasta).HasColumnName("FechaVigenciaHasta");
            entity.Property(e => e.Activo).HasColumnName("EstaActivo").HasDefaultValue(true);

            entity.HasOne(e => e.Producto)
                  .WithMany(p => p.Precios)
                  .HasForeignKey(e => e.IdProducto)
                  .HasConstraintName("FK_Precio_Producto")
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Tamano)
                  .WithMany(t => t.Precios)
                  .HasForeignKey(e => e.IdTamano)
                  .HasConstraintName("FK_Precio_Tamano")
                  .OnDelete(DeleteBehavior.Restrict);

            // Un solo precio ACTIVO por combinación producto + tamaño
            entity.HasIndex(e => new { e.IdProducto, e.IdTamano })
                  .IsUnique()
                  .HasDatabaseName("IX_Precio_idProducto_idTamano")
                  .HasFilter("[EstaActivo] = 1");

            entity.HasIndex(e => e.IdTamano).HasDatabaseName("IX_Precio_idTamano");
        });

        // ---------- soda.tb_Bebida ----------
        builder.Entity<Bebida>(entity =>
        {
            entity.ToTable("tb_Bebida", EsquemaSoda, t =>
            {
                t.HasCheckConstraint("CK_Bebida_Precio", "[Precio] > 0");
                t.HasCheckConstraint("CK_Bebida_Tipo",
                    "[Tipo] IN ('Gaseosa','Embotellada','Energizante','Jugo')");
            });

            entity.HasKey(e => e.IdBebida).HasName("PK_Bebida");
            entity.Property(e => e.IdBebida).HasColumnName("idBebida");
            entity.Property(e => e.NombreBebida).HasColumnName("Nombre").HasMaxLength(100).IsUnicode(false).IsRequired();
            entity.Property(e => e.TipoBebida).HasColumnName("Tipo").HasMaxLength(20).IsUnicode(false).IsRequired();
            entity.Property(e => e.IdTamano).HasColumnName("idTamano");
            entity.Property(e => e.Precio).HasColumnName("Precio");
            entity.Property(e => e.Activo).HasColumnName("EstaActivo").HasDefaultValue(true);

            entity.HasOne(e => e.Tamano)
                  .WithMany(t => t.Bebidas)
                  .HasForeignKey(e => e.IdTamano)
                  .HasConstraintName("FK_Bebida_Tamano")
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => e.IdTamano).HasDatabaseName("IX_Bebida_idTamano");
        });
    }
}
