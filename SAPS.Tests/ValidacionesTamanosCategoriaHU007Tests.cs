using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SAPS.Tests.Helpers;
using SAPS.Web.Controllers;
using SAPS.Web.Data;
using SAPS.Web.Models.Catalogo;

namespace SAPS.Tests;

/// <summary>
/// HU-007 | Relación entre categorías y tamaños: los productos solo usan los tamaños permitidos
/// por su categoría, las bebidas solo usan tamaños de bebida y los tamaños en uso no se pueden
/// quitar ni cambiar de tipo.
/// </summary>
public class ValidacionesTamanosCategoriaHU007Tests
{
    private static AdministracionController Controlador(ApplicationDbContext db) =>
        TestServices.ConContexto(new AdministracionController(db), "POST");

    private static async Task<(Categoria desayuno, Categoria almuerzo, Tamano mediano)> SembrarAsync(ApplicationDbContext db)
    {
        var desayuno = new Categoria { NombreCategoria = "Desayuno" };
        var almuerzo = new Categoria { NombreCategoria = "Almuerzo" };
        var mediano = new Tamano { NombreTamano = "Mediano" };
        db.AddRange(desayuno, almuerzo, mediano);
        await db.SaveChangesAsync();
        db.CategoriasTamanos.Add(new CategoriaTamano { IdCategoria = almuerzo.IdCategoria, IdTamano = mediano.IdTamano });
        await db.SaveChangesAsync();
        return (desayuno, almuerzo, mediano);
    }

    private static ProductoFormViewModel ProductoConTamano(Categoria cat, Tamano tamano) => new()
    {
        NombreProducto = "Pizza",
        IdCategoria = cat.IdCategoria,
        RequiereTamano = true,
        PreciosPorTamano = [new PrecioPorTamanoInput { IdTamano = tamano.IdTamano, Incluir = true, Monto = 1500 }]
    };

    [Fact]
    public async Task CrearProducto_ConTamanoNoPermitidoParaLaCategoria_NoSeGuarda()
    {
        await using var db = TestServices.CrearContextoInMemory();
        var (desayuno, _, mediano) = await SembrarAsync(db);
        var c = Controlador(db);

        var resultado = await c.CrearProducto(ProductoConTamano(desayuno, mediano));

        Assert.IsType<ViewResult>(resultado);
        Assert.True(c.ModelState.ContainsKey("PreciosPorTamano[0].Monto"));
        Assert.Empty(db.Productos);
    }

    [Fact]
    public async Task CrearProducto_ConTamanoPermitidoParaLaCategoria_SeGuarda()
    {
        await using var db = TestServices.CrearContextoInMemory();
        var (_, almuerzo, mediano) = await SembrarAsync(db);

        var resultado = await Controlador(db).CrearProducto(ProductoConTamano(almuerzo, mediano));

        Assert.IsType<RedirectToActionResult>(resultado);
        Assert.Single(db.Productos);
    }

    [Fact]
    public async Task CrearProducto_NoOfreceTamanosDeBebida()
    {
        await using var db = TestServices.CrearContextoInMemory();
        var (_, _, _) = await SembrarAsync(db);
        db.Tamanos.Add(new Tamano { NombreTamano = "600ml", EsParaBebida = true });
        await db.SaveChangesAsync();

        var vista = Assert.IsType<ViewResult>(await TestServices.ConContexto(new AdministracionController(db)).CrearProducto());
        var modelo = Assert.IsType<ProductoFormViewModel>(vista.Model);

        Assert.Contains(modelo.PreciosPorTamano, p => p.NombreTamano == "Mediano");
        Assert.DoesNotContain(modelo.PreciosPorTamano, p => p.NombreTamano == "600ml");
    }

    [Fact]
    public async Task CrearCategoria_GuardaSusTamanosYDescartaLosDeBebida()
    {
        await using var db = TestServices.CrearContextoInMemory();
        var (_, _, mediano) = await SembrarAsync(db);
        var bebida = new Tamano { NombreTamano = "600ml", EsParaBebida = true };
        db.Tamanos.Add(bebida);
        await db.SaveChangesAsync();

        var resultado = await Controlador(db).CrearCategoria(new CategoriaFormViewModel
        {
            NombreCategoria = "Pizzas", TamanosPermitidos = [mediano.IdTamano, bebida.IdTamano]
        });

        Assert.IsType<RedirectToActionResult>(resultado);
        var pizzas = await db.Categorias.Include(c => c.TamanosPermitidos).SingleAsync(c => c.NombreCategoria == "Pizzas");
        var ids = pizzas.TamanosPermitidos.Select(t => t.IdTamano).ToList();
        Assert.Single(ids);
        Assert.Equal(mediano.IdTamano, ids[0]);
    }

    [Fact]
    public async Task EditarCategoria_PuedeAgregarYQuitarTamanosSinUso()
    {
        await using var db = TestServices.CrearContextoInMemory();
        var (desayuno, almuerzo, mediano) = await SembrarAsync(db);

        // Agregar un tamaño a Desayuno
        await Controlador(db).EditarCategoria(new CategoriaFormViewModel
        {
            IdCategoria = desayuno.IdCategoria, NombreCategoria = "Desayuno", TamanosPermitidos = [mediano.IdTamano]
        });
        Assert.Equal(2, await db.CategoriasTamanos.CountAsync());

        // Quitarlo de Almuerzo (no tiene productos que lo usen)
        db.ChangeTracker.Clear();
        var resultado = await Controlador(db).EditarCategoria(new CategoriaFormViewModel
        {
            IdCategoria = almuerzo.IdCategoria, NombreCategoria = "Almuerzo", TamanosPermitidos = []
        });

        Assert.IsType<RedirectToActionResult>(resultado);
        var restantes = await db.CategoriasTamanos.AsNoTracking().ToListAsync();
        Assert.Single(restantes);
        Assert.Equal(desayuno.IdCategoria, restantes[0].IdCategoria);
    }

    [Fact]
    public async Task EditarCategoria_QuitarUnTamanoQueUsanProductosActivos_SeRechaza()
    {
        await using var db = TestServices.CrearContextoInMemory();
        var (_, almuerzo, mediano) = await SembrarAsync(db);
        var producto = new Producto { NombreProducto = "Pizza", IdCategoria = almuerzo.IdCategoria, RequiereTamano = true };
        db.Precios.Add(new Precio { Producto = producto, IdTamano = mediano.IdTamano, MontoPrecio = 1500 });
        await db.SaveChangesAsync();
        var c = Controlador(db);

        var resultado = await c.EditarCategoria(new CategoriaFormViewModel
        {
            IdCategoria = almuerzo.IdCategoria, NombreCategoria = "Almuerzo", TamanosPermitidos = []
        });

        Assert.IsType<ViewResult>(resultado);
        Assert.True(c.ModelState.ContainsKey(nameof(CategoriaFormViewModel.TamanosPermitidos)));
        Assert.Single(await db.CategoriasTamanos.ToListAsync());
    }

    [Fact]
    public async Task CrearBebida_ConTamanoDeComida_SeRechaza()
    {
        await using var db = TestServices.CrearContextoInMemory();
        var (_, _, mediano) = await SembrarAsync(db);
        var c = Controlador(db);

        var resultado = await c.CrearBebida(new BebidaFormViewModel
        {
            NombreBebida = "Coca Cola", TipoBebida = "Gaseosa", IdTamano = mediano.IdTamano, Precio = 900
        });

        Assert.IsType<ViewResult>(resultado);
        Assert.True(c.ModelState.ContainsKey(nameof(BebidaFormViewModel.IdTamano)));
        Assert.Empty(db.Bebidas);
    }

    [Fact]
    public async Task EditarTamano_MarcarComoBebidaUnTamanoQueUsanProductos_SeRechaza()
    {
        await using var db = TestServices.CrearContextoInMemory();
        var (_, almuerzo, mediano) = await SembrarAsync(db);
        var producto = new Producto { NombreProducto = "Pizza", IdCategoria = almuerzo.IdCategoria, RequiereTamano = true };
        db.Precios.Add(new Precio { Producto = producto, IdTamano = mediano.IdTamano, MontoPrecio = 1500 });
        await db.SaveChangesAsync();
        var c = Controlador(db);

        var resultado = await c.EditarTamano(new TamanoFormViewModel
        {
            IdTamano = mediano.IdTamano, NombreTamano = "Mediano", EsParaBebida = true
        });

        Assert.IsType<ViewResult>(resultado);
        Assert.True(c.ModelState.ContainsKey(nameof(TamanoFormViewModel.EsParaBebida)));
        Assert.False((await db.Tamanos.AsNoTracking().SingleAsync()).EsParaBebida);
    }

    [Fact]
    public async Task EditarTamano_MarcarComoComidaUnTamanoQueUsanBebidas_SeRechaza()
    {
        await using var db = TestServices.CrearContextoInMemory();
        var tam = new Tamano { NombreTamano = "600ml", EsParaBebida = true };
        db.Bebidas.Add(new Bebida { NombreBebida = "Coca Cola", TipoBebida = "Gaseosa", Precio = 900, Tamano = tam });
        await db.SaveChangesAsync();
        var c = Controlador(db);

        var resultado = await c.EditarTamano(new TamanoFormViewModel
        {
            IdTamano = tam.IdTamano, NombreTamano = "600ml", EsParaBebida = false
        });

        Assert.IsType<ViewResult>(resultado);
        Assert.True(c.ModelState.ContainsKey(nameof(TamanoFormViewModel.EsParaBebida)));
    }
}
