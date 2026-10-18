using Microsoft.AspNetCore.Mvc;
using SAPS.Tests.Helpers;
using SAPS.Web.Controllers;
using SAPS.Web.Data;
using SAPS.Web.Models.Auditoria;
using SAPS.Web.Services.Auditoria;

namespace SAPS.Tests;

/// <summary>
/// HU-015 criterio 6 (task 20): el evento "cambio de precio" se puede filtrar en la bitácora, también por producto,
/// y su detalle muestra el producto, el precio anterior y el nuevo, la diferencia y si fue un cambio directo en la BD.
/// </summary>
public class HU015_BitacoraCambioPrecioTests
{
    private static readonly DateTime Hoy = new(2026, 10, 16, 18, 0, 0, DateTimeKind.Utc);

    // ---------- Filtros ----------

    [Fact]
    public async Task FiltroPorCambioDePrecio_SoloMuestraCambiosDePrecio()
    {
        await using var db = await SembrarAsync();

        var modelo = await ListarAsync(db, new BitacoraFiltros { TipoEvento = TiposEventoBitacora.CambioPrecio });

        Assert.Equal(3, modelo.TotalRegistros);
        Assert.All(modelo.Filas, f => Assert.Equal(TiposEventoBitacora.CambioPrecio, f.TipoEvento));
    }

    [Fact]
    public async Task FiltroPorProducto_MuestraElHistorialDePreciosDeEseProductoEnTodosSusTamanos()
    {
        await using var db = await SembrarAsync();

        var modelo = await ListarAsync(db, new BitacoraFiltros { TipoEvento = TiposEventoBitacora.CambioPrecio, Texto = "Casado" });

        Assert.Equal(2, modelo.TotalRegistros);
        Assert.All(modelo.Filas, f => Assert.StartsWith("Casado", f.DetallePrincipal));
    }

    [Fact]
    public async Task FiltroPorCuentaAfectada_MuestraLosEventosDeEsaCuenta()
    {
        await using var db = await SembrarAsync();

        var modelo = await ListarAsync(db, new BitacoraFiltros { Texto = "soda@recyplast.cr" });

        var fila = Assert.Single(modelo.Filas);
        Assert.Equal(TiposEventoBitacora.RolCambiado, fila.TipoEvento);
    }

    [Fact]
    public void Filtros_ElTextoSeNormalizaYSeConservaEnLaRutaYEnLasEtiquetas()
    {
        var vacio = new BitacoraFiltros { Texto = "   " };
        vacio.Normalizar();
        Assert.Null(vacio.Texto);
        Assert.False(vacio.TieneFiltros);

        var filtros = new BitacoraFiltros { Texto = "  Casado  " };
        filtros.Normalizar();
        Assert.Equal("Casado", filtros.Texto);
        Assert.True(filtros.TieneFiltros);
        Assert.Equal("Casado", filtros.ComoRuta()["texto"]);
        Assert.Contains(filtros.Activos(), a => a.Clave == "texto" && a.Valor == "Casado");
    }

    // ---------- Detalle ----------

    [Fact]
    public async Task Detalle_CambioDePrecio_MuestraProductoPreciosDiferenciaYEnlaceAlHistorial()
    {
        await using var db = await SembrarAsync();
        var id = db.Bitacoras.Single(b => b.IdRegistro == "10").IdBitacora;

        var vista = Assert.IsType<ViewResult>(await Controlador(db).Detalle(id, new BitacoraFiltros()));
        var modelo = Assert.IsType<BitacoraDetalleViewModel>(vista.Model);

        Assert.True(modelo.EsCambioPrecio);
        Assert.Equal("Casado · Grande", modelo.Producto);
        Assert.Equal("Casado", modelo.NombreArticulo);
        Assert.Equal(PresentacionBitacora.Colones("2500"), modelo.ValorAnterior);
        Assert.Equal(PresentacionBitacora.Colones("2800"), modelo.ValorNuevo);
        Assert.StartsWith("+", modelo.Diferencia);
        Assert.Equal("Precio de producto", modelo.Fila.RecursoTexto);
        Assert.False(modelo.Fila.EsCambioDirectoBd);
        Assert.Equal("ADM001", modelo.Fila.NombreUsuario);
    }

    [Fact]
    public async Task Detalle_CambioHechoDirectoEnLaBase_SeMarcaComoCambioDirecto()
    {
        await using var db = await SembrarAsync();
        var id = db.Bitacoras.Single(b => b.IdRegistro == "11").IdBitacora;

        var vista = Assert.IsType<ViewResult>(await Controlador(db).Detalle(id, new BitacoraFiltros()));
        var modelo = Assert.IsType<BitacoraDetalleViewModel>(vista.Model);

        Assert.True(modelo.Fila.EsCambioDirectoBd);
        Assert.StartsWith("−", modelo.Diferencia);
    }

    [Fact]
    public async Task Detalle_CambioDePrecioDeBebida_UsaElNombreDeLaBebida()
    {
        await using var db = await SembrarAsync();
        var id = db.Bitacoras.Single(b => b.Recurso == RecursosBitacora.Bebida).IdBitacora;

        var vista = Assert.IsType<ViewResult>(await Controlador(db).Detalle(id, new BitacoraFiltros()));
        var modelo = Assert.IsType<BitacoraDetalleViewModel>(vista.Model);

        Assert.Equal("Fresco de mora", modelo.Producto);
        Assert.Equal("Fresco de mora", modelo.NombreArticulo);
        Assert.Equal("Precio de bebida", modelo.Fila.RecursoTexto);
    }

    [Fact]
    public async Task Detalle_DeUnEventoQueNoExiste_DevuelveNoEncontrado()
    {
        await using var db = await SembrarAsync();

        Assert.IsType<NotFoundResult>(await Controlador(db).Detalle(999_999, new BitacoraFiltros()));
    }

    // ---------- Textos ----------

    [Theory]
    [InlineData("Producto: Casado | Tamaño: Grande", "Casado")]
    [InlineData("Producto: Gallo pinto | Tamaño: Único", "Gallo pinto")]
    [InlineData("Bebida: Fresco de mora | Tamaño: Sin tamaño", "Fresco de mora")]
    [InlineData("Cuenta soda@recyplast.cr: rol Soda -> Recursos Humanos", null)]
    [InlineData(null, null)]
    public void NombreArticuloDelPrecio_DevuelveSoloElNombreSinTamano(string? descripcion, string? esperado)
    {
        Assert.Equal(esperado, PresentacionBitacora.NombreArticuloDelPrecio(descripcion));
    }

    [Theory]
    [InlineData("tb_Precio", "Precio de producto")]
    [InlineData("tb_Bebida", "Precio de bebida")]
    [InlineData("AspNetUsers", "Cuentas de usuario")]
    [InlineData("Sesion", "Sesión")]
    [InlineData("tb_Pedido", "tb_Pedido")]
    public void EtiquetaRecurso_MuestraUnNombreLegibleYDejaIgualLosDesconocidos(string recurso, string esperado)
    {
        Assert.Equal(esperado, PresentacionBitacora.EtiquetaRecurso(recurso));
    }

    // ---------- Apoyo ----------

    private static BitacoraController Controlador(ApplicationDbContext db) =>
        TestServices.ConContexto(new BitacoraController(db));

    private static async Task<BitacoraListadoViewModel> ListarAsync(ApplicationDbContext db, BitacoraFiltros filtros)
    {
        var vista = Assert.IsType<ViewResult>(await Controlador(db).Index(filtros));
        return Assert.IsType<BitacoraListadoViewModel>(vista.Model);
    }

    /// <summary>
    /// Tres cambios de precio (dos del Casado y uno de una bebida), un cambio de rol y un inicio de sesión.
    /// Uno de los cambios del Casado se hizo desde la pantalla (con CONTEXT_INFO) y el otro directo en la BD.
    /// </summary>
    private static async Task<ApplicationDbContext> SembrarAsync()
    {
        var db = TestServices.CrearContextoInMemory();
        db.Bitacoras.AddRange(
            Precio("10", RecursosBitacora.Precio, "Producto: Casado | Tamaño: Grande", "2500", "2800",
                idUsuario: "u-admin", nombre: "ADM001", rol: "Administrador", minutos: 1),
            Precio("11", RecursosBitacora.Precio, "Producto: Casado | Tamaño: Pequeño", "2200", "2100",
                idUsuario: null, nombre: "sa", rol: null, minutos: 2),
            Precio("3", RecursosBitacora.Bebida, "Bebida: Fresco de mora | Tamaño: Sin tamaño", "800", "900",
                idUsuario: "u-admin", nombre: "ADM001", rol: "Administrador", minutos: 3),
            new Bitacora
            {
                FechaHoraUtc = Hoy.AddMinutes(4), Modulo = ModulosBitacora.Seguridad, TipoEvento = TiposEventoBitacora.RolCambiado,
                Recurso = RecursosBitacora.Cuentas, IdRegistro = "u-soda", EsExitoso = true, IdUsuario = "u-admin",
                NombreUsuario = "ADM001", Rol = "Administrador", UsuarioBd = "sa", ValorAnterior = "Soda", ValorNuevo = "RecursosHumanos",
                Descripcion = "Cuenta soda@recyplast.cr: rol Soda -> Recursos Humanos",
            },
            new Bitacora
            {
                FechaHoraUtc = Hoy.AddMinutes(5), Modulo = ModulosBitacora.Seguridad, TipoEvento = TiposEventoBitacora.InicioSesion,
                Recurso = RecursosBitacora.Sesion, EsExitoso = true, IdUsuario = "u-admin", NombreUsuario = "ADM001",
                Rol = "Administrador", UsuarioBd = "sa", Descripcion = "Inicio de sesión exitoso",
            });
        await db.SaveChangesAsync();
        return db;
    }

    private static Bitacora Precio(string idRegistro, string recurso, string descripcion, string anterior, string nuevo,
        string? idUsuario, string nombre, string? rol, int minutos) => new()
    {
        FechaHoraUtc = Hoy.AddMinutes(minutos), Modulo = ModulosBitacora.Catalogo, TipoEvento = TiposEventoBitacora.CambioPrecio,
        Recurso = recurso, IdRegistro = idRegistro, EsExitoso = true, IdUsuario = idUsuario, NombreUsuario = nombre, Rol = rol,
        UsuarioBd = "sa", ValorAnterior = anterior, ValorNuevo = nuevo, Descripcion = descripcion,
    };
}
