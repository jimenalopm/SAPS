using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using SAPS.Tests.Helpers;
using SAPS.Web.Controllers;
using SAPS.Web.Models.Catalogo;

namespace SAPS.Tests;

/// <summary>
/// HU-007 | Validaciones de precios y tamaños: tope de precio, orden entre tamaños
/// (un tamaño mayor no cuesta menos que uno menor) y orden obligatorio del tamaño.
/// </summary>
public class ValidacionesPreciosHU007Tests
{
    private static async Task<(Categoria cat, Tamano mediano, Tamano grande)> SembrarPizzaAsync(SAPS.Web.Data.ApplicationDbContext db)
    {
        var cat = new Categoria { NombreCategoria = "Almuerzo" };
        var mediano = new Tamano { NombreTamano = "Mediano" };
        var grande = new Tamano { NombreTamano = "Grande" };
        db.AddRange(cat, mediano, grande);
        await db.SaveChangesAsync();
        db.CategoriasTamanos.AddRange(
            new CategoriaTamano { IdCategoria = cat.IdCategoria, IdTamano = mediano.IdTamano },
            new CategoriaTamano { IdCategoria = cat.IdCategoria, IdTamano = grande.IdTamano });
        await db.SaveChangesAsync();
        return (cat, mediano, grande);
    }

    private static ProductoFormViewModel Pizza(Categoria cat, Tamano mediano, int precioMediano, Tamano grande, int precioGrande) => new()
    {
        NombreProducto = "Pizza",
        IdCategoria = cat.IdCategoria,
        RequiereTamano = true,
        PreciosPorTamano =
        [
            new PrecioPorTamanoInput { IdTamano = mediano.IdTamano, Incluir = true, Monto = precioMediano },
            new PrecioPorTamanoInput { IdTamano = grande.IdTamano, Incluir = true, Monto = precioGrande },
        ]
    };

    [Fact]
    public async Task CrearProducto_TamanoGrandeMasBaratoQueMediano_NoSeGuarda()
    {
        await using var db = TestServices.CrearContextoInMemory();
        var (cat, mediano, grande) = await SembrarPizzaAsync(db);
        var c = TestServices.ConContexto(new AdministracionController(db), "POST");

        var resultado = await c.CrearProducto(Pizza(cat, mediano, 1500, grande, 500));

        Assert.IsType<ViewResult>(resultado);
        var error = c.ModelState["PreciosPorTamano[1].Monto"]!.Errors.Single().ErrorMessage;
        Assert.Contains("Grande", error);
        Assert.Contains("Mediano", error);
        Assert.Empty(db.Productos);
    }

    [Fact]
    public async Task CrearProducto_TamanoGrandeMasCaro_SeGuarda()
    {
        await using var db = TestServices.CrearContextoInMemory();
        var (cat, mediano, grande) = await SembrarPizzaAsync(db);
        var c = TestServices.ConContexto(new AdministracionController(db), "POST");

        var resultado = await c.CrearProducto(Pizza(cat, mediano, 1500, grande, 2500));

        Assert.IsType<RedirectToActionResult>(resultado);
        Assert.Single(db.Productos);
    }

    [Fact]
    public async Task CrearProducto_TamanosConElMismoPrecio_SeGuarda()
    {
        await using var db = TestServices.CrearContextoInMemory();
        var (cat, mediano, grande) = await SembrarPizzaAsync(db);
        var c = TestServices.ConContexto(new AdministracionController(db), "POST");

        var resultado = await c.CrearProducto(Pizza(cat, mediano, 1500, grande, 1500));

        Assert.IsType<RedirectToActionResult>(resultado);
    }

    [Fact]
    public async Task CrearProducto_ValidaElOrdenSinImportarComoLleguenLosTamanos()
    {
        await using var db = TestServices.CrearContextoInMemory();
        var (cat, mediano, grande) = await SembrarPizzaAsync(db);
        var c = TestServices.ConContexto(new AdministracionController(db), "POST");
        // Grande se envía primero en la lista, pero igual debe ser mayor o igual que Mediano.
        var modelo = Pizza(cat, mediano, 1500, grande, 500);
        modelo.PreciosPorTamano.Reverse();

        var resultado = await c.CrearProducto(modelo);

        Assert.IsType<ViewResult>(resultado);
        Assert.True(c.ModelState.ContainsKey("PreciosPorTamano[0].Monto"));
    }

    [Fact]
    public async Task CrearProducto_PrecioUnicoSobreElMaximo_NoSeGuarda()
    {
        await using var db = TestServices.CrearContextoInMemory();
        var cat = new Categoria { NombreCategoria = "Almuerzo" };
        db.Categorias.Add(cat);
        await db.SaveChangesAsync();
        var c = TestServices.ConContexto(new AdministracionController(db), "POST");

        var resultado = await c.CrearProducto(new ProductoFormViewModel
        {
            NombreProducto = "Casado", IdCategoria = cat.IdCategoria, PrecioUnico = ReglasCatalogo.PrecioMaximo + 1
        });

        Assert.IsType<ViewResult>(resultado);
        Assert.True(c.ModelState.ContainsKey(nameof(ProductoFormViewModel.PrecioUnico)));
        Assert.Empty(db.Productos);
    }

    [Fact]
    public async Task CrearProducto_PrecioUnicoEnElMaximo_SeGuarda()
    {
        await using var db = TestServices.CrearContextoInMemory();
        var cat = new Categoria { NombreCategoria = "Almuerzo" };
        db.Categorias.Add(cat);
        await db.SaveChangesAsync();
        var c = TestServices.ConContexto(new AdministracionController(db), "POST");

        var resultado = await c.CrearProducto(new ProductoFormViewModel
        {
            NombreProducto = "Casado", IdCategoria = cat.IdCategoria, PrecioUnico = ReglasCatalogo.PrecioMaximo
        });

        Assert.IsType<RedirectToActionResult>(resultado);
    }

    [Fact]
    public async Task CrearProducto_PrecioDeUnTamanoSobreElMaximo_NoSeGuarda()
    {
        await using var db = TestServices.CrearContextoInMemory();
        var (cat, mediano, grande) = await SembrarPizzaAsync(db);
        var c = TestServices.ConContexto(new AdministracionController(db), "POST");

        var resultado = await c.CrearProducto(Pizza(cat, mediano, 1500, grande, ReglasCatalogo.PrecioMaximo + 1));

        Assert.IsType<ViewResult>(resultado);
        Assert.True(c.ModelState.ContainsKey("PreciosPorTamano[1].Monto"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(10_001)]
    public void Bebida_PrecioFueraDeRango_SeRechaza(int precio)
    {
        var modelo = new BebidaFormViewModel { NombreBebida = "Coca Cola", Precio = precio };
        var errores = new List<ValidationResult>();
        Validator.TryValidateObject(modelo, new ValidationContext(modelo), errores, true);
        Assert.Contains(errores, e => e.MemberNames.Contains(nameof(BebidaFormViewModel.Precio)));
    }

    [Fact]
    public async Task CrearProducto_TamanoConNombreDesconocido_NoSeComparaConLosDemas()
    {
        await using var db = TestServices.CrearContextoInMemory();
        var (cat, mediano, grande) = await SembrarPizzaAsync(db);
        var combo = new Tamano { NombreTamano = "Combo" }; // el sistema no sabe qué tan grande es
        db.Tamanos.Add(combo);
        await db.SaveChangesAsync();
        db.CategoriasTamanos.Add(new CategoriaTamano { IdCategoria = cat.IdCategoria, IdTamano = combo.IdTamano });
        await db.SaveChangesAsync();
        var c = TestServices.ConContexto(new AdministracionController(db), "POST");
        var modelo = Pizza(cat, mediano, 1500, grande, 2000);
        modelo.PreciosPorTamano.Add(new PrecioPorTamanoInput { IdTamano = combo.IdTamano, Incluir = true, Monto = 500 });

        var resultado = await c.CrearProducto(modelo);

        Assert.IsType<RedirectToActionResult>(resultado);
    }

    [Fact]
    public async Task CrearTamano_SeGuardaSinPedirNingunNumeroDeOrden()
    {
        await using var db = TestServices.CrearContextoInMemory();
        var c = TestServices.ConContexto(new AdministracionController(db), "POST");

        var resultado = await c.CrearTamano(new TamanoFormViewModel { NombreTamano = "Extra grande" });

        Assert.IsType<RedirectToActionResult>(resultado);
        Assert.Single(db.Tamanos);
    }
}
