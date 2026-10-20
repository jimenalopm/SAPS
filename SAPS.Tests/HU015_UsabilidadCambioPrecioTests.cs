using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using SAPS.Tests.Helpers;
using SAPS.Web.Controllers;
using SAPS.Web.Data;
using SAPS.Web.Models.Catalogo;
using SAPS.Web.Services.Auditoria;

namespace SAPS.Tests;

/// <summary>
/// Inspección de usabilidad de HU-015 (Sprint 2): el mensaje al guardar dice qué precio cambió (H1/H9) y,
/// si la base de datos rechaza el guardado, el formulario muestra un mensaje en lenguaje natural (H9).
/// La confirmación antes de guardar (H3) es de la pantalla (wwwroot/js/confirmar-precio.js).
/// </summary>
public class HU015_UsabilidadCambioPrecioTests
{
    // ---------- Descripción del cambio ----------

    [Fact]
    public void DescribirCambioPrecios_PrecioUnico_MuestraAnteriorYNuevo()
    {
        var texto = AdministracionController.DescribirCambioPrecios([(null, 1500)], [(null, 1800)], _ => null);

        Assert.Equal($"{Colones(1500)} → {Colones(1800)}", texto);
    }

    [Fact]
    public void DescribirCambioPrecios_PorTamano_NombraCadaTamanoNuevoYQuitado()
    {
        var nombres = new Dictionary<int, string> { [1] = "Pequeño", [2] = "Grande", [3] = "Mediano" };

        var texto = AdministracionController.DescribirCambioPrecios(
            [(1, 1200), (2, 2000), (3, 1600)],
            [(1, 1200), (2, 2200), (4, 2500)],
            id => id is int t && nombres.TryGetValue(t, out var n) ? n : "Familiar");

        Assert.Equal($"Grande: {Colones(2000)} → {Colones(2200)}; Familiar: nuevo {Colones(2500)}; Mediano: se quitó {Colones(1600)}", texto);
    }

    [Fact]
    public void DescribirCambioPrecios_SinCambios_DevuelveNull()
    {
        Assert.Null(AdministracionController.DescribirCambioPrecios([(null, 1500)], [(null, 1500)], _ => null));
    }

    [Fact]
    public void MensajeProductoActualizado_SinCambioDePrecio_EsElMensajeDeSiempre()
    {
        Assert.Equal("Producto actualizado correctamente.", AdministracionController.MensajeProductoActualizado("Casado", null));
    }

    // ---------- Mensaje al guardar ----------

    [Fact]
    public async Task EditarProducto_ConCambioDePrecio_ElMensajeDiceElPrecioYQueQuedoEnElHistorial()
    {
        await using var db = TestServices.CrearContextoInMemory();
        var (cat, producto) = await SembrarProductoAsync(db, 1500);
        var controlador = Controlador(db);

        await controlador.EditarProducto(Formulario(cat, producto, 1800));

        var mensaje = Assert.IsType<string>(controlador.TempData["Mensaje"]);
        Assert.Contains("«Casado» actualizado correctamente", mensaje);
        Assert.Contains($"{Colones(1500)} → {Colones(1800)}", mensaje);
        Assert.Contains("historial de precios", mensaje);
    }

    [Fact]
    public async Task EditarProducto_PrecioUnicoConFilasDeTamanoVacias_NoFallaAlArmarElMensaje()
    {
        // Así llega el formulario real con precio único: las filas de tamaño deshabilitadas se envían con IdTamano 0.
        await using var db = TestServices.CrearContextoInMemory();
        var (cat, producto) = await SembrarProductoAsync(db, 1500);
        var controlador = Controlador(db);
        var formulario = Formulario(cat, producto, 1800);
        formulario.PreciosPorTamano = [new PrecioPorTamanoInput(), new PrecioPorTamanoInput()];

        var resultado = await controlador.EditarProducto(formulario);

        Assert.IsType<RedirectToActionResult>(resultado);
        Assert.Contains($"{Colones(1500)} → {Colones(1800)}", Assert.IsType<string>(controlador.TempData["Mensaje"]));
    }

    [Fact]
    public async Task EditarProducto_SinCambioDePrecio_NoMencionaElPrecio()
    {
        await using var db = TestServices.CrearContextoInMemory();
        var (cat, producto) = await SembrarProductoAsync(db, 1500);
        var controlador = Controlador(db);

        await controlador.EditarProducto(Formulario(cat, producto, 1500));

        Assert.Equal("Producto actualizado correctamente.", controlador.TempData["Mensaje"]);
    }

    [Fact]
    public async Task EditarBebida_ConCambioDePrecio_ElMensajeDiceElPrecio()
    {
        await using var db = TestServices.CrearContextoInMemory();
        var bebida = await SembrarBebidaAsync(db, 800);
        var controlador = Controlador(db);

        await controlador.EditarBebida(new BebidaFormViewModel
        {
            IdBebida = bebida.IdBebida, NombreBebida = "Naranjada", TipoBebida = "Frescos", Precio = 900,
        });

        var mensaje = Assert.IsType<string>(controlador.TempData["Mensaje"]);
        Assert.Contains("«Naranjada» actualizada correctamente", mensaje);
        Assert.Contains($"{Colones(800)} → {Colones(900)}", mensaje);
    }

    // ---------- Error al guardar ----------

    [Fact]
    public async Task EditarProducto_SiLaBaseRechazaElGuardado_MuestraUnMensajeClaroEnElFormulario()
    {
        await using var db = TestServices.CrearContextoInMemory();
        var (cat, producto) = await SembrarProductoAsync(db, 1500);
        var controlador = Controlador(db);
        db.SavingChanges += (_, _) => throw new DbUpdateException("String or binary data would be truncated.");

        var resultado = await controlador.EditarProducto(Formulario(cat, producto, 1800));

        Assert.IsType<ViewResult>(resultado);
        var error = Assert.Single(controlador.ModelState[string.Empty]!.Errors);
        Assert.Equal(AdministracionController.MensajeErrorGuardado, error.ErrorMessage);
        Assert.DoesNotContain("truncated", error.ErrorMessage);
        Assert.Null(controlador.TempData["Mensaje"]);
    }

    [Fact]
    public async Task EditarBebida_SiLaBaseRechazaElGuardado_MuestraUnMensajeClaroEnElFormulario()
    {
        await using var db = TestServices.CrearContextoInMemory();
        var bebida = await SembrarBebidaAsync(db, 800);
        var controlador = Controlador(db);
        db.SavingChanges += (_, _) => throw new DbUpdateException("fallo simulado");

        var resultado = await controlador.EditarBebida(new BebidaFormViewModel
        {
            IdBebida = bebida.IdBebida, NombreBebida = "Naranjada", TipoBebida = "Frescos", Precio = 900,
        });

        Assert.IsType<ViewResult>(resultado);
        Assert.Contains(controlador.ModelState[string.Empty]!.Errors, e => e.ErrorMessage == AdministracionController.MensajeErrorGuardado);
    }

    // ---------- Apoyo ----------

    private static string Colones(int monto) => PresentacionBitacora.Colones(monto.ToString())!;

    private static ProductoFormViewModel Formulario(Categoria cat, Producto producto, int precio) => new()
    {
        IdProducto = producto.IdProducto, NombreProducto = "Casado", IdCategoria = cat.IdCategoria, PrecioUnico = precio,
    };

    private static AdministracionController Controlador(ApplicationDbContext db)
    {
        var controlador = TestServices.ConContexto(new AdministracionController(db, new SinContexto()), "POST");
        controlador.ControllerContext.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, "u-9"), new Claim(ClaimTypes.Name, "ADM001"), new Claim(ClaimTypes.Role, "Administrador")],
            "Prueba"));
        return controlador;
    }

    private static async Task<(Categoria cat, Producto producto)> SembrarProductoAsync(ApplicationDbContext db, int monto)
    {
        var cat = new Categoria { NombreCategoria = "Almuerzo" };
        db.Categorias.Add(cat);
        await db.SaveChangesAsync();
        var producto = new Producto { NombreProducto = "Casado", IdCategoria = cat.IdCategoria };
        producto.Precios.Add(new Precio { MontoPrecio = monto, FechaVigenciaDesde = new DateOnly(2026, 1, 1) });
        db.Productos.Add(producto);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        return (cat, producto);
    }

    private static async Task<Bebida> SembrarBebidaAsync(ApplicationDbContext db, int precio)
    {
        var bebida = new Bebida { NombreBebida = "Naranjada", TipoBebida = "Frescos", Precio = precio };
        db.Bebidas.Add(bebida);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        return bebida;
    }

    /// <summary>InMemory no tiene CONTEXT_INFO: el contexto de auditoría no hace nada.</summary>
    private sealed class SinContexto : IContextoAuditoria
    {
        public Task EstablecerAsync(DatabaseFacade baseDatos, ClaimsPrincipal usuario, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task LimpiarAsync(DatabaseFacade baseDatos, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
