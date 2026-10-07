using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using SAPS.Tests.Helpers;
using SAPS.Web.Controllers;
using SAPS.Web.Models.Catalogo;

namespace SAPS.Tests;

/// <summary>
/// HU-007 | Validaciones de nombres del catálogo: largo máximo y mínimo, caracteres permitidos,
/// espacios sobrantes y duplicados de productos y bebidas.
/// </summary>
public class ValidacionesNombresHU007Tests
{
    private static List<ValidationResult> Validar(object modelo)
    {
        var resultados = new List<ValidationResult>();
        Validator.TryValidateObject(modelo, new ValidationContext(modelo), resultados, validateAllProperties: true);
        return resultados;
    }

    private static ProductoFormViewModel Producto(string nombre) =>
        new() { NombreProducto = nombre, IdCategoria = 1, PrecioUnico = 1000 };

    [Theory]
    [InlineData("Pinto con 2 acomp.")]
    [InlineData("Café con leche")]
    [InlineData("Piña colada (grande)")]
    [InlineData("Arroz c/s tortilla")]
    [InlineData("Pan 100%")]
    public void NombreProducto_Valido_NoGeneraErrores(string nombre) =>
        Assert.Empty(Validar(Producto(nombre)));

    [Fact]
    public void NombreProducto_Mas_De_60_Caracteres_SeRechaza()
    {
        var errores = Validar(Producto(new string('a', 61)));
        Assert.Contains(errores, e => e.MemberNames.Contains(nameof(ProductoFormViewModel.NombreProducto)));
    }

    [Fact]
    public void NombreProducto_Con_60_Caracteres_SeAcepta() =>
        Assert.Empty(Validar(Producto(new string('a', 60))));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("ab")]
    [InlineData(" a ")]
    [InlineData("12345")]
    [InlineData("---")]
    [InlineData("Pizza 😀")]
    [InlineData("Casado <b>")]
    [InlineData("Casado; DROP")]
    public void NombreProducto_Invalido_SeRechaza(string nombre) =>
        Assert.NotEmpty(Validar(Producto(nombre)));

    [Fact]
    public void NombreBebida_Mas_De_60_Caracteres_SeRechaza()
    {
        var modelo = new BebidaFormViewModel { NombreBebida = new string('b', 61), Precio = 500 };
        Assert.Contains(Validar(modelo), e => e.MemberNames.Contains(nameof(BebidaFormViewModel.NombreBebida)));
    }

    [Fact]
    public void NombreCategoria_Corto_SeRechaza()
    {
        var modelo = new CategoriaFormViewModel { NombreCategoria = "ab" };
        Assert.NotEmpty(Validar(modelo));
    }

    [Theory]
    [InlineData("  Pinto   con   huevo  ", "Pinto con huevo")]
    [InlineData("Café", "Café")]
    [InlineData("", "")]
    public void Normalizar_QuitaEspaciosSobrantes(string entrada, string esperado) =>
        Assert.Equal(esperado, NombreCatalogoAttribute.Normalizar(entrada));

    // ---------- Duplicados ----------

    [Fact]
    public async Task CrearProducto_NombreRepetidoEnLaMismaCategoria_NoSeGuarda()
    {
        await using var db = TestServices.CrearContextoInMemory();
        var cat = new Categoria { NombreCategoria = "Desayuno" };
        db.Categorias.Add(cat);
        db.Productos.Add(new Producto { NombreProducto = "Pinto", Categoria = cat });
        await db.SaveChangesAsync();
        var c = TestServices.ConContexto(new AdministracionController(db), "POST");

        var resultado = await c.CrearProducto(new ProductoFormViewModel
        {
            NombreProducto = "  Pinto ", IdCategoria = cat.IdCategoria, PrecioUnico = 1100
        });

        Assert.IsType<ViewResult>(resultado);
        Assert.True(c.ModelState.ContainsKey(nameof(ProductoFormViewModel.NombreProducto)));
        Assert.Single(db.Productos);
    }

    [Fact]
    public async Task CrearProducto_MismoNombreEnOtraCategoria_SeGuarda()
    {
        await using var db = TestServices.CrearContextoInMemory();
        var desayuno = new Categoria { NombreCategoria = "Desayuno" };
        var almuerzo = new Categoria { NombreCategoria = "Almuerzo" };
        db.Categorias.AddRange(desayuno, almuerzo);
        db.Productos.Add(new Producto { NombreProducto = "Pinto", Categoria = desayuno });
        await db.SaveChangesAsync();
        var c = TestServices.ConContexto(new AdministracionController(db), "POST");

        var resultado = await c.CrearProducto(new ProductoFormViewModel
        {
            NombreProducto = "Pinto", IdCategoria = almuerzo.IdCategoria, PrecioUnico = 1500
        });

        Assert.IsType<RedirectToActionResult>(resultado);
        Assert.Equal(2, db.Productos.Count());
    }

    [Fact]
    public async Task CrearProducto_NombreDeProductoDesactivado_AvisaQueSePuedeReactivar()
    {
        await using var db = TestServices.CrearContextoInMemory();
        var cat = new Categoria { NombreCategoria = "Desayuno" };
        db.Categorias.Add(cat);
        db.Productos.Add(new Producto { NombreProducto = "Pinto", Categoria = cat, Activo = false });
        await db.SaveChangesAsync();
        var c = TestServices.ConContexto(new AdministracionController(db), "POST");

        await c.CrearProducto(new ProductoFormViewModel { NombreProducto = "Pinto", IdCategoria = cat.IdCategoria, PrecioUnico = 1100 });

        var mensaje = c.ModelState[nameof(ProductoFormViewModel.NombreProducto)]!.Errors.Single().ErrorMessage;
        Assert.Contains("desactivado", mensaje);
    }

    [Fact]
    public async Task EditarProducto_ConservarSuPropioNombre_NoCuentaComoDuplicado()
    {
        await using var db = TestServices.CrearContextoInMemory();
        var cat = new Categoria { NombreCategoria = "Desayuno" };
        var producto = new Producto { NombreProducto = "Pinto", Categoria = cat };
        db.Productos.Add(producto);
        db.Precios.Add(new Precio { Producto = producto, MontoPrecio = 1100 });
        await db.SaveChangesAsync();
        var c = TestServices.ConContexto(new AdministracionController(db), "POST");

        var resultado = await c.EditarProducto(new ProductoFormViewModel
        {
            IdProducto = producto.IdProducto, NombreProducto = "Pinto", IdCategoria = cat.IdCategoria, PrecioUnico = 1200
        });

        Assert.IsType<RedirectToActionResult>(resultado);
    }

    [Fact]
    public async Task CrearBebida_MismoNombreYTamano_NoSeGuarda()
    {
        await using var db = TestServices.CrearContextoInMemory();
        var tam = new Tamano { NombreTamano = "600ml" };
        db.Tamanos.Add(tam);
        db.Bebidas.Add(new Bebida { NombreBebida = "Coca Cola", TipoBebida = "Gaseosa", Precio = 900, Tamano = tam });
        await db.SaveChangesAsync();
        var c = TestServices.ConContexto(new AdministracionController(db), "POST");

        var resultado = await c.CrearBebida(new BebidaFormViewModel
        {
            NombreBebida = "Coca Cola", TipoBebida = "Gaseosa", IdTamano = tam.IdTamano, Precio = 900
        });

        Assert.IsType<ViewResult>(resultado);
        Assert.True(c.ModelState.ContainsKey(nameof(BebidaFormViewModel.NombreBebida)));
        Assert.Single(db.Bebidas);
    }

    [Fact]
    public async Task CrearBebida_MismoNombreEnOtroTamano_SeGuarda()
    {
        await using var db = TestServices.CrearContextoInMemory();
        var chico = new Tamano { NombreTamano = "355ml" };
        var grande = new Tamano { NombreTamano = "600ml" };
        db.Tamanos.AddRange(chico, grande);
        db.Bebidas.Add(new Bebida { NombreBebida = "Coca Cola", TipoBebida = "Gaseosa", Precio = 700, Tamano = chico });
        await db.SaveChangesAsync();
        var c = TestServices.ConContexto(new AdministracionController(db), "POST");

        var resultado = await c.CrearBebida(new BebidaFormViewModel
        {
            NombreBebida = "Coca Cola", TipoBebida = "Gaseosa", IdTamano = grande.IdTamano, Precio = 900
        });

        Assert.IsType<RedirectToActionResult>(resultado);
        Assert.Equal(2, db.Bebidas.Count());
    }
}
