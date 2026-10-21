using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SAPS.Tests.Helpers;
using SAPS.Web.Controllers;
using SAPS.Web.Middleware;
using SAPS.Web.Models.Catalogo;
using SAPS.Web.Models.Pedidos;
using SAPS.Web.Services.Auditoria;
using SAPS.Web.Services.Pedidos;
using SAPS.Web.Services.Seguridad;

namespace SAPS.Tests;

/// <summary>
/// Endurecimiento posterior al Sprint 2: encabezados de seguridad, límite de peticiones, bitácora de pedidos y
/// de catálogo, y control de concurrencia en productos y bebidas.
/// </summary>
public class Endurecimiento_Sprint2Tests
{
    // ================= Encabezados de seguridad =================

    [Fact]
    public async Task Encabezados_CadaRespuestaLlevaCspNonceYProteccionesBasicas()
    {
        var http = new DefaultHttpContext();
        await new EncabezadosSeguridadMiddleware(_ => Task.CompletedTask).InvokeAsync(http);

        var nonce = (string)http.Items[EncabezadosSeguridadMiddleware.ClaveNonce]!;
        var csp = http.Response.Headers["Content-Security-Policy"].ToString();
        Assert.Contains($"script-src 'self' 'nonce-{nonce}'", csp);
        Assert.Contains("frame-ancestors 'none'", csp);
        Assert.Contains("object-src 'none'", csp);
        Assert.DoesNotContain("script-src 'self' 'unsafe-inline'", csp);
        Assert.Equal("nosniff", http.Response.Headers["X-Content-Type-Options"]);
        Assert.Equal("DENY", http.Response.Headers["X-Frame-Options"]);
        Assert.Equal("strict-origin-when-cross-origin", http.Response.Headers["Referrer-Policy"]);
    }

    [Fact]
    public async Task Encabezados_ElNonceCambiaEnCadaSolicitud()
    {
        var a = new DefaultHttpContext();
        var b = new DefaultHttpContext();
        var middleware = new EncabezadosSeguridadMiddleware(_ => Task.CompletedTask);
        await middleware.InvokeAsync(a);
        await middleware.InvokeAsync(b);

        Assert.NotEqual(a.Items[EncabezadosSeguridadMiddleware.ClaveNonce], b.Items[EncabezadosSeguridadMiddleware.ClaveNonce]);
    }

    // ================= Límite de peticiones =================

    private static HttpContext Login(string metodo, string ip) => new DefaultHttpContext
    {
        Request = { Method = metodo, Path = "/Identity/Account/Login" },
        Connection = { RemoteIpAddress = IPAddress.Parse(ip) },
    };

    [Fact]
    public void LimiteLogin_BloqueaElEnvioNumeroOnceDeUnaMismaIp()
    {
        var opciones = new RateLimiterOptions();
        LimitesPeticiones.Configurar(opciones);
        var limitador = opciones.GlobalLimiter!;

        for (var i = 0; i < LimitesPeticiones.IntentosLoginPorMinuto; i++)
        {
            using var permiso = limitador.AttemptAcquire(Login("POST", "10.0.0.1"));
            Assert.True(permiso.IsAcquired);
        }

        using var rechazado = limitador.AttemptAcquire(Login("POST", "10.0.0.1"));
        Assert.False(rechazado.IsAcquired);
    }

    [Fact]
    public void LimiteLogin_OtraIpYLasConsultasGetNoSeVenAfectadas()
    {
        var opciones = new RateLimiterOptions();
        LimitesPeticiones.Configurar(opciones);
        var limitador = opciones.GlobalLimiter!;
        for (var i = 0; i < LimitesPeticiones.IntentosLoginPorMinuto + 1; i++)
            limitador.AttemptAcquire(Login("POST", "10.0.0.1")).Dispose();

        using var otraIp = limitador.AttemptAcquire(Login("POST", "10.0.0.2"));
        using var abrirPagina = limitador.AttemptAcquire(Login("GET", "10.0.0.1"));
        Assert.True(otraIp.IsAcquired);
        Assert.True(abrirPagina.IsAcquired);
    }

    [Fact]
    public void PedidosController_TieneLaPoliticaDeLimiteDePeticiones()
    {
        var atributo = typeof(PedidosController)
            .GetCustomAttributes(typeof(EnableRateLimitingAttribute), inherit: false)
            .Cast<EnableRateLimitingAttribute>().Single();

        Assert.Equal(LimitesPeticiones.PoliticaPedidos, atributo.PolicyName);
    }

    // ================= Bitácora de pedidos =================

    private sealed class BitacoraFalsa : IServicioBitacora
    {
        public List<EventoBitacora> Eventos { get; } = [];

        public Task<bool> RegistrarAsync(EventoBitacora evento, CancellationToken ct = default)
        {
            Eventos.Add(evento);
            return Task.FromResult(true);
        }
    }

    private static async Task<(Microsoft.Data.Sqlite.SqliteConnection conexion, SAPS.Web.Data.ApplicationDbContext db, int idPrecio)> BaseConCasadoAsync()
    {
        var conexion = TestServices.AbrirConexionSqlite();
        var db = TestServices.CrearContextoSqlite(conexion);
        db.Users.Add(new Microsoft.AspNetCore.Identity.IdentityUser { Id = "usr-soda", UserName = "SODA001" });
        var precio = new Precio
        {
            Producto = new Producto { NombreProducto = "Casado", Categoria = new Categoria { NombreCategoria = "Almuerzo" } },
            MontoPrecio = 2800,
            FechaVigenciaDesde = new DateOnly(2026, 1, 1),
        };
        db.Precios.Add(precio);
        await db.SaveChangesAsync();
        return (conexion, db, precio.IdPrecio);
    }

    private static ServicioPedidos CrearServicio(SAPS.Web.Data.ApplicationDbContext db, IServicioBitacora? bitacora)
    {
        var colaboradores = new Mock<IColaboradores>();
        colaboradores.Setup(c => c.BuscarAsync("DEMO001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Colaborador("DEMO001", "Ana Solís (prueba)", true, "/img.png", "left"));
        return new ServicioPedidos(db, colaboradores.Object, new RelojFijo(new DateTimeOffset(2026, 9, 29, 18, 0, 0, TimeSpan.Zero)), bitacora);
    }

    private static RegistrarPedidoRequest Pedido(int idPrecio, Guid token) => new()
    {
        TokenRegistro = token, CodigoColaborador = "DEMO001", TipoComida = "Almuerzo",
        Lineas = [new LineaPedidoRequest { Clave = $"P:{idPrecio}", Cantidad = 2, PrecioMostrado = 2800 }],
    };

    [Fact]
    public async Task Pedido_RegistradoDejaUnEventoConIdColaboradorYTotal()
    {
        var (conexion, db, idPrecio) = await BaseConCasadoAsync();
        using var _ = conexion; await using var __ = db;
        var bitacora = new BitacoraFalsa();

        var registrado = await CrearServicio(db, bitacora).RegistrarAsync(Pedido(idPrecio, Guid.NewGuid()), "usr-soda");

        var evento = Assert.Single(bitacora.Eventos);
        Assert.Equal(ModulosBitacora.Pedidos, evento.Modulo);
        Assert.Equal(TiposEventoBitacora.PedidoRegistrado, evento.TipoEvento);
        Assert.Equal(RecursosBitacora.Pedido, evento.Recurso);
        Assert.True(evento.EsExitoso);
        Assert.Equal(registrado.IdPedido.ToString(), evento.IdRegistro);
        Assert.Equal("5600", evento.ValorNuevo);
        Assert.Contains("DEMO001", evento.Descripcion);
        // La columna es varchar: "₡" se guardaría como "¢".
        Assert.Contains("5600 colones", evento.Descripcion);
        Assert.All(evento.Descripcion!, c => Assert.True(c < 256, "La descripción debe caber en varchar"));
    }

    [Fact]
    public async Task Pedido_UnReintentoConElMismoTokenNoDuplicaElEvento()
    {
        var (conexion, db, idPrecio) = await BaseConCasadoAsync();
        using var _ = conexion; await using var __ = db;
        var bitacora = new BitacoraFalsa();
        var servicio = CrearServicio(db, bitacora);
        var token = Guid.NewGuid();

        await servicio.RegistrarAsync(Pedido(idPrecio, token), "usr-soda");
        await servicio.RegistrarAsync(Pedido(idPrecio, token), "usr-soda");

        Assert.Single(bitacora.Eventos);
    }

    [Fact]
    public async Task Pedido_RechazadoPorPrecioCambiadoQuedaEnLaBitacoraComoFallido()
    {
        var (conexion, db, idPrecio) = await BaseConCasadoAsync();
        using var _ = conexion; await using var __ = db;
        var bitacora = new BitacoraFalsa();
        var controlador = TestServices.ConContexto(
            new PedidosController(CrearServicio(db, bitacora), new EntornoPrueba(true), NullLogger<PedidosController>.Instance, bitacora), "POST");
        controlador.ControllerContext.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, "usr-soda")], "Prueba"));
        var solicitud = Pedido(idPrecio, Guid.NewGuid());
        solicitud.Lineas[0].PrecioMostrado = 2500;

        var resultado = await controlador.Registrar(solicitud, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(resultado);
        var evento = Assert.Single(bitacora.Eventos);
        Assert.Equal(TiposEventoBitacora.PedidoRechazado, evento.TipoEvento);
        Assert.False(evento.EsExitoso);
        Assert.Contains("Cambió un precio", evento.Descripcion);
    }

    // ================= Bitácora de catálogo =================

    private sealed class SinContexto : IContextoAuditoria
    {
        public Task EstablecerAsync(DatabaseFacade baseDatos, ClaimsPrincipal usuario, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task LimpiarAsync(DatabaseFacade baseDatos, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private static AdministracionController Administracion(SAPS.Web.Data.ApplicationDbContext db, IServicioBitacora? bitacora)
    {
        var controlador = TestServices.ConContexto(new AdministracionController(db, new SinContexto(), null, bitacora), "POST");
        controlador.ControllerContext.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, "u-9"), new Claim(ClaimTypes.Name, "ADM001")], "Prueba"));
        return controlador;
    }

    [Fact]
    public async Task Catalogo_DesactivarUnaBebidaQuedaEnLaBitacora()
    {
        await using var db = TestServices.CrearContextoInMemory();
        var bebida = new Bebida { NombreBebida = "Naranjada", TipoBebida = "Frescos", Precio = 800 };
        db.Bebidas.Add(bebida);
        await db.SaveChangesAsync();
        var bitacora = new BitacoraFalsa();

        await Administracion(db, bitacora).CambiarEstadoBebida(bebida.IdBebida);

        var evento = Assert.Single(bitacora.Eventos);
        Assert.Equal(ModulosBitacora.Catalogo, evento.Modulo);
        Assert.Equal(TiposEventoBitacora.CatalogoDesactivado, evento.TipoEvento);
        Assert.Equal(RecursosBitacora.Bebida, evento.Recurso);
        Assert.Equal(bebida.IdBebida.ToString(), evento.IdRegistro);
        Assert.Equal("Activo", evento.ValorAnterior);
        Assert.Equal("Inactivo", evento.ValorNuevo);
        Assert.Contains("Activo -> Inactivo", evento.Descripcion);
        Assert.All(evento.Descripcion!, c => Assert.True(c < 256, "La descripción debe caber en varchar"));
    }

    [Fact]
    public async Task Catalogo_CrearUnaCategoriaQuedaEnLaBitacora()
    {
        await using var db = TestServices.CrearContextoInMemory();
        var bitacora = new BitacoraFalsa();

        await Administracion(db, bitacora).CrearCategoria(new CategoriaFormViewModel { NombreCategoria = "Postres" });

        var evento = Assert.Single(bitacora.Eventos);
        Assert.Equal(TiposEventoBitacora.CatalogoCreado, evento.TipoEvento);
        Assert.Equal(RecursosBitacora.Categoria, evento.Recurso);
        Assert.Equal("Postres", evento.ValorNuevo);
    }

    [Fact]
    public async Task Catalogo_EditarSoloElPrecioNoDuplicaLoQueYaRegistraElTrigger()
    {
        await using var db = TestServices.CrearContextoInMemory();
        var cat = new Categoria { NombreCategoria = "Almuerzo" };
        var producto = new Producto { NombreProducto = "Casado", Categoria = cat };
        producto.Precios.Add(new Precio { MontoPrecio = 1500, FechaVigenciaDesde = new DateOnly(2026, 1, 1) });
        db.Productos.Add(producto);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var bitacora = new BitacoraFalsa();

        await Administracion(db, bitacora).EditarProducto(new ProductoFormViewModel
        {
            IdProducto = producto.IdProducto, NombreProducto = "Casado", IdCategoria = cat.IdCategoria, PrecioUnico = 1800,
        });

        Assert.Empty(bitacora.Eventos);
    }

    [Fact]
    public async Task Catalogo_EditarElNombreDeUnProductoQuedaEnLaBitacora()
    {
        await using var db = TestServices.CrearContextoInMemory();
        var cat = new Categoria { NombreCategoria = "Almuerzo" };
        var producto = new Producto { NombreProducto = "Casado", Categoria = cat };
        producto.Precios.Add(new Precio { MontoPrecio = 1500, FechaVigenciaDesde = new DateOnly(2026, 1, 1) });
        db.Productos.Add(producto);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var bitacora = new BitacoraFalsa();

        await Administracion(db, bitacora).EditarProducto(new ProductoFormViewModel
        {
            IdProducto = producto.IdProducto, NombreProducto = "Casado especial", IdCategoria = cat.IdCategoria, PrecioUnico = 1500,
        });

        var evento = Assert.Single(bitacora.Eventos);
        Assert.Equal(TiposEventoBitacora.CatalogoModificado, evento.TipoEvento);
        Assert.Equal("Casado", evento.ValorAnterior);
        Assert.Equal("Casado especial", evento.ValorNuevo);
    }

    [Theory]
    [InlineData(TiposEventoBitacora.CatalogoCreado)]
    [InlineData(TiposEventoBitacora.CatalogoModificado)]
    [InlineData(TiposEventoBitacora.CatalogoActivado)]
    [InlineData(TiposEventoBitacora.CatalogoDesactivado)]
    [InlineData(TiposEventoBitacora.PedidoRegistrado)]
    [InlineData(TiposEventoBitacora.PedidoRechazado)]
    public void Bitacora_LosEventosNuevosTienenEtiquetaYApareceEnElFiltro(string tipo)
    {
        Assert.NotEqual(tipo, PresentacionBitacora.EtiquetaEvento(tipo));
        Assert.Contains(PresentacionBitacora.EventosConocidos, e => e.TipoEvento == tipo);
    }

    // ================= Concurrencia =================

    [Fact]
    public async Task Concurrencia_ProductoYBebidaTienenVersionDeFila()
    {
        await using var db = TestServices.CrearContextoSqlServerSinConexion();

        Assert.True(db.Model.FindEntityType(typeof(Producto))!.FindProperty(nameof(Producto.RowVersion))!.IsConcurrencyToken);
        Assert.True(db.Model.FindEntityType(typeof(Bebida))!.FindProperty(nameof(Bebida.RowVersion))!.IsConcurrencyToken);
        await Task.CompletedTask;
    }

    [Fact]
    public async Task Concurrencia_EditarUnaBebidaConUnaVersionVencidaNoGuardaYAvisa()
    {
        using var conexion = TestServices.AbrirConexionSqlite();
        await using var db = TestServices.CrearContextoSqlite(conexion);
        var bebida = new Bebida { NombreBebida = "Naranjada", TipoBebida = "Frescos", Precio = 800 };
        db.Bebidas.Add(bebida);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var resultado = await Administracion(db, null).EditarBebida(new BebidaFormViewModel
        {
            IdBebida = bebida.IdBebida, NombreBebida = "Naranjada", TipoBebida = "Frescos", Precio = 900,
            RowVersion = [9, 9, 9, 9, 9, 9, 9, 9],
        });

        var redireccion = Assert.IsType<RedirectToActionResult>(resultado);
        Assert.Equal(nameof(AdministracionController.Catalogo), redireccion.ActionName);
        db.ChangeTracker.Clear();
        Assert.Equal(800, (await db.Bebidas.AsNoTracking().SingleAsync()).Precio);
    }

    [Fact]
    public async Task Concurrencia_EditarUnProductoConUnaVersionVencidaNoGuardaYAvisa()
    {
        using var conexion = TestServices.AbrirConexionSqlite();
        await using var db = TestServices.CrearContextoSqlite(conexion);
        var cat = new Categoria { NombreCategoria = "Almuerzo" };
        var producto = new Producto { NombreProducto = "Casado", Categoria = cat };
        producto.Precios.Add(new Precio { MontoPrecio = 1500, FechaVigenciaDesde = new DateOnly(2026, 1, 1) });
        db.Productos.Add(producto);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var controlador = Administracion(db, null);
        var resultado = await controlador.EditarProducto(new ProductoFormViewModel
        {
            IdProducto = producto.IdProducto, NombreProducto = "Casado", IdCategoria = cat.IdCategoria, PrecioUnico = 1800,
            RowVersion = [9, 9, 9, 9, 9, 9, 9, 9],
        });

        Assert.IsType<RedirectToActionResult>(resultado);
        Assert.Contains("Casado", (string)controlador.TempData["Error"]!);
        db.ChangeTracker.Clear();
        var precios = await db.Precios.AsNoTracking().ToListAsync();
        Assert.Single(precios);
        Assert.Equal(1500, precios[0].MontoPrecio);
        Assert.True(precios[0].Activo);
    }

    // ================= Cierre de pendientes =================

    [Fact]
    public void FechaCostaRica_UsaUtcMenosSeisAunqueElServidorEsteEnOtraZona()
    {
        // 03:00 UTC del 21/10 son las 21:00 del 20/10 en Costa Rica.
        var reloj = new RelojFijo(new DateTimeOffset(2026, 10, 21, 3, 0, 0, TimeSpan.Zero));

        Assert.Equal(new DateOnly(2026, 10, 20), SAPS.Web.Services.FechaCostaRica.Hoy(reloj));
    }

    [Fact]
    public async Task Catalogo_SiLaBaseRechazaUnaAlta_ElFormularioExplicaElErrorEnVezDeCaerEnLaPaginaDeError()
    {
        await using var db = TestServices.CrearContextoInMemory();
        var controlador = Administracion(db, null);
        db.SavingChanges += (_, _) => throw new DbUpdateException("clave duplicada simulada");

        var resultado = await controlador.CrearCategoria(new CategoriaFormViewModel { NombreCategoria = "Postres" });

        Assert.IsType<ViewResult>(resultado);
        Assert.Contains(controlador.ModelState[string.Empty]!.Errors, e => e.ErrorMessage == AdministracionController.MensajeErrorGuardado);
    }

    [Fact]
    public void Contrasena_ElMinimoSigueSiendoDeSeisCaracteres()
    {
        Assert.Equal(6, SAPS.Web.Data.IdentityConfig.LongitudMinimaContrasena);
    }
}
