using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using SAPS.Tests.Helpers;
using SAPS.Web.Controllers;
using SAPS.Web.Data;
using SAPS.Web.Models.Catalogo;

namespace SAPS.Tests;

/// <summary>
/// HU-007 | Nombre de tamaño, búsqueda con espacios sobrantes y orden de precios
/// entre tamaños de una misma bebida.
/// </summary>
public class ValidacionesPendientesHU007Tests
{
    private static AdministracionController Controlador(ApplicationDbContext db) =>
        TestServices.ConContexto(new AdministracionController(db), "POST");

    private static List<ValidationResult> Validar(object modelo)
    {
        var resultados = new List<ValidationResult>();
        Validator.TryValidateObject(modelo, new ValidationContext(modelo), resultados, validateAllProperties: true);
        return resultados;
    }

    // ---------- Nombre del tamaño ----------

    [Theory]
    [InlineData("Grande")]
    [InlineData("600ml")]
    [InlineData("2.5L")]
    [InlineData("1L")]
    public void NombreTamano_Valido_NoGeneraErrores(string nombre) =>
        Assert.Empty(Validar(new TamanoFormViewModel { NombreTamano = nombre }));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("G")]
    [InlineData("123")]
    [InlineData("Gran<de>")]
    public void NombreTamano_Invalido_SeRechaza(string nombre) =>
        Assert.NotEmpty(Validar(new TamanoFormViewModel { NombreTamano = nombre }));

    [Fact]
    public async Task CrearTamano_EspaciosSobrantes_SeNormalizanYDetectanDuplicado()
    {
        await using var db = TestServices.CrearContextoInMemory();
        db.Tamanos.Add(new Tamano { NombreTamano = "Extra grande" });
        await db.SaveChangesAsync();
        var c = Controlador(db);

        var resultado = await c.CrearTamano(new TamanoFormViewModel { NombreTamano = "  Extra   grande " });

        Assert.IsType<ViewResult>(resultado);
        Assert.True(c.ModelState.ContainsKey(nameof(TamanoFormViewModel.NombreTamano)));
        Assert.Single(db.Tamanos);
    }

    // ---------- Búsqueda con espacios ----------

    [Fact]
    public async Task Catalogo_BusquedaConEspaciosSobrantes_EncuentraElProducto()
    {
        await using var db = TestServices.CrearContextoInMemory();
        db.Productos.Add(new Producto { NombreProducto = "Pizza de queso", Categoria = new Categoria { NombreCategoria = "Pizzas" } });
        await db.SaveChangesAsync();

        var vista = Assert.IsType<ViewResult>(await Controlador(db).Catalogo("productos", prodQ: "  Pizza   de  "));
        var vm = Assert.IsType<CatalogoIndexViewModel>(vista.Model);

        Assert.Single(vm.Productos);
    }

    // ---------- Orden de precios entre tamaños de una bebida ----------

    private static async Task<Tamano> SembrarBebidaAsync(ApplicationDbContext db)
    {
        var pequeno = new Tamano { NombreTamano = "600ml", EsParaBebida = true };
        var grande = new Tamano { NombreTamano = "3L", EsParaBebida = true };
        db.Bebidas.Add(new Bebida { NombreBebida = "Coca Cola", TipoBebida = "Gaseosa", Precio = 1000, Tamano = pequeno });
        db.Tamanos.Add(grande);
        await db.SaveChangesAsync();
        return grande;
    }

    [Fact]
    public async Task CrearBebida_TamanoMayorMasBaratoQueElMenor_SeRechaza()
    {
        await using var db = TestServices.CrearContextoInMemory();
        var grande = await SembrarBebidaAsync(db);
        var c = Controlador(db);

        var resultado = await c.CrearBebida(new BebidaFormViewModel
        {
            NombreBebida = "Coca Cola", TipoBebida = "Gaseosa", IdTamano = grande.IdTamano, Precio = 500,
        });

        Assert.IsType<ViewResult>(resultado);
        Assert.True(c.ModelState.ContainsKey(nameof(BebidaFormViewModel.Precio)));
        Assert.Single(db.Bebidas);
    }

    [Fact]
    public async Task CrearBebida_TamanoMayorMasCaro_SeGuarda()
    {
        await using var db = TestServices.CrearContextoInMemory();
        var grande = await SembrarBebidaAsync(db);

        var resultado = await Controlador(db).CrearBebida(new BebidaFormViewModel
        {
            NombreBebida = "Coca Cola", TipoBebida = "Gaseosa", IdTamano = grande.IdTamano, Precio = 2500,
        });

        Assert.IsType<RedirectToActionResult>(resultado);
        Assert.Equal(2, db.Bebidas.Count());
    }
}
