using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SAPS.Tests.Helpers;
using SAPS.Web.Controllers;
using SAPS.Web.Data;
using SAPS.Web.Models.Catalogo;

namespace SAPS.Tests;

/// <summary>
/// HU-007 | Tipos de bebida administrables desde el catálogo: alta, edición, estado y su uso en bebidas.
/// </summary>
public class ValidacionesTiposBebidaHU007Tests
{
    private static AdministracionController Controlador(ApplicationDbContext db) =>
        TestServices.ConContexto(new AdministracionController(db), "POST");

    private static List<ValidationResult> Validar(object modelo)
    {
        var resultados = new List<ValidationResult>();
        Validator.TryValidateObject(modelo, new ValidationContext(modelo), resultados, validateAllProperties: true);
        return resultados;
    }

    [Fact]
    public async Task TiposIniciales_SonLosCuatroDeSiempre()
    {
        await using var db = TestServices.CrearContextoInMemory();

        var nombres = await db.TiposBebida.Select(t => t.NombreTipo).OrderBy(n => n).ToListAsync();

        Assert.Equal(new[] { "Embotellada", "Energizante", "Gaseosa", "Jugo" }, nombres);
    }

    // ---------- Nombre ----------

    [Theory]
    [InlineData("Café frío")]
    [InlineData("Jugo")]
    [InlineData("Té frío")]
    public void NombreTipo_Valido_NoGeneraErrores(string nombre) =>
        Assert.Empty(Validar(new TipoBebidaFormViewModel { NombreTipo = nombre }));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Té")]
    [InlineData("123")]
    [InlineData("Jugo<script>")]
    [InlineData("Un nombre de tipo demasiado largo")]
    public void NombreTipo_Invalido_SeRechaza(string nombre) =>
        Assert.NotEmpty(Validar(new TipoBebidaFormViewModel { NombreTipo = nombre }));

    // ---------- Alta y edición ----------

    [Fact]
    public async Task CrearTipoBebida_Valido_SeGuardaYApareceParaElegir()
    {
        await using var db = TestServices.CrearContextoInMemory();
        var c = Controlador(db);

        var resultado = await c.CrearTipoBebida(new TipoBebidaFormViewModel { NombreTipo = "  Café   frío " });

        Assert.IsType<RedirectToActionResult>(resultado);
        Assert.Contains(await db.TiposBebida.ToListAsync(), t => t.NombreTipo == "Café frío" && t.Activo);

        var form = Assert.IsType<ViewResult>(await c.CrearBebida());
        var modelo = Assert.IsType<BebidaFormViewModel>(form.Model);
        Assert.Contains(modelo.TiposDisponibles, t => t.NombreTipo == "Café frío");
    }

    [Fact]
    public async Task CrearTipoBebida_Duplicado_SeRechaza()
    {
        await using var db = TestServices.CrearContextoInMemory();
        var c = Controlador(db);

        var resultado = await c.CrearTipoBebida(new TipoBebidaFormViewModel { NombreTipo = " Gaseosa " });

        Assert.IsType<ViewResult>(resultado);
        Assert.True(c.ModelState.ContainsKey(nameof(TipoBebidaFormViewModel.NombreTipo)));
        Assert.Equal(4, db.TiposBebida.Count());
    }

    [Fact]
    public async Task EditarTipoBebida_CambiarNombre_ActualizaLasBebidasQueLoUsan()
    {
        await using var db = TestServices.CrearContextoInMemory();
        db.Bebidas.Add(new Bebida { NombreBebida = "Naranjada", TipoBebida = "Jugo", Precio = 800 });
        await db.SaveChangesAsync();
        var jugo = await db.TiposBebida.SingleAsync(t => t.NombreTipo == "Jugo");

        var resultado = await Controlador(db).EditarTipoBebida(new TipoBebidaFormViewModel
        {
            IdTipoBebida = jugo.IdTipoBebida, NombreTipo = "Jugo natural",
        });

        Assert.IsType<RedirectToActionResult>(resultado);
        Assert.Equal("Jugo natural", (await db.Bebidas.AsNoTracking().SingleAsync()).TipoBebida);
    }

    [Fact]
    public async Task EditarTipoBebida_NombreDeOtroTipo_SeRechaza()
    {
        await using var db = TestServices.CrearContextoInMemory();
        var jugo = await db.TiposBebida.SingleAsync(t => t.NombreTipo == "Jugo");
        var c = Controlador(db);

        var resultado = await c.EditarTipoBebida(new TipoBebidaFormViewModel { IdTipoBebida = jugo.IdTipoBebida, NombreTipo = "Gaseosa" });

        Assert.IsType<ViewResult>(resultado);
        Assert.True(c.ModelState.ContainsKey(nameof(TipoBebidaFormViewModel.NombreTipo)));
    }

    // ---------- Estado ----------

    [Fact]
    public async Task CambiarEstadoTipoBebida_ConBebidasActivas_NoSeDesactiva()
    {
        await using var db = TestServices.CrearContextoInMemory();
        db.Bebidas.Add(new Bebida { NombreBebida = "Naranjada", TipoBebida = "Jugo", Precio = 800 });
        await db.SaveChangesAsync();
        var jugo = await db.TiposBebida.SingleAsync(t => t.NombreTipo == "Jugo");
        var c = Controlador(db);

        await c.CambiarEstadoTipoBebida(jugo.IdTipoBebida);

        Assert.True((await db.TiposBebida.AsNoTracking().SingleAsync(t => t.NombreTipo == "Jugo")).Activo);
        Assert.NotNull(c.TempData["Error"]);
    }

    [Fact]
    public async Task CambiarEstadoTipoBebida_SinBebidasActivas_SiSeDesactiva()
    {
        await using var db = TestServices.CrearContextoInMemory();
        var jugo = await db.TiposBebida.SingleAsync(t => t.NombreTipo == "Jugo");

        await Controlador(db).CambiarEstadoTipoBebida(jugo.IdTipoBebida);

        Assert.False((await db.TiposBebida.AsNoTracking().SingleAsync(t => t.NombreTipo == "Jugo")).Activo);
    }

    [Fact]
    public async Task CambiarEstadoBebida_ActivarConTipoInactivo_NoSeActiva()
    {
        await using var db = TestServices.CrearContextoInMemory();
        var jugo = await db.TiposBebida.SingleAsync(t => t.NombreTipo == "Jugo");
        jugo.Activo = false;
        var beb = new Bebida { NombreBebida = "Naranjada", TipoBebida = "Jugo", Precio = 800, Activo = false };
        db.Bebidas.Add(beb);
        await db.SaveChangesAsync();
        var c = Controlador(db);

        await c.CambiarEstadoBebida(beb.IdBebida);

        Assert.False((await db.Bebidas.AsNoTracking().SingleAsync()).Activo);
        Assert.NotNull(c.TempData["Error"]);
    }

    // ---------- Uso en bebidas ----------

    [Fact]
    public async Task CrearBebida_ConTipoInactivo_SeRechaza()
    {
        await using var db = TestServices.CrearContextoInMemory();
        var jugo = await db.TiposBebida.SingleAsync(t => t.NombreTipo == "Jugo");
        jugo.Activo = false;
        await db.SaveChangesAsync();
        var c = Controlador(db);

        var resultado = await c.CrearBebida(new BebidaFormViewModel { NombreBebida = "Naranjada", TipoBebida = "Jugo", Precio = 800 });

        Assert.IsType<ViewResult>(resultado);
        Assert.True(c.ModelState.ContainsKey(nameof(BebidaFormViewModel.TipoBebida)));
        Assert.Empty(db.Bebidas);
    }

    [Fact]
    public async Task EditarBebida_ConservaSuTipoAunqueEsteInactivo()
    {
        await using var db = TestServices.CrearContextoInMemory();
        var beb = new Bebida { NombreBebida = "Naranjada", TipoBebida = "Jugo", Precio = 800 };
        db.Bebidas.Add(beb);
        var jugo = await db.TiposBebida.SingleAsync(t => t.NombreTipo == "Jugo");
        jugo.Activo = false;
        await db.SaveChangesAsync();

        var resultado = await Controlador(db).EditarBebida(new BebidaFormViewModel
        {
            IdBebida = beb.IdBebida, NombreBebida = "Naranjada", TipoBebida = "Jugo", Precio = 900,
        });

        Assert.IsType<RedirectToActionResult>(resultado);
        Assert.Equal(900, (await db.Bebidas.AsNoTracking().SingleAsync()).Precio);
    }
}
