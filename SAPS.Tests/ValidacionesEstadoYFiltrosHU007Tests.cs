using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SAPS.Tests.Helpers;
using SAPS.Web.Controllers;
using SAPS.Web.Data;
using SAPS.Web.Models.Catalogo;

namespace SAPS.Tests;

/// <summary>
/// HU-007 | Reglas de activación y desactivación (no dejar productos o bebidas sin categoría o
/// tamaño activos) y filtro de productos por categoría en el catálogo.
/// </summary>
public class ValidacionesEstadoYFiltrosHU007Tests
{
    private static AdministracionController Controlador(ApplicationDbContext db, string metodo = "POST") =>
        TestServices.ConContexto(new AdministracionController(db), metodo);

    [Fact]
    public async Task DesactivarCategoria_ConProductosActivos_SeRechaza()
    {
        await using var db = TestServices.CrearContextoInMemory();
        var cat = new Categoria { NombreCategoria = "Desayuno" };
        db.Productos.Add(new Producto { NombreProducto = "Pinto", Categoria = cat });
        await db.SaveChangesAsync();
        var c = Controlador(db);

        await c.CambiarEstadoCategoria(cat.IdCategoria);

        Assert.True((await db.Categorias.AsNoTracking().SingleAsync()).Activo);
        Assert.Contains("producto", (string)c.TempData["Error"]!);
        Assert.Null(c.TempData["Mensaje"]);
    }

    [Fact]
    public async Task DesactivarCategoria_SinProductosActivos_SeDesactiva()
    {
        await using var db = TestServices.CrearContextoInMemory();
        var cat = new Categoria { NombreCategoria = "Desayuno" };
        db.Productos.Add(new Producto { NombreProducto = "Pinto", Categoria = cat, Activo = false });
        await db.SaveChangesAsync();
        var c = Controlador(db);

        await c.CambiarEstadoCategoria(cat.IdCategoria);

        Assert.False((await db.Categorias.AsNoTracking().SingleAsync()).Activo);
        Assert.Null(c.TempData["Error"]);
    }

    [Fact]
    public async Task DesactivarTamano_UsadoPorProductosActivos_SeRechaza()
    {
        await using var db = TestServices.CrearContextoInMemory();
        var tam = new Tamano { NombreTamano = "Grande" };
        var producto = new Producto { NombreProducto = "Pizza", Categoria = new Categoria { NombreCategoria = "Almuerzo" }, RequiereTamano = true };
        db.Precios.Add(new Precio { Producto = producto, Tamano = tam, MontoPrecio = 2000 });
        await db.SaveChangesAsync();
        var c = Controlador(db);

        await c.CambiarEstadoTamano(tam.IdTamano);

        Assert.True((await db.Tamanos.AsNoTracking().SingleAsync()).Activo);
        Assert.NotNull(c.TempData["Error"]);
    }

    [Fact]
    public async Task DesactivarTamano_UsadoPorBebidasActivas_SeRechaza()
    {
        await using var db = TestServices.CrearContextoInMemory();
        var tam = new Tamano { NombreTamano = "600ml", EsParaBebida = true };
        db.Bebidas.Add(new Bebida { NombreBebida = "Coca Cola", TipoBebida = "Gaseosa", Precio = 900, Tamano = tam });
        await db.SaveChangesAsync();
        var c = Controlador(db);

        await c.CambiarEstadoTamano(tam.IdTamano);

        Assert.True((await db.Tamanos.AsNoTracking().SingleAsync()).Activo);
        Assert.NotNull(c.TempData["Error"]);
    }

    [Fact]
    public async Task DesactivarTamano_SinUso_SeDesactiva()
    {
        await using var db = TestServices.CrearContextoInMemory();
        var tam = new Tamano { NombreTamano = "Grande" };
        db.Tamanos.Add(tam);
        await db.SaveChangesAsync();

        await Controlador(db).CambiarEstadoTamano(tam.IdTamano);

        Assert.False((await db.Tamanos.AsNoTracking().SingleAsync()).Activo);
    }

    [Fact]
    public async Task ActivarProducto_ConCategoriaInactiva_SeRechaza()
    {
        await using var db = TestServices.CrearContextoInMemory();
        var cat = new Categoria { NombreCategoria = "Vieja", Activo = false };
        var prod = new Producto { NombreProducto = "Casado", Categoria = cat, Activo = false };
        db.Productos.Add(prod);
        await db.SaveChangesAsync();
        var c = Controlador(db);

        await c.CambiarEstadoProducto(prod.IdProducto);

        Assert.False((await db.Productos.AsNoTracking().SingleAsync()).Activo);
        Assert.NotNull(c.TempData["Error"]);
    }

    [Fact]
    public async Task ActivarBebida_ConTamanoInactivo_SeRechaza()
    {
        await using var db = TestServices.CrearContextoInMemory();
        var tam = new Tamano { NombreTamano = "3L", Activo = false, EsParaBebida = true };
        var beb = new Bebida { NombreBebida = "Coca Cola", TipoBebida = "Gaseosa", Precio = 2500, Tamano = tam, Activo = false };
        db.Bebidas.Add(beb);
        await db.SaveChangesAsync();
        var c = Controlador(db);

        await c.CambiarEstadoBebida(beb.IdBebida);

        Assert.False((await db.Bebidas.AsNoTracking().SingleAsync()).Activo);
        Assert.NotNull(c.TempData["Error"]);
    }

    [Fact]
    public async Task Catalogo_FiltraLosProductosPorCategoria()
    {
        await using var db = TestServices.CrearContextoInMemory();
        var desayuno = new Categoria { NombreCategoria = "Desayuno" };
        var almuerzo = new Categoria { NombreCategoria = "Almuerzo" };
        db.Productos.AddRange(
            new Producto { NombreProducto = "Pinto", Categoria = desayuno },
            new Producto { NombreProducto = "Huevos", Categoria = desayuno },
            new Producto { NombreProducto = "Casado", Categoria = almuerzo });
        await db.SaveChangesAsync();

        var vista = Assert.IsType<ViewResult>(await Controlador(db, "GET").Catalogo(tab: "productos", prodCat: desayuno.IdCategoria));
        var modelo = Assert.IsType<CatalogoIndexViewModel>(vista.Model);

        Assert.Equal(2, modelo.Productos.Count);
        Assert.All(modelo.Productos, p => Assert.Equal(desayuno.IdCategoria, p.IdCategoria));
        Assert.Equal(2, modelo.CategoriasFiltro.Count);
    }

    [Fact]
    public async Task Catalogo_BuscaProductosPorParteDelNombre()
    {
        await using var db = TestServices.CrearContextoInMemory();
        var cat = new Categoria { NombreCategoria = "Desayuno" };
        db.Productos.AddRange(
            new Producto { NombreProducto = "Pinto con huevo", Categoria = cat },
            new Producto { NombreProducto = "Café", Categoria = cat });
        await db.SaveChangesAsync();

        var vista = Assert.IsType<ViewResult>(await Controlador(db, "GET").Catalogo(tab: "productos", prodQ: "into"));
        var modelo = Assert.IsType<CatalogoIndexViewModel>(vista.Model);

        Assert.Equal("Pinto con huevo", Assert.Single(modelo.Productos).NombreProducto);
    }
}
