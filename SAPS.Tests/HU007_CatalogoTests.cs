using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using SAPS.Tests.Helpers;
using SAPS.Web.Controllers;
using SAPS.Web.Data;
using SAPS.Web.Models.Catalogo;

namespace SAPS.Tests;

/// <summary>
/// HU-007 | Gestionar catálogo de productos y precios.
/// Criterios: productos agrupados por categoría; precios en colones como números
/// enteros positivos (RNF-003); productos con varios tamaños tienen un precio por
/// tamaño; un único precio activo por producto+tamaño, conservando el historial;
/// bebidas en catálogo separado con tipos permitidos; no se pierden datos al
/// borrar categorías/tamaños en uso; el administrador crea, edita, busca y
/// activa/desactiva categorías, tamaños, productos y bebidas sin perder historial.
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
        var ck = Entidad<Precio>().GetCheckConstraints().Single(c => c.Name == "CK_Precio_Monto");
        Assert.Equal("[Monto] > 0", ck.Sql);
    }

    [Fact]
    public void Bebida_TieneRestriccionesDePrecioPositivoYTiposPermitidos()
    {
        var checks = Entidad<Bebida>().GetCheckConstraints().ToDictionary(c => c.Name!, c => c.Sql);

        Assert.Equal("[Precio] > 0", checks["CK_Bebida_Precio"]);
        foreach (var tipo in new[] { "Gaseosa", "Embotellada", "Energizante", "Jugo" })
            Assert.Contains($"'{tipo}'", checks["CK_Bebida_Tipo"]);
    }

    [Fact]
    public void Precio_SoloUnPrecioActivoPorProductoYTamano()
    {
        var indice = Entidad<Precio>().GetIndexes().Single(i => i.GetDatabaseName() == "IX_Precio_idProducto_idTamano");

        Assert.True(indice.IsUnique);
        Assert.Equal(new[] { nameof(Precio.IdProducto), nameof(Precio.IdTamano) }, indice.Properties.Select(p => p.Name));
        Assert.Equal("[EstaActivo] = 1", indice.GetFilter());
    }

    [Fact]
    public void Categoria_YTamano_TienenNombreUnico()
    {
        Assert.Contains(Entidad<Categoria>().GetIndexes(), i => i.IsUnique && i.GetDatabaseName() == "UQ_Categoria_Nombre");
        Assert.Contains(Entidad<Tamano>().GetIndexes(), i => i.IsUnique && i.GetDatabaseName() == "UQ_Tamano_Nombre");
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
    [InlineData(typeof(SAPS.Web.Models.Pedidos.Pedido), "tb_Pedido")]
    [InlineData(typeof(SAPS.Web.Models.Pedidos.DetallePedido), "tb_DetallePedido")]
    public void Entidades_SeMapeanALasTablasDelDiseno(Type entidad, string tabla)
    {
        var tipo = ModeloSqlServer().FindEntityType(entidad)!;
        Assert.Equal(tabla, tipo.GetTableName());
        Assert.Equal("soda", tipo.GetSchema()); // EBD13: tablas del módulo soda
    }

    // ---------- CRUD del catálogo (AdministracionController) ----------

    private static AdministracionController Controlador(ApplicationDbContext db, string metodo = "POST") =>
        TestServices.ConContexto(new AdministracionController(db), metodo);

    private static async Task<(Categoria cat, Tamano pequeno, Tamano grande)> SembrarBaseAsync(ApplicationDbContext db)
    {
        var cat = new Categoria { NombreCategoria = "Almuerzo" };
        var pequeno = new Tamano { NombreTamano = "Pequeño" };
        var grande = new Tamano { NombreTamano = "Grande" };
        db.AddRange(cat, pequeno, grande);
        await db.SaveChangesAsync();
        return (cat, pequeno, grande);
    }

    private static async Task<Producto> SembrarProductoPrecioUnicoAsync(ApplicationDbContext db, Categoria cat, int monto)
    {
        var producto = new Producto { NombreProducto = "Casado", IdCategoria = cat.IdCategoria };
        producto.Precios.Add(new Precio { MontoPrecio = monto, FechaVigenciaDesde = new DateOnly(2026, 1, 1) });
        db.Productos.Add(producto);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        return producto;
    }

    [Fact]
    public async Task CrearCategoria_Valida_SeGuardaYRedirigeAlCatalogo()
    {
        await using var db = TestServices.CrearContextoInMemory();

        var resultado = await Controlador(db).CrearCategoria(new CategoriaFormViewModel { NombreCategoria = "Desayuno" });

        var redirect = Assert.IsType<RedirectToActionResult>(resultado);
        Assert.Equal(nameof(AdministracionController.Catalogo), redirect.ActionName);
        Assert.Equal("categorias", redirect.RouteValues!["tab"]);
        var guardada = await db.Categorias.SingleAsync();
        Assert.Equal("Desayuno", guardada.NombreCategoria);
        Assert.True(guardada.Activo);
    }

    [Fact]
    public async Task CrearCategoria_NombreDuplicado_EsRechazada()
    {
        await using var db = TestServices.CrearContextoInMemory();
        db.Categorias.Add(new Categoria { NombreCategoria = "Desayuno" });
        await db.SaveChangesAsync();
        var controlador = Controlador(db);

        var resultado = await controlador.CrearCategoria(new CategoriaFormViewModel { NombreCategoria = "Desayuno" });

        Assert.IsType<ViewResult>(resultado);
        Assert.True(controlador.ModelState.ContainsKey(nameof(CategoriaFormViewModel.NombreCategoria)));
        Assert.Equal(1, await db.Categorias.CountAsync());
    }

    [Fact]
    public async Task EditarCategoria_NombreDeOtraCategoria_EsRechazada()
    {
        await using var db = TestServices.CrearContextoInMemory();
        var desayuno = new Categoria { NombreCategoria = "Desayuno" };
        db.Categorias.AddRange(desayuno, new Categoria { NombreCategoria = "Almuerzo" });
        await db.SaveChangesAsync();
        var controlador = Controlador(db);

        var resultado = await controlador.EditarCategoria(
            new CategoriaFormViewModel { IdCategoria = desayuno.IdCategoria, NombreCategoria = "Almuerzo" });

        Assert.IsType<ViewResult>(resultado);
        Assert.False(controlador.ModelState.IsValid);
        Assert.Equal("Desayuno", (await db.Categorias.FindAsync(desayuno.IdCategoria))!.NombreCategoria);
    }

    [Fact]
    public async Task EditarCategoria_Inexistente_DevuelveNotFound()
    {
        await using var db = TestServices.CrearContextoInMemory();

        var resultado = await Controlador(db, "GET").EditarCategoria(999);

        Assert.IsType<NotFoundResult>(resultado);
    }

    [Fact]
    public async Task CambiarEstadoCategoria_DesactivaSinBorrarYLuegoReactiva()
    {
        await using var db = TestServices.CrearContextoInMemory();
        var cat = new Categoria { NombreCategoria = "Fresco" };
        db.Categorias.Add(cat);
        await db.SaveChangesAsync();
        var controlador = Controlador(db);

        await controlador.CambiarEstadoCategoria(cat.IdCategoria);
        Assert.False((await db.Categorias.SingleAsync()).Activo);

        await controlador.CambiarEstadoCategoria(cat.IdCategoria);
        Assert.True((await db.Categorias.SingleAsync()).Activo);
    }

    [Fact]
    public async Task CrearTamano_NombreDuplicado_EsRechazado()
    {
        await using var db = TestServices.CrearContextoInMemory();
        db.Tamanos.Add(new Tamano { NombreTamano = "Grande" });
        await db.SaveChangesAsync();

        var resultado = await Controlador(db).CrearTamano(new TamanoFormViewModel { NombreTamano = "Grande" });

        Assert.IsType<ViewResult>(resultado);
        Assert.Equal(1, await db.Tamanos.CountAsync());
    }

    [Fact]
    public async Task CrearProducto_ConPrecioUnico_GuardaProductoYUnPrecioSinTamano()
    {
        await using var db = TestServices.CrearContextoInMemory();
        var (cat, _, _) = await SembrarBaseAsync(db);

        var resultado = await Controlador(db).CrearProducto(new ProductoFormViewModel
        {
            NombreProducto = "Casado con pollo", IdCategoria = cat.IdCategoria, PrecioUnico = 2800
        });

        Assert.IsType<RedirectToActionResult>(resultado);
        var producto = await db.Productos.Include(p => p.Precios).SingleAsync();
        var precio = Assert.Single(producto.Precios);
        Assert.Null(precio.IdTamano);
        Assert.Equal(2800, precio.MontoPrecio);
        Assert.True(precio.Activo);
    }

    [Fact]
    public async Task CrearProducto_ConTamanos_GuardaUnPrecioPorCadaTamanoSeleccionado()
    {
        await using var db = TestServices.CrearContextoInMemory();
        var (cat, pequeno, grande) = await SembrarBaseAsync(db);

        await Controlador(db).CrearProducto(new ProductoFormViewModel
        {
            NombreProducto = "Fresco de cas", IdCategoria = cat.IdCategoria, RequiereTamano = true,
            PreciosPorTamano =
            [
                new PrecioPorTamanoInput { IdTamano = pequeno.IdTamano, Incluir = true, Monto = 600 },
                new PrecioPorTamanoInput { IdTamano = grande.IdTamano, Incluir = true, Monto = 1000 },
            ]
        });

        var precios = await db.Precios.OrderBy(p => p.MontoPrecio).ToListAsync();
        Assert.Equal(2, precios.Count);
        Assert.Equal(pequeno.IdTamano, precios[0].IdTamano);
        Assert.Equal(grande.IdTamano, precios[1].IdTamano);
    }

    [Theory]
    [InlineData(false, null, false, null)]   // precio único vacío
    [InlineData(false, 0, false, null)]      // precio único cero
    [InlineData(false, -500, false, null)]   // precio único negativo
    [InlineData(true, null, false, null)]    // requiere tamaño pero ninguno seleccionado
    [InlineData(true, null, true, null)]     // tamaño seleccionado sin precio
    [InlineData(true, null, true, 0)]        // tamaño seleccionado con precio cero
    public async Task CrearProducto_ConPreciosInvalidos_NoSeGuarda(bool requiereTamano, int? precioUnico, bool incluirTamano, int? montoTamano)
    {
        await using var db = TestServices.CrearContextoInMemory();
        var (cat, pequeno, _) = await SembrarBaseAsync(db);
        var controlador = Controlador(db);

        var resultado = await controlador.CrearProducto(new ProductoFormViewModel
        {
            NombreProducto = "Lasaña", IdCategoria = cat.IdCategoria, RequiereTamano = requiereTamano,
            PrecioUnico = precioUnico,
            PreciosPorTamano = [new PrecioPorTamanoInput { IdTamano = pequeno.IdTamano, Incluir = incluirTamano, Monto = montoTamano }]
        });

        Assert.IsType<ViewResult>(resultado);
        Assert.False(controlador.ModelState.IsValid);
        Assert.Empty(db.Productos);
        Assert.Empty(db.Precios);
    }

    [Fact]
    public async Task CrearProducto_ConTamanoRepetido_NoSeGuarda()
    {
        await using var db = TestServices.CrearContextoInMemory();
        var (cat, pequeno, _) = await SembrarBaseAsync(db);

        var resultado = await Controlador(db).CrearProducto(new ProductoFormViewModel
        {
            NombreProducto = "Fresco", IdCategoria = cat.IdCategoria, RequiereTamano = true,
            PreciosPorTamano =
            [
                new PrecioPorTamanoInput { IdTamano = pequeno.IdTamano, Incluir = true, Monto = 600 },
                new PrecioPorTamanoInput { IdTamano = pequeno.IdTamano, Incluir = true, Monto = 700 },
            ]
        });

        Assert.IsType<ViewResult>(resultado);
        Assert.Empty(db.Productos);
    }

    [Fact]
    public async Task CrearProducto_EnCategoriaInactiva_NoSeGuarda()
    {
        await using var db = TestServices.CrearContextoInMemory();
        var cat = new Categoria { NombreCategoria = "Vieja", Activo = false };
        db.Categorias.Add(cat);
        await db.SaveChangesAsync();
        var controlador = Controlador(db);

        var resultado = await controlador.CrearProducto(new ProductoFormViewModel
        {
            NombreProducto = "X", IdCategoria = cat.IdCategoria, PrecioUnico = 1000
        });

        Assert.IsType<ViewResult>(resultado);
        Assert.True(controlador.ModelState.ContainsKey(nameof(ProductoFormViewModel.IdCategoria)));
        Assert.Empty(db.Productos);
    }

    [Fact]
    public async Task EditarProducto_CambioDePrecio_CierraElAnteriorYCreaUnoNuevoActivo()
    {
        await using var db = TestServices.CrearContextoInMemory();
        var (cat, _, _) = await SembrarBaseAsync(db);
        var producto = await SembrarProductoPrecioUnicoAsync(db, cat, 2500);

        var resultado = await Controlador(db).EditarProducto(new ProductoFormViewModel
        {
            IdProducto = producto.IdProducto, NombreProducto = "Casado", IdCategoria = cat.IdCategoria, PrecioUnico = 2800
        });

        Assert.IsType<RedirectToActionResult>(resultado);
        var precios = await db.Precios.AsNoTracking().ToListAsync();
        Assert.Equal(2, precios.Count);
        var cerrado = Assert.Single(precios, p => !p.Activo);
        Assert.Equal(2500, cerrado.MontoPrecio);
        Assert.Equal(DateOnly.FromDateTime(DateTime.Today), cerrado.FechaVigenciaHasta);
        var vigente = Assert.Single(precios, p => p.Activo);
        Assert.Equal(2800, vigente.MontoPrecio);
        Assert.Null(vigente.FechaVigenciaHasta);
    }

    [Fact]
    public async Task EditarProducto_SinCambioDePrecio_NoGeneraHistorial()
    {
        await using var db = TestServices.CrearContextoInMemory();
        var (cat, _, _) = await SembrarBaseAsync(db);
        var producto = await SembrarProductoPrecioUnicoAsync(db, cat, 2500);

        await Controlador(db).EditarProducto(new ProductoFormViewModel
        {
            IdProducto = producto.IdProducto, NombreProducto = "Casado típico", IdCategoria = cat.IdCategoria, PrecioUnico = 2500
        });

        var precio = Assert.Single(await db.Precios.AsNoTracking().ToListAsync());
        Assert.True(precio.Activo);
        Assert.Equal("Casado típico", (await db.Productos.AsNoTracking().SingleAsync()).NombreProducto);
    }

    [Fact]
    public async Task EditarProducto_DePrecioUnicoATamanos_CierraElPrecioUnico()
    {
        await using var db = TestServices.CrearContextoInMemory();
        var (cat, pequeno, grande) = await SembrarBaseAsync(db);
        var producto = await SembrarProductoPrecioUnicoAsync(db, cat, 2500);

        await Controlador(db).EditarProducto(new ProductoFormViewModel
        {
            IdProducto = producto.IdProducto, NombreProducto = "Casado", IdCategoria = cat.IdCategoria, RequiereTamano = true,
            PreciosPorTamano =
            [
                new PrecioPorTamanoInput { IdTamano = pequeno.IdTamano, Incluir = true, Monto = 2000 },
                new PrecioPorTamanoInput { IdTamano = grande.IdTamano, Incluir = true, Monto = 3000 },
            ]
        });

        var activos = await db.Precios.AsNoTracking().Where(p => p.Activo).ToListAsync();
        Assert.Equal(2, activos.Count);
        Assert.All(activos, p => Assert.NotNull(p.IdTamano));
        var unico = Assert.Single(await db.Precios.AsNoTracking().Where(p => p.IdTamano == null).ToListAsync());
        Assert.False(unico.Activo);
        Assert.True((await db.Productos.AsNoTracking().SingleAsync()).RequiereTamano);
    }

    [Fact]
    public async Task CambiarEstadoProducto_DesactivaSinTocarSusPrecios()
    {
        await using var db = TestServices.CrearContextoInMemory();
        var (cat, _, _) = await SembrarBaseAsync(db);
        var producto = await SembrarProductoPrecioUnicoAsync(db, cat, 2500);

        await Controlador(db).CambiarEstadoProducto(producto.IdProducto);

        Assert.False((await db.Productos.AsNoTracking().SingleAsync()).Activo);
        Assert.True((await db.Precios.AsNoTracking().SingleAsync()).Activo);
    }

    [Fact]
    public async Task CrearBebida_Valida_SeGuarda()
    {
        await using var db = TestServices.CrearContextoInMemory();
        var (_, pequeno, _) = await SembrarBaseAsync(db);

        var resultado = await Controlador(db).CrearBebida(new BebidaFormViewModel
        {
            NombreBebida = "Agua Cristal", TipoBebida = "Embotellada", IdTamano = pequeno.IdTamano, Precio = 900
        });

        Assert.IsType<RedirectToActionResult>(resultado);
        var bebida = await db.Bebidas.SingleAsync();
        Assert.Equal(900, bebida.Precio);
        Assert.Equal(pequeno.IdTamano, bebida.IdTamano);
    }

    [Fact]
    public async Task CrearBebida_TipoNoPermitido_EsRechazada()
    {
        await using var db = TestServices.CrearContextoInMemory();
        var controlador = Controlador(db);

        var resultado = await controlador.CrearBebida(new BebidaFormViewModel { NombreBebida = "Cerveza", TipoBebida = "Licor", Precio = 1500 });

        Assert.IsType<ViewResult>(resultado);
        Assert.True(controlador.ModelState.ContainsKey(nameof(BebidaFormViewModel.TipoBebida)));
        Assert.Empty(db.Bebidas);
    }

    [Fact]
    public async Task CrearBebida_ConTamanoInactivo_EsRechazada()
    {
        await using var db = TestServices.CrearContextoInMemory();
        var inactivo = new Tamano { NombreTamano = "3L", Activo = false };
        db.Tamanos.Add(inactivo);
        await db.SaveChangesAsync();
        var controlador = Controlador(db);

        var resultado = await controlador.CrearBebida(new BebidaFormViewModel
        {
            NombreBebida = "Coca-Cola", TipoBebida = "Gaseosa", IdTamano = inactivo.IdTamano, Precio = 2500
        });

        Assert.IsType<ViewResult>(resultado);
        Assert.True(controlador.ModelState.ContainsKey(nameof(BebidaFormViewModel.IdTamano)));
        Assert.Empty(db.Bebidas);
    }

    [Fact]
    public async Task Catalogo_PorDefectoOcultaInactivosYPermiteIncluirlosYBuscar()
    {
        await using var db = TestServices.CrearContextoInMemory();
        db.Categorias.AddRange(
            new Categoria { NombreCategoria = "Desayuno" },
            new Categoria { NombreCategoria = "Almuerzo" },
            new Categoria { NombreCategoria = "Antigua", Activo = false });
        await db.SaveChangesAsync();
        var controlador = Controlador(db, "GET");

        var soloActivas = (CatalogoIndexViewModel)Assert.IsType<ViewResult>(await controlador.Catalogo()).Model!;
        var todas = (CatalogoIndexViewModel)Assert.IsType<ViewResult>(await controlador.Catalogo(catInc: true)).Model!;
        var busqueda = (CatalogoIndexViewModel)Assert.IsType<ViewResult>(await controlador.Catalogo(catQ: "Desa")).Model!;

        Assert.Equal(new[] { "Almuerzo", "Desayuno" }, soloActivas.Categorias.Select(c => c.NombreCategoria));
        Assert.Equal(3, todas.Categorias.Count);
        Assert.Equal("Desayuno", Assert.Single(busqueda.Categorias).NombreCategoria);
    }

    [Theory]
    [InlineData(typeof(CategoriaFormViewModel), nameof(CategoriaFormViewModel.NombreCategoria))]
    [InlineData(typeof(TamanoFormViewModel), nameof(TamanoFormViewModel.NombreTamano))]
    [InlineData(typeof(ProductoFormViewModel), nameof(ProductoFormViewModel.NombreProducto))]
    [InlineData(typeof(BebidaFormViewModel), nameof(BebidaFormViewModel.NombreBebida))]
    public void Formularios_NombreEsObligatorio(Type tipoFormulario, string propiedad)
    {
        var modelo = Activator.CreateInstance(tipoFormulario)!;
        tipoFormulario.GetProperty(propiedad)!.SetValue(modelo, "");
        var errores = new List<ValidationResult>();

        Validator.TryValidateObject(modelo, new ValidationContext(modelo), errores, validateAllProperties: true);

        Assert.Contains(errores, e => e.MemberNames.Contains(propiedad));
    }
}
