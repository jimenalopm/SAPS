using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using SAPS.Tests.Helpers;
using SAPS.Web.Models.Catalogo;

namespace SAPS.Tests;

/// <summary>
/// HU-007 | Gestionar catálogo de productos y precios.
/// Criterios: productos agrupados por categoría; precios en colones como números
/// enteros positivos (RNF-003); productos con varios tamaños tienen un precio por
/// tamaño; un único precio activo por producto+tamaño, conservando el historial;
/// bebidas en catálogo separado con tipos permitidos; no se pierden datos al
/// borrar categorías/tamaños en uso.
/// </summary>
public class HU007_CatalogoTests
{
    // Modelo "de diseño" completo (incluye check constraints, filtros e índices),
    // generado con el proveedor SQL Server igual que en producción.
    private static IModel ModeloSqlServer()
    {
        using var ctx = TestServices.CrearContextoSqlServerSinConexion();
        return ctx.GetService<IDesignTimeModel>().Model;
    }

    private static IEntityType Entidad<T>() => ModeloSqlServer().FindEntityType(typeof(T))!;

    // ---------- Persistencia (EF Core InMemory) ----------

    [Fact]
    public async Task Producto_SeRegistraDentroDeSuCategoria()
    {
        await using var ctx = TestServices.CrearContextoInMemory();
        var almuerzo = new Categoria { NombreCategoria = "Almuerzo" };
        ctx.Categorias.Add(almuerzo);
        ctx.Productos.Add(new Producto { NombreProducto = "Casado con pollo", Categoria = almuerzo });
        ctx.Productos.Add(new Producto { NombreProducto = "Olla de carne", Categoria = almuerzo, EsEspecial = true });
        await ctx.SaveChangesAsync();

        var categoria = await ctx.Categorias.Include(c => c.Productos).SingleAsync();

        Assert.Equal("Almuerzo", categoria.NombreCategoria);
        Assert.Equal(2, categoria.Productos.Count);
        Assert.Single(categoria.Productos, p => p.EsEspecial);
    }

    [Fact]
    public async Task Producto_SinTamanos_TienePrecioUnicoConTamanoNulo()
    {
        await using var ctx = TestServices.CrearContextoInMemory();
        var producto = new Producto { NombreProducto = "Gallo pinto", Categoria = new Categoria { NombreCategoria = "Desayuno" } };
        ctx.Precios.Add(new Precio { Producto = producto, MontoPrecio = 1800 });
        await ctx.SaveChangesAsync();

        var precio = await ctx.Precios.Include(p => p.Producto).SingleAsync();

        Assert.Null(precio.IdTamano);
        Assert.Equal(1800, precio.MontoPrecio);
        Assert.Equal("Gallo pinto", precio.Producto!.NombreProducto);
    }

    [Fact]
    public async Task Producto_ConTamanos_TieneUnPrecioPorCadaTamano()
    {
        await using var ctx = TestServices.CrearContextoInMemory();
        var pequeno = new Tamano { NombreTamano = "Pequeño" };
        var mediano = new Tamano { NombreTamano = "Mediano" };
        var grande = new Tamano { NombreTamano = "Grande" };
        var fresco = new Producto
        {
            NombreProducto = "Fresco de cas",
            RequiereTamano = true,
            Categoria = new Categoria { NombreCategoria = "Fresco" }
        };
        ctx.Precios.AddRange(
            new Precio { Producto = fresco, Tamano = pequeno, MontoPrecio = 600 },
            new Precio { Producto = fresco, Tamano = mediano, MontoPrecio = 800 },
            new Precio { Producto = fresco, Tamano = grande, MontoPrecio = 1000 });
        await ctx.SaveChangesAsync();

        var cargado = await ctx.Productos.Include(p => p.Precios).ThenInclude(p => p.Tamano).SingleAsync();

        Assert.True(cargado.RequiereTamano);
        Assert.Equal(3, cargado.Precios.Count);
        Assert.Equal(1000, cargado.Precios.Single(p => p.Tamano!.NombreTamano == "Grande").MontoPrecio);
    }

    [Fact]
    public async Task Precio_ActualizacionConservaHistorialYUnSoloPrecioActivo()
    {
        await using var ctx = TestServices.CrearContextoInMemory();
        var producto = new Producto { NombreProducto = "Café", Categoria = new Categoria { NombreCategoria = "Café/Repostería" } };
        var anterior = new Precio { Producto = producto, MontoPrecio = 700, FechaVigenciaDesde = new DateOnly(2026, 1, 1) };
        ctx.Precios.Add(anterior);
        await ctx.SaveChangesAsync();

        // Cambio de precio: se cierra la fila vigente y se inserta una nueva activa
        var hoy = new DateOnly(2026, 9, 1);
        anterior.Activo = false;
        anterior.FechaVigenciaHasta = hoy;
        ctx.Precios.Add(new Precio { Producto = producto, MontoPrecio = 750, FechaVigenciaDesde = hoy });
        await ctx.SaveChangesAsync();

        var precios = await ctx.Precios.Where(p => p.IdProducto == producto.IdProducto).ToListAsync();
        Assert.Equal(2, precios.Count);
        var vigente = Assert.Single(precios, p => p.Activo);
        Assert.Equal(750, vigente.MontoPrecio);
        Assert.Null(vigente.FechaVigenciaHasta);
        Assert.Equal(hoy, precios.Single(p => !p.Activo).FechaVigenciaHasta);
    }

    [Fact]
    public async Task Bebida_SeRegistraEnCatalogoSeparadoConSuPresentacion()
    {
        await using var ctx = TestServices.CrearContextoInMemory();
        var presentacion = new Tamano { NombreTamano = "600ml" };
        ctx.Bebidas.Add(new Bebida { NombreBebida = "Coca-Cola", TipoBebida = "Gaseosa", Tamano = presentacion, Precio = 1200 });
        await ctx.SaveChangesAsync();

        var bebida = await ctx.Bebidas.Include(b => b.Tamano).SingleAsync();

        Assert.Equal("Gaseosa", bebida.TipoBebida);
        Assert.Equal("600ml", bebida.Tamano!.NombreTamano);
        Assert.Empty(ctx.Productos); // no se mezcla con los productos del menú
    }

    // ---------- Valores por defecto de las entidades ----------

    [Fact]
    public void EntidadesNuevas_QuedanActivasPorDefecto()
    {
        Assert.True(new Categoria { NombreCategoria = "X" }.Activo);
        Assert.True(new Tamano { NombreTamano = "X" }.Activo);
        Assert.True(new Producto { NombreProducto = "X" }.Activo);
        Assert.True(new Precio().Activo);
        Assert.True(new Bebida { NombreBebida = "X", TipoBebida = "Jugo" }.Activo);
    }

    [Fact]
    public void ProductoNuevo_NoEsEspecialNiRequiereTamanoPorDefecto()
    {
        var producto = new Producto { NombreProducto = "Arroz" };

        Assert.False(producto.EsEspecial);
        Assert.False(producto.RequiereTamano);
    }

    [Fact]
    public void PrecioNuevo_VigenteDesdeHoySinFechaDeCierre()
    {
        var precio = new Precio();

        Assert.Equal(DateOnly.FromDateTime(DateTime.Today), precio.FechaVigenciaDesde);
        Assert.Null(precio.FechaVigenciaHasta);
    }

    // ---------- Reglas mapeadas en el modelo de base de datos ----------

    [Fact]
    public void Precios_SonNumerosEnterosSinDecimales_RNF003()
    {
        Assert.Equal(typeof(int), Entidad<Precio>().FindProperty(nameof(Precio.MontoPrecio))!.ClrType);
        Assert.Equal(typeof(int), Entidad<Bebida>().FindProperty(nameof(Bebida.Precio))!.ClrType);
    }

    [Fact]
    public void Precio_TieneRestriccionDeMontoPositivo()
    {
        var ck = Entidad<Precio>().GetCheckConstraints().Single(c => c.Name == "CK_Precio_PrecioPositivo");
        Assert.Equal("[precio] > 0", ck.Sql);
    }

    [Fact]
    public void Bebida_TieneRestriccionesDePrecioPositivoYTiposPermitidos()
    {
        var checks = Entidad<Bebida>().GetCheckConstraints().ToDictionary(c => c.Name!, c => c.Sql);

        Assert.Equal("[precio] > 0", checks["CK_Bebida_PrecioPositivo"]);
        foreach (var tipo in new[] { "Gaseosa", "Embotellada", "Energizante", "Jugo" })
            Assert.Contains($"'{tipo}'", checks["CK_Bebida_TipoBebida"]);
    }

    [Fact]
    public void Precio_SoloUnPrecioActivoPorProductoYTamano()
    {
        var indice = Entidad<Precio>().GetIndexes().Single(i => i.GetDatabaseName() == "IX_Precio_Producto_Tamano");

        Assert.True(indice.IsUnique);
        Assert.Equal(new[] { nameof(Precio.IdProducto), nameof(Precio.IdTamano) }, indice.Properties.Select(p => p.Name));
        Assert.Equal("[activo] = 1", indice.GetFilter());
    }

    [Fact]
    public void Categoria_YTamano_TienenNombreUnico()
    {
        Assert.Contains(Entidad<Categoria>().GetIndexes(), i => i.IsUnique && i.GetDatabaseName() == "UQ_Categoria_NombreCategoria");
        Assert.Contains(Entidad<Tamano>().GetIndexes(), i => i.IsUnique && i.GetDatabaseName() == "UQ_Tamano_NombreTamano");
    }

    [Theory]
    [InlineData(typeof(Producto), nameof(Producto.NombreProducto), 100)]
    [InlineData(typeof(Categoria), nameof(Categoria.NombreCategoria), 50)]
    [InlineData(typeof(Tamano), nameof(Tamano.NombreTamano), 30)]
    [InlineData(typeof(Bebida), nameof(Bebida.NombreBebida), 100)]
    public void Nombres_SonObligatoriosYConLongitudMaxima(Type entidad, string propiedad, int maximo)
    {
        var prop = ModeloSqlServer().FindEntityType(entidad)!.FindProperty(propiedad)!;

        Assert.False(prop.IsNullable);
        Assert.Equal(maximo, prop.GetMaxLength());
    }

    [Fact]
    public void RelacionesDelCatalogo_NoPermitenBorradoEnCascada()
    {
        var fks = new[] { Entidad<Producto>(), Entidad<Precio>(), Entidad<Bebida>() }
            .SelectMany(e => e.GetForeignKeys())
            .ToList();

        Assert.Equal(4, fks.Count); // Producto→Categoria, Precio→Producto, Precio→Tamano, Bebida→Tamano
        Assert.All(fks, fk => Assert.Equal(DeleteBehavior.Restrict, fk.DeleteBehavior));
    }

    [Theory]
    [InlineData(typeof(Categoria), "tb_Categoria")]
    [InlineData(typeof(Tamano), "tb_Tamano")]
    [InlineData(typeof(Producto), "tb_Producto")]
    [InlineData(typeof(Precio), "tb_Precio")]
    [InlineData(typeof(Bebida), "tb_Bebida")]
    public void Entidades_SeMapeanALasTablasDelDiseno(Type entidad, string tabla)
    {
        Assert.Equal(tabla, ModeloSqlServer().FindEntityType(entidad)!.GetTableName());
    }
}
