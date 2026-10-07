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
        var mediano = new Tamano { NombreTamano = "Mediano", Orden = 2 };
        var grande = new Tamano { NombreTamano = "Grande", Orden = 3 };
        db.AddRange(cat, mediano, grande);
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
    [InlineData(100_001)]
    public void Bebida_PrecioFueraDeRango_SeRechaza(int precio)
    {
        var modelo = new BebidaFormViewModel { NombreBebida = "Coca Cola", Precio = precio };
        var errores = new List<ValidationResult>();
        Validator.TryValidateObject(modelo, new ValidationContext(modelo), errores, true);
        Assert.Contains(errores, e => e.MemberNames.Contains(nameof(BebidaFormViewModel.Precio)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(100)]
    public void Tamano_OrdenFueraDeRango_SeRechaza(int orden)
    {
        var modelo = new TamanoFormViewModel { NombreTamano = "Grande", Orden = orden };
        var errores = new List<ValidationResult>();
        Validator.TryValidateObject(modelo, new ValidationContext(modelo), errores, true);
        Assert.Contains(errores, e => e.MemberNames.Contains(nameof(TamanoFormViewModel.Orden)));
    }

    [Fact]
    public async Task CrearTamano_SinOrden_NoSeGuarda()
    {
        await using var db = TestServices.CrearContextoInMemory();
        var c = TestServices.ConContexto(new AdministracionController(db), "POST");

        var resultado = await c.CrearTamano(new TamanoFormViewModel { NombreTamano = "Extra grande" });

        Assert.IsType<ViewResult>(resultado);
        Assert.True(c.ModelState.ContainsKey(nameof(TamanoFormViewModel.Orden)));
        Assert.Empty(db.Tamanos);
    }

    [Fact]
    public async Task CrearTamano_ConOrden_SeGuardaConEseOrden()
    {
        await using var db = TestServices.CrearContextoInMemory();
        var c = TestServices.ConContexto(new AdministracionController(db), "POST");

        var resultado = await c.CrearTamano(new TamanoFormViewModel { NombreTamano = "Extra grande", Orden = 4 });

        Assert.IsType<RedirectToActionResult>(resultado);
        Assert.Equal(4, db.Tamanos.Single().Orden);
    }
}
