using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SAPS.Tests.Helpers;
using SAPS.Web.Controllers;
using SAPS.Web.Data;
using SAPS.Web.Models.Auditoria;
using SAPS.Web.Services.Auditoria;

namespace SAPS.Tests;

/// <summary>
/// Sprint 2 — HU-004 criterios 3, 5, 6 y 8: consulta de la bitácora (BitacoraController.Index), sus filtros
/// (resultado, módulo, usuario y fechas en hora de Costa Rica), la paginación y los textos legibles de
/// PresentacionBitacora. Complementa HU015_BitacoraCambioPrecioTests (filtro por producto y detalle de precios).
/// </summary>
public class HU004_ConsultaBitacoraTests
{
    /// <summary>16/10/2026 a medianoche en Costa Rica (UTC-6).</summary>
    private static readonly DateTime MedianocheCr = new(2026, 10, 16, 6, 0, 0, DateTimeKind.Utc);

    // ---------- Criterios 5 y 6: solo el Administrador y solo lectura ----------

    [Fact]
    public void Controlador_SoloPermiteElRolAdministrador()
    {
        var atributo = typeof(BitacoraController).GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(atributo);
        Assert.Equal("Administrador", atributo!.Roles);
    }

    [Fact]
    public void Controlador_SoloTieneAccionesDeConsulta()
    {
        var acciones = typeof(BitacoraController)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

        Assert.NotEmpty(acciones);
        Assert.All(acciones, a =>
        {
            Assert.NotNull(a.GetCustomAttribute<HttpGetAttribute>());
            Assert.Null(a.GetCustomAttribute<HttpPostAttribute>());
            Assert.Null(a.GetCustomAttribute<HttpPutAttribute>());
            Assert.Null(a.GetCustomAttribute<HttpDeleteAttribute>());
        });
    }

    [Fact]
    public async Task Consultar_NoModificaNingunRegistro()
    {
        await using var db = await SembrarAsync();
        var antes = db.Bitacoras.Select(b => new { b.IdBitacora, b.Descripcion }).ToList();

        await ListarAsync(db, new BitacoraFiltros());
        await Controlador(db).Detalle(antes[0].IdBitacora, new BitacoraFiltros());

        Assert.False(db.ChangeTracker.HasChanges());
        Assert.Equal(antes, db.Bitacoras.Select(b => new { b.IdBitacora, b.Descripcion }).ToList());
    }

    // ---------- Listado y filtros ----------

    [Fact]
    public async Task SinFiltros_MuestraTodoConLoMasRecienteDePrimero()
    {
        await using var db = await SembrarAsync();

        var modelo = await ListarAsync(db, new BitacoraFiltros());

        Assert.Equal(6, modelo.TotalRegistros);
        Assert.Equal(modelo.Filas.OrderByDescending(f => f.FechaHoraCostaRica).Select(f => f.IdBitacora),
                     modelo.Filas.Select(f => f.IdBitacora));
    }

    [Fact]
    public async Task FiltroResultadoFallido_SoloMuestraLosIntentosFallidos()
    {
        await using var db = await SembrarAsync();

        var modelo = await ListarAsync(db, new BitacoraFiltros { Resultado = "FALLIDO" });

        var fila = Assert.Single(modelo.Filas);
        Assert.False(fila.EsExitoso);
        Assert.Equal("fallido", modelo.Filtros.Resultado);
    }

    [Fact]
    public async Task FiltroResultadoConValorInvalido_SeIgnora()
    {
        await using var db = await SembrarAsync();

        var modelo = await ListarAsync(db, new BitacoraFiltros { Resultado = "talvez" });

        Assert.Null(modelo.Filtros.Resultado);
        Assert.Equal(6, modelo.TotalRegistros);
    }

    [Fact]
    public async Task FiltroPorModulo_SoloMuestraEseModulo()
    {
        await using var db = await SembrarAsync();

        var modelo = await ListarAsync(db, new BitacoraFiltros { Modulo = ModulosBitacora.Catalogo });

        var fila = Assert.Single(modelo.Filas);
        Assert.Equal(TiposEventoBitacora.CambioPrecio, fila.TipoEvento);
    }

    [Fact]
    public async Task FiltroPorUsuario_BuscaPorParteDelNombre()
    {
        await using var db = await SembrarAsync();

        var modelo = await ListarAsync(db, new BitacoraFiltros { Usuario = "soda@" });

        Assert.Equal(2, modelo.TotalRegistros);
        Assert.All(modelo.Filas, f => Assert.Contains("soda@", f.NombreUsuario));
    }

    [Fact]
    public async Task FiltroPorFecha_UsaElDiaEnHoraDeCostaRica()
    {
        await using var db = await SembrarAsync();

        // El evento de las 11:30 p. m. del 15/10 en Costa Rica ya es 16/10 en UTC: no debe salir en el día 16.
        var dia16 = await ListarAsync(db, new BitacoraFiltros { Desde = new DateTime(2026, 10, 16), Hasta = new DateTime(2026, 10, 16) });
        var dia15 = await ListarAsync(db, new BitacoraFiltros { Desde = new DateTime(2026, 10, 15), Hasta = new DateTime(2026, 10, 15) });

        Assert.Equal(5, dia16.TotalRegistros);
        var fila = Assert.Single(dia15.Filas);
        Assert.Equal(new DateTime(2026, 10, 15, 23, 30, 0), fila.FechaHoraCostaRica);
    }

    [Fact]
    public async Task FiltroPorFecha_SiLasFechasVienenInvertidas_SeOrdenan()
    {
        await using var db = await SembrarAsync();

        var modelo = await ListarAsync(db, new BitacoraFiltros { Desde = new DateTime(2026, 10, 16), Hasta = new DateTime(2026, 10, 15) });

        Assert.Equal(new DateTime(2026, 10, 15), modelo.Filtros.Desde);
        Assert.Equal(new DateTime(2026, 10, 16), modelo.Filtros.Hasta);
        Assert.Equal(6, modelo.TotalRegistros);
    }

    [Fact]
    public async Task FiltrosCombinados_SeAplicanTodosALaVez()
    {
        await using var db = await SembrarAsync();

        var modelo = await ListarAsync(db, new BitacoraFiltros
        {
            TipoEvento = TiposEventoBitacora.InicioSesion, Resultado = "exitoso", Usuario = "ADM001",
        });

        var fila = Assert.Single(modelo.Filas);
        Assert.Equal("ADM001", fila.NombreUsuario);
        Assert.True(fila.EsExitoso);
    }

    [Fact]
    public async Task FiltroSinCoincidencias_DevuelveListaVaciaSinError()
    {
        await using var db = await SembrarAsync();

        var modelo = await ListarAsync(db, new BitacoraFiltros { Usuario = "nadie-con-este-nombre" });

        Assert.Empty(modelo.Filas);
        Assert.Equal(0, modelo.TotalRegistros);
        Assert.Equal(1, modelo.TotalPaginas);
        Assert.Equal(0, modelo.PrimeraFila);
    }

    // ---------- Paginación ----------

    [Fact]
    public async Task Paginacion_MuestraCincuentaPorPaginaYCalculaElTotal()
    {
        await using var db = await SembrarMuchosAsync(120);

        var pagina1 = await ListarAsync(db, new BitacoraFiltros());
        var pagina3 = await ListarAsync(db, new BitacoraFiltros(), pagina: 3);

        Assert.Equal(BitacoraController.TamanoPagina, pagina1.Filas.Count);
        Assert.Equal(3, pagina1.TotalPaginas);
        Assert.Equal(20, pagina3.Filas.Count);
        Assert.Equal(101, pagina3.PrimeraFila);
        Assert.Equal(120, pagina3.UltimaFila);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    [InlineData(99, 3)]
    public async Task Paginacion_UnaPaginaFueraDeRango_SeAjustaAlLimite(int pedida, int esperada)
    {
        await using var db = await SembrarMuchosAsync(120);

        var modelo = await ListarAsync(db, new BitacoraFiltros(), pedida);

        Assert.Equal(esperada, modelo.Pagina);
        Assert.NotEmpty(modelo.Filas);
    }

    // ---------- Criterio 8: eventos nuevos sin cambiar código ----------

    [Fact]
    public async Task OpcionesDelFiltro_IncluyenLosEventosConocidosAunqueLaTablaEsteVacia()
    {
        await using var db = TestServices.CrearContextoInMemory();

        var modelo = await ListarAsync(db, new BitacoraFiltros());

        var tipos = modelo.TiposPorModulo.SelectMany(g => g.Opciones).Select(o => o.Valor).ToList();
        foreach (var (_, tipo) in PresentacionBitacora.EventosConocidos)
            Assert.Contains(tipo, tipos);
    }

    [Fact]
    public async Task OpcionesDelFiltro_UnEventoNuevoEnLaTabla_ApareceSinCambiarCodigo()
    {
        await using var db = await SembrarAsync();
        db.Bitacoras.Add(Evento("Pedidos", "AnulacionOrden", "tb_Pedido", "ADM001", exitoso: true, MedianocheCr.AddHours(15)));
        await db.SaveChangesAsync();

        var modelo = await ListarAsync(db, new BitacoraFiltros());

        Assert.Contains(modelo.Modulos, m => m.Valor == "Pedidos" && m.Texto == "Pedidos");
        Assert.Contains(modelo.TiposPorModulo.SelectMany(g => g.Opciones), o => o.Valor == "AnulacionOrden");
    }

    [Fact]
    public async Task OpcionesDelFiltro_UnTipoDesconocidoEnLaUrl_SeConservaComoOpcion()
    {
        await using var db = await SembrarAsync();

        var modelo = await ListarAsync(db, new BitacoraFiltros { TipoEvento = "EventoInventado" });

        Assert.Empty(modelo.Filas);
        var otros = Assert.Single(modelo.TiposPorModulo, g => g.Texto == "Otros");
        Assert.Equal("EventoInventado", Assert.Single(otros.Opciones).Valor);
    }

    // ---------- Filtros en la URL ----------

    [Fact]
    public void ComoRuta_ConservaLosFiltrosYLaPagina()
    {
        var filtros = new BitacoraFiltros
        {
            TipoEvento = TiposEventoBitacora.CambioPrecio, Resultado = "exitoso",
            Desde = new DateTime(2026, 10, 1), Hasta = new DateTime(2026, 10, 16),
        };
        filtros.Normalizar();

        var ruta = filtros.ComoRuta(pagina: 2);

        Assert.Equal("CambioPrecio", ruta["tipoEvento"]);
        Assert.Equal("exitoso", ruta["resultado"]);
        Assert.Equal("2026-10-01", ruta["desde"]);
        Assert.Equal("2026-10-16", ruta["hasta"]);
        Assert.Equal("2", ruta["pagina"]);
    }

    [Fact]
    public void ComoRuta_SinClave_QuitaEseFiltroParaElBotonDeQuitar()
    {
        var filtros = new BitacoraFiltros { TipoEvento = TiposEventoBitacora.InicioSesion, Usuario = "ADM001" };
        filtros.Normalizar();

        var ruta = filtros.ComoRuta(pagina: 1, sinClave: "usuario");

        Assert.False(ruta.ContainsKey("usuario"));
        Assert.False(ruta.ContainsKey("pagina"));
        Assert.Equal("InicioSesion", ruta["tipoEvento"]);
    }

    [Fact]
    public void Normalizar_RecortaTextosMuyLargos()
    {
        var filtros = new BitacoraFiltros { Usuario = new string('x', 500) };

        filtros.Normalizar();

        Assert.Equal(100, filtros.Usuario!.Length);
    }

    [Fact]
    public void Activos_MuestraCadaFiltroConTextoLegible()
    {
        var filtros = new BitacoraFiltros
        {
            TipoEvento = TiposEventoBitacora.RolCambiado, Modulo = ModulosBitacora.Catalogo, Resultado = "fallido",
        };
        filtros.Normalizar();

        var activos = filtros.Activos();

        Assert.Contains(activos, a => a.Nombre == "Evento" && a.Valor == "Cambio de rol");
        Assert.Contains(activos, a => a.Nombre == "Módulo" && a.Valor == "Catálogo");
        Assert.Contains(activos, a => a.Nombre == "Resultado" && a.Valor == "Fallido");
    }

    // ---------- Textos legibles (PresentacionBitacora) ----------

    [Fact]
    public void HoraCostaRica_EsUtcMenosSeisYSeConvierteDeVuelta()
    {
        var utc = new DateTime(2026, 10, 16, 5, 30, 0);

        var cr = PresentacionBitacora.AHoraCostaRica(utc);

        Assert.Equal(new DateTime(2026, 10, 15, 23, 30, 0), cr);
        Assert.Equal(utc, PresentacionBitacora.AUtc(cr));
    }

    [Theory]
    [InlineData("InicioSesion", "Inicio de sesión")]
    [InlineData("CuentaDesactivada", "Cuenta desactivada")]
    [InlineData("CambioPrecio", "Cambio de precio")]
    [InlineData("AnulacionOrden", "AnulacionOrden")]
    [InlineData(null, "")]
    public void EtiquetaEvento_TraduceLosConocidosYDejaIgualLosNuevos(string? tipo, string esperado)
    {
        Assert.Equal(esperado, PresentacionBitacora.EtiquetaEvento(tipo));
    }

    [Theory]
    [InlineData("RecursosHumanos", "Recursos Humanos")]
    [InlineData("Administrador", "Administrador")]
    [InlineData("", null)]
    public void EtiquetaRol_MuestraElNombreLegible(string rol, string? esperado)
    {
        Assert.Equal(esperado, PresentacionBitacora.EtiquetaRol(rol));
    }

    [Fact]
    public void Colones_FormateaMontosYDejaIgualLoQueNoEsNumero()
    {
        Assert.StartsWith("₡", PresentacionBitacora.Colones("2500.00"));
        Assert.Contains("500", PresentacionBitacora.Colones("2500.00"));
        Assert.Equal("sin dato", PresentacionBitacora.Colones("sin dato"));
        Assert.Null(PresentacionBitacora.Colones("  "));
    }

    [Theory]
    [InlineData("2500", "2800", "+", "12,0 %")]
    [InlineData("2000", "1500", "−", "25,0 %")]
    public void DiferenciaPrecio_MuestraSignoMontoYPorcentaje(string anterior, string nuevo, string signo, string porcentaje)
    {
        var texto = PresentacionBitacora.DiferenciaPrecio(anterior, nuevo);

        Assert.NotNull(texto);
        Assert.StartsWith(signo + "₡", texto);
        Assert.Contains(porcentaje, texto);
    }

    [Fact]
    public void DiferenciaPrecio_DesdeCeroOConTextos_NoDivideNiFalla()
    {
        Assert.Equal("+₡500", PresentacionBitacora.DiferenciaPrecio("0", "500"));
        Assert.Null(PresentacionBitacora.DiferenciaPrecio("abc", "500"));
        Assert.Null(PresentacionBitacora.DiferenciaPrecio(null, "500"));
    }

    [Theory]
    [InlineData("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/141.0 Safari/537.36", "Chrome en Windows")]
    [InlineData("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/141.0 Safari/537.36 Edg/141.0", "Edge en Windows")]
    [InlineData("Mozilla/5.0 (iPhone; CPU iPhone OS 18_0 like Mac OS X) AppleWebKit/605.1.15 Version/18.0 Mobile Safari/604.1", "Safari en iPhone")]
    [InlineData("Mozilla/5.0 (Linux; Android 14) AppleWebKit/537.36 Chrome/141.0 Mobile Safari/537.36", "Chrome en Android")]
    [InlineData("curl/8.4.0", "curl/8.4.0")]
    [InlineData("", null)]
    public void ResumirDispositivo_DaNavegadorYSistema(string userAgent, string? esperado)
    {
        Assert.Equal(esperado, PresentacionBitacora.ResumirDispositivo(userAgent));
    }

    [Theory]
    [InlineData("Cuenta soda@recyplast.cr creada con rol Soda", "soda@recyplast.cr")]
    [InlineData("Cuenta rh@recyplast.cr: rol Soda -> Recursos Humanos", "rh@recyplast.cr")]
    [InlineData("Cuenta admin@recyplast.cr desactivada", "admin@recyplast.cr")]
    [InlineData("Inicio de sesión exitoso", null)]
    [InlineData(null, null)]
    public void CuentaAfectada_SacaElCorreoDeLaDescripcion(string? descripcion, string? esperado)
    {
        Assert.Equal(esperado, PresentacionBitacora.CuentaAfectada(descripcion));
    }

    [Theory]
    [InlineData("Producto: Casado | Tamaño: Grande", "Casado · Grande")]
    [InlineData("Producto: Gallo pinto | Tamaño: Único", "Gallo pinto")]
    [InlineData("Bebida: Fresco de mora | Tamaño: Sin tamaño", "Fresco de mora")]
    [InlineData(null, null)]
    public void ProductoDelPrecio_UneNombreYTamano(string? descripcion, string? esperado)
    {
        Assert.Equal(esperado, PresentacionBitacora.ProductoDelPrecio(descripcion));
    }

    // ---------- Apoyo ----------

    private static BitacoraController Controlador(ApplicationDbContext db) =>
        TestServices.ConContexto(new BitacoraController(db));

    private static async Task<BitacoraListadoViewModel> ListarAsync(ApplicationDbContext db, BitacoraFiltros filtros, int pagina = 1)
    {
        var vista = Assert.IsType<ViewResult>(await Controlador(db).Index(filtros, pagina));
        return Assert.IsType<BitacoraListadoViewModel>(vista.Model);
    }

    /// <summary>
    /// Seis eventos: uno a las 11:30 p. m. del 15/10 (hora CR) y cinco el 16/10, entre ellos un login fallido,
    /// un alta de cuenta, un cambio de precio y dos acciones de la cuenta de la soda.
    /// </summary>
    private static async Task<ApplicationDbContext> SembrarAsync()
    {
        var db = TestServices.CrearContextoInMemory();
        db.Bitacoras.AddRange(
            Evento(ModulosBitacora.Seguridad, TiposEventoBitacora.InicioSesion, RecursosBitacora.Sesion, "soda@recyplast.cr", true, MedianocheCr.AddMinutes(-30)),
            Evento(ModulosBitacora.Seguridad, TiposEventoBitacora.InicioSesion, RecursosBitacora.Sesion, "ADM001", true, MedianocheCr.AddHours(8)),
            Evento(ModulosBitacora.Seguridad, TiposEventoBitacora.InicioSesion, RecursosBitacora.Sesion, "noexiste@recyplast.cr", false, MedianocheCr.AddHours(9)),
            Evento(ModulosBitacora.Seguridad, TiposEventoBitacora.CuentaCreada, RecursosBitacora.Cuentas, "ADM001", true, MedianocheCr.AddHours(10)),
            Evento(ModulosBitacora.Catalogo, TiposEventoBitacora.CambioPrecio, RecursosBitacora.Precio, "ADM001", true, MedianocheCr.AddHours(11)),
            Evento(ModulosBitacora.Seguridad, TiposEventoBitacora.CierreSesion, RecursosBitacora.Sesion, "soda@recyplast.cr", true, MedianocheCr.AddHours(12)));
        await db.SaveChangesAsync();
        return db;
    }

    private static async Task<ApplicationDbContext> SembrarMuchosAsync(int cantidad)
    {
        var db = TestServices.CrearContextoInMemory();
        for (var i = 0; i < cantidad; i++)
            db.Bitacoras.Add(Evento(ModulosBitacora.Seguridad, TiposEventoBitacora.InicioSesion, RecursosBitacora.Sesion,
                "ADM001", true, MedianocheCr.AddMinutes(i)));
        await db.SaveChangesAsync();
        return db;
    }

    private static Bitacora Evento(string modulo, string tipo, string recurso, string usuario, bool exitoso, DateTime fechaUtc) => new()
    {
        FechaHoraUtc = fechaUtc, Modulo = modulo, TipoEvento = tipo, Recurso = recurso, EsExitoso = exitoso,
        NombreUsuario = usuario, UsuarioBd = "sa", Descripcion = $"{tipo} de {usuario}",
    };
}
