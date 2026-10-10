using Microsoft.AspNetCore.Mvc;
using SAPS.Tests.Helpers;
using SAPS.Web.Controllers;
using SAPS.Web.Models.Catalogo;
namespace SAPS.Tests;
public class MejorasCatalogoTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Filtros_SeparanActivosDeInactivosEnLasCuatroSecciones(bool inactivos)
    {
        await using var db = TestServices.CrearContextoInMemory();
        var cat = new Categoria { NombreCategoria = "Activa", Activo = true };
        db.Categorias.AddRange(cat, new Categoria { NombreCategoria = "Inactiva", Activo = false });
        db.Tamanos.AddRange(new Tamano { NombreTamano = "Activo", Activo = true }, new Tamano { NombreTamano = "Inactivo", Activo = false });
        db.Productos.AddRange(new Producto { NombreProducto = "Activo", Categoria = cat, Activo = true }, new Producto { NombreProducto = "Inactivo", Categoria = cat, Activo = false });
        db.Bebidas.AddRange(new Bebida { TipoBebida = "Gaseosa", NombreBebida = "Activa", Precio = 100, Activo = true }, new Bebida { TipoBebida = "Gaseosa", NombreBebida = "Inactiva", Precio = 100, Activo = false });
        await db.SaveChangesAsync();
        var c = TestServices.ConContexto(new AdministracionController(db));
        var v = Assert.IsType<ViewResult>(await c.Catalogo(catInc: inactivos, tamInc: inactivos, prodInc: inactivos, bebInc: inactivos));
        var m = Assert.IsType<CatalogoIndexViewModel>(v.Model);
        Assert.Equal(!inactivos, Assert.Single(m.Categorias).Activo);
        Assert.Equal(!inactivos, Assert.Single(m.Tamanos).Activo);
        Assert.Equal(!inactivos, Assert.Single(m.Productos).Activo);
        Assert.Equal(!inactivos, Assert.Single(m.Bebidas).Activo);
    }
    [Theory]
    [InlineData(-20)]
    [InlineData(0)]
    [InlineData(null)]
    public async Task PrecioInvalido_ErrorEnLaCasillaDelTamanoSinGuardar(int? monto)
    {
        await using var db = TestServices.CrearContextoInMemory();
        var cat = new Categoria { NombreCategoria = "Prueba" }; var tam = new Tamano { NombreTamano = "Grande" };
        db.AddRange(cat, tam); await db.SaveChangesAsync();
        var c = TestServices.ConContexto(new AdministracionController(db), "POST");
        var m = new ProductoFormViewModel { NombreProducto = "Prueba", IdCategoria = cat.IdCategoria, RequiereTamano = true,
            PreciosPorTamano = [new() { IdTamano = tam.IdTamano, Incluir = true, Monto = monto }] };
        Assert.IsType<ViewResult>(await c.CrearProducto(m));
        var error = Assert.Single(c.ModelState["PreciosPorTamano[0].Monto"]!.Errors);
        Assert.Equal(monto is null ? "Indique el precio de este tamaño." : "No se pueden colocar valores negativos o iguales a cero.", error.ErrorMessage);
        Assert.Empty(db.Productos);
    }
    [Fact]
    public async Task Reactivacion_NombraElRegistroEnCadaSeccion()
    {
        await using var db = TestServices.CrearContextoInMemory();
        var cat = new Categoria { NombreCategoria = "Almuerzos", Activo = false }; var tam = new Tamano { NombreTamano = "Grande", Activo = false };
        var prod = new Producto { NombreProducto = "Casado", Categoria = cat, Activo = false }; var beb = new Bebida { TipoBebida = "Gaseosa", NombreBebida = "Agua", Precio = 100, Activo = false };
        db.AddRange(cat,tam,prod,beb); await db.SaveChangesAsync();
        var c = TestServices.ConContexto(new AdministracionController(db), "POST");
        await c.CambiarEstadoCategoria(cat.IdCategoria); Assert.Equal("Categoría «Almuerzos» activada.",c.TempData["Mensaje"]);
        await c.CambiarEstadoTamano(tam.IdTamano); Assert.Equal("Tamaño «Grande» activado.",c.TempData["Mensaje"]);
        await c.CambiarEstadoProducto(prod.IdProducto); Assert.Equal("Producto «Casado» activado.",c.TempData["Mensaje"]);
        await c.CambiarEstadoBebida(beb.IdBebida); Assert.Equal("Bebida «Agua» activada.",c.TempData["Mensaje"]);
    }
}
