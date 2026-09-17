using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SAPS.Web.Models.Catalogo;

namespace SAPS.Web.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext(options)
{
    public DbSet<Categoria> Categorias => Set<Categoria>();
    public DbSet<Tamano> Tamanos => Set<Tamano>();
    public DbSet<Producto> Productos => Set<Producto>();
    public DbSet<Precio> Precios => Set<Precio>();
    public DbSet<Bebida> Bebidas => Set<Bebida>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // ---------- tb_Categoria ----------
        builder.Entity<Categoria>(entity =>
        {
            entity.ToTable("tb_Categoria");
            entity.HasKey(e => e.IdCategoria);
            entity.Property(e => e.IdCategoria).HasColumnName("idCategoria");
            entity.Property(e => e.NombreCategoria).HasColumnName("nombreCategoria").HasMaxLength(50).IsRequired();
            entity.Property(e => e.Activo).HasColumnName("activo").HasDefaultValue(true);

            entity.HasIndex(e => e.NombreCategoria)
                  .IsUnique()
                  .HasDatabaseName("UQ_Categoria_NombreCategoria");
        });

        // ---------- tb_Tamano ----------
        builder.Entity<Tamano>(entity =>
        {
            entity.ToTable("tb_Tamano");
            entity.HasKey(e => e.IdTamano);
            entity.Property(e => e.IdTamano).HasColumnName("idTamano");
            entity.Property(e => e.NombreTamano).HasColumnName("nombreTamano").HasMaxLength(30).IsRequired();
            entity.Property(e => e.Activo).HasColumnName("activo").HasDefaultValue(true);

            entity.HasIndex(e => e.NombreTamano)
                  .IsUnique()
                  .HasDatabaseName("UQ_Tamano_NombreTamano");
        });

        // ---------- tb_Producto ----------
        builder.Entity<Producto>(entity =>
        {
            entity.ToTable("tb_Producto");
            entity.HasKey(e => e.IdProducto);
            entity.Property(e => e.IdProducto).HasColumnName("idProducto");
            entity.Property(e => e.NombreProducto).HasColumnName("nombreProducto").HasMaxLength(100).IsRequired();
            entity.Property(e => e.IdCategoria).HasColumnName("idCategoria");
            entity.Property(e => e.EsEspecial).HasColumnName("esEspecial").HasDefaultValue(false);
            entity.Property(e => e.RequiereTamano).HasColumnName("requiereTamano").HasDefaultValue(false);
            entity.Property(e => e.Activo).HasColumnName("activo").HasDefaultValue(true);

            entity.HasOne(e => e.Categoria)
                  .WithMany(c => c.Productos)
                  .HasForeignKey(e => e.IdCategoria)
                  .HasConstraintName("FK_Producto_Categoria")
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => e.IdCategoria).HasDatabaseName("IX_Producto_Categoria");
        });

        // ---------- tb_Precio ----------
        builder.Entity<Precio>(entity =>
        {
            entity.ToTable("tb_Precio", t =>
                t.HasCheckConstraint("CK_Precio_PrecioPositivo", "[precio] > 0"));

            entity.HasKey(e => e.IdPrecio);
            entity.Property(e => e.IdPrecio).HasColumnName("idPrecio");
            entity.Property(e => e.IdProducto).HasColumnName("idProducto");
            entity.Property(e => e.IdTamano).HasColumnName("idTamano");
            entity.Property(e => e.MontoPrecio).HasColumnName("precio");
            entity.Property(e => e.FechaVigenciaDesde).HasColumnName("fechaVigenciaDesde");
            entity.Property(e => e.FechaVigenciaHasta).HasColumnName("fechaVigenciaHasta");
            entity.Property(e => e.Activo).HasColumnName("activo").HasDefaultValue(true);

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
                  .HasDatabaseName("IX_Precio_Producto_Tamano")
                  .HasFilter("[activo] = 1");
        });

        // ---------- tb_Bebida ----------
        builder.Entity<Bebida>(entity =>
        {
            entity.ToTable("tb_Bebida", t =>
            {
                t.HasCheckConstraint("CK_Bebida_PrecioPositivo", "[precio] > 0");
                t.HasCheckConstraint("CK_Bebida_TipoBebida",
                    "[tipoBebida] IN ('Gaseosa','Embotellada','Energizante','Jugo')");
            });

            entity.HasKey(e => e.IdBebida);
            entity.Property(e => e.IdBebida).HasColumnName("idBebida");
            entity.Property(e => e.NombreBebida).HasColumnName("nombreBebida").HasMaxLength(100).IsRequired();
            entity.Property(e => e.TipoBebida).HasColumnName("tipoBebida").HasMaxLength(20).IsRequired();
            entity.Property(e => e.IdTamano).HasColumnName("idTamano");
            entity.Property(e => e.Precio).HasColumnName("precio");
            entity.Property(e => e.Activo).HasColumnName("activo").HasDefaultValue(true);

            entity.HasOne(e => e.Tamano)
                  .WithMany(t => t.Bebidas)
                  .HasForeignKey(e => e.IdTamano)
                  .HasConstraintName("FK_Bebida_Tamano")
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => e.IdTamano).HasDatabaseName("IX_Bebida_Tamano");
        });
    }
}