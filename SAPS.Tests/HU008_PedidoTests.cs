using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SAPS.Tests.Helpers;
using SAPS.Web.Controllers;
using SAPS.Web.Data;
using SAPS.Web.Models.Catalogo;
using SAPS.Web.Models.Pedidos;
using SAPS.Web.Services.Pedidos;

namespace SAPS.Tests;

/// <summary>
/// HU-008 | Registrar pedido de un colaborador.
/// Criterios (docs/HU008-RegistroPedidos.md): la operadora busca al colaborador por código
/// y solo se aceptan colaboradores activos; se ofrecen únicamente productos activos de
/// categorías activas, con precio vigente y tamaño activo; el total lo calcula el servidor
/// con el precio de la base (enteros, subtotal = cantidad × precio); se rechazan pedidos
/// vacíos, cantidades no positivas, artículos repetidos o no disponibles y precios cambiados;
/// cabecera y detalle se guardan juntos; un reintento con el mismo identificador no duplica
/// el pedido; los nombres y precios cobrados quedan copiados en el detalle.
///
/// Se usa SQLite en memoria (relacional) porque el servicio abre una transacción
/// Serializable, que EF Core InMemory no soporta.
/// </summary>
public sealed class HU008_PedidoTests : IDisposable
{
    private const string UsuarioOperadora = "usr-soda001";
    // 29/09/2026 18:00 UTC = 12:00 en Costa Rica
    private static readonly DateTimeOffset Ahora = new(2026, 9, 29, 18, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly InicioVigencia = new(2026, 1, 1);

    private readonly SqliteConnection _conexion;
    private readonly ApplicationDbContext _db;
    private readonly RelojFijo _reloj = new(Ahora);
    private readonly Mock<IColaboradores> _colaboradores = new();
    private readonly ServicioPedidos _servicio;

    // Datos sembrados
    private readonly Categoria _almuerzo;
    private readonly Tamano _pequeno, _grande, _botella;
    private readonly Precio _precioCasado, _precioFrescoPequeno, _precioFrescoGrande;
    private readonly Bebida _cocaCola;

    public HU008_PedidoTests()
    {
        _conexion = TestServices.AbrirConexionSqlite();
        _db = TestServices.CrearContextoSqlite(_conexion);

        _colaboradores.Setup(c => c.BuscarAsync("DEMO001", It.IsAny<CancellationToken>()))
                      .ReturnsAsync(new Colaborador("DEMO001", "Ana Solís (prueba)", true, "/img.png", "left"));
        _colaboradores.Setup(c => c.BuscarAsync("DEMO003", It.IsAny<CancellationToken>()))
                      .ReturnsAsync(new Colaborador("DEMO003", "Colaborador inactivo", false, "/img.png", "right"));

        _db.Users.Add(new IdentityUser { Id = UsuarioOperadora, UserName = "SODA001" });

        _almuerzo = new Categoria { NombreCategoria = "Almuerzo" };
        _pequeno = new Tamano { NombreTamano = "Pequeño" };
        _grande = new Tamano { NombreTamano = "Grande" };
        _botella = new Tamano { NombreTamano = "600ml" };
        var casado = new Producto { NombreProducto = "Casado", Categoria = _almuerzo };
        var fresco = new Producto { NombreProducto = "Fresco de cas", Categoria = _almuerzo, RequiereTamano = true };
        _precioCasado = new Precio { Producto = casado, MontoPrecio = 2800, FechaVigenciaDesde = InicioVigencia };
        _precioFrescoPequeno = new Precio { Producto = fresco, Tamano = _pequeno, MontoPrecio = 600, FechaVigenciaDesde = InicioVigencia };
        _precioFrescoGrande = new Precio { Producto = fresco, Tamano = _grande, MontoPrecio = 1000, FechaVigenciaDesde = InicioVigencia };
        _cocaCola = new Bebida { NombreBebida = "Coca-Cola", TipoBebida = "Gaseosa", Tamano = _botella, Precio = 1200 };
        _db.AddRange(_precioCasado, _precioFrescoPequeno, _precioFrescoGrande, _cocaCola);
        _db.SaveChanges();

        _servicio = new ServicioPedidos(_db, _colaboradores.Object, _reloj);
    }

    public void Dispose()
    {
        _db.Dispose();
        _conexion.Dispose();
    }

    private string P(Precio p) => $"P:{p.IdPrecio}";
    private string B(Bebida b) => $"B:{b.IdBebida}";

    private RegistrarPedidoRequest Solicitud(params (string clave, int cantidad, int precio)[] lineas) => new()
    {
        TokenRegistro = Guid.NewGuid(),
        CodigoColaborador = "DEMO001",
        TipoComida = "Almuerzo",
        Lineas = lineas.Select(l => new LineaPedidoRequest { Clave = l.clave, Cantidad = l.cantidad, PrecioMostrado = l.precio }).ToList()
    };

    private async Task<string> MensajeDeErrorAsync(RegistrarPedidoRequest solicitud, string usuario = UsuarioOperadora)
    {
        var ex = await Assert.ThrowsAnyAsync<PedidoInvalidoException>(() => _servicio.RegistrarAsync(solicitud, usuario));
        return ex.Message;
    }

    // ================= Búsqueda del colaborador =================

    [Fact]
    public async Task BuscarColaborador_CodigoActivo_DevuelveCodigoNombreYFoto()
    {
        var colaborador = await _servicio.BuscarColaboradorAsync("DEMO001");

        Assert.Equal("DEMO001", colaborador.Codigo);
        Assert.Equal("Ana Solís (prueba)", colaborador.Nombre);
        Assert.False(string.IsNullOrEmpty(colaborador.FotoUrl));
    }

    [Theory]
    [InlineData(null, "Ingrese el código")]
    [InlineData("   ", "Ingrese el código")]
    [InlineData("DEMO999", "no encontrado")]
    [InlineData("DEMO003", "inactivo")]
    public async Task BuscarColaborador_CodigoInvalidoInexistenteOInactivo_EsRechazado(string? codigo, string mensaje)
    {
        var ex = await Assert.ThrowsAnyAsync<PedidoInvalidoException>(() => _servicio.BuscarColaboradorAsync(codigo));

        Assert.Contains(mensaje, ex.Message);
    }

    [Fact]
    public async Task BuscarColaborador_CodigoDeMasDe50Caracteres_EsRechazado()
    {
        var ex = await Assert.ThrowsAsync<PedidoInvalidoException>(() => _servicio.BuscarColaboradorAsync(new string('A', 51)));

        Assert.Contains("demasiado largo", ex.Message);
    }

    // ================= Catálogo disponible para vender =================

    [Fact]
    public async Task Catalogo_OfreceProductosPorTamanoYBebidasConSuPrecio()
    {
        var catalogo = await _servicio.CatalogoAsync();

        Assert.Equal(4, catalogo.Count);
        Assert.Contains(catalogo, a => a.Clave == P(_precioCasado) && a.Precio == 2800 && a.Tamano == null);
        Assert.Contains(catalogo, a => a.Clave == P(_precioFrescoPequeno) && a.Tamano == "Pequeño" && a.Precio == 600);
        Assert.Contains(catalogo, a => a.Clave == P(_precioFrescoGrande) && a.Tamano == "Grande" && a.Precio == 1000);
        Assert.Contains(catalogo, a => a.Clave == B(_cocaCola) && a.Categoria == "Bebidas" && a.Precio == 1200);
    }

    [Fact]
    public async Task Catalogo_OcultaProductosInactivos()
    {
        _precioCasado.Producto!.Activo = false;
        await _db.SaveChangesAsync();

        Assert.DoesNotContain(await _servicio.CatalogoAsync(), a => a.Nombre == "Casado");
    }

    [Fact]
    public async Task Catalogo_OcultaProductosDeCategoriasInactivas()
    {
        _almuerzo.Activo = false;
        await _db.SaveChangesAsync();

        var catalogo = await _servicio.CatalogoAsync();

        Assert.Equal(new[] { "Bebidas" }, catalogo.Select(a => a.Categoria).Distinct());
    }

    [Fact]
    public async Task Catalogo_DesactivarUnTamano_DejaDisponiblesLosDemas()
    {
        _pequeno.Activo = false;
        await _db.SaveChangesAsync();

        var frescos = (await _servicio.CatalogoAsync()).Where(a => a.Nombre == "Fresco de cas").ToList();

        Assert.Equal("Grande", Assert.Single(frescos).Tamano);
    }

    [Fact]
    public async Task Catalogo_OcultaBebidasInactivasOConTamanoInactivo()
    {
        _botella.Activo = false;
        await _db.SaveChangesAsync();
        Assert.DoesNotContain(await _servicio.CatalogoAsync(), a => a.Categoria == "Bebidas");

        _botella.Activo = true;
        _cocaCola.Activo = false;
        await _db.SaveChangesAsync();
        Assert.DoesNotContain(await _servicio.CatalogoAsync(), a => a.Categoria == "Bebidas");
    }

    [Fact]
    public async Task Catalogo_SoloIncluyePreciosVigentes()
    {
        _precioCasado.Activo = false;
        _precioCasado.FechaVigenciaHasta = new DateOnly(2026, 9, 28);            // precio vencido
        _db.Precios.Add(new Precio { IdProducto = _precioCasado.IdProducto, MontoPrecio = 3000,
            FechaVigenciaDesde = new DateOnly(2026, 10, 1) });                    // precio futuro
        await _db.SaveChangesAsync();

        Assert.DoesNotContain(await _servicio.CatalogoAsync(), a => a.Nombre == "Casado");
    }

    [Fact]
    public async Task Catalogo_LaVigenciaSeEvaluaConLaFechaDeCostaRica()
    {
        // 30/09 03:00 UTC sigue siendo 29/09 (21:00) en Costa Rica
        _reloj.AhoraUtc = new DateTimeOffset(2026, 9, 30, 3, 0, 0, TimeSpan.Zero);
        _db.Precios.Add(new Precio { IdProducto = _precioFrescoPequeno.IdProducto, IdTamano = _botella.IdTamano,
            MontoPrecio = 700, FechaVigenciaDesde = new DateOnly(2026, 9, 30) });
        await _db.SaveChangesAsync();

        Assert.DoesNotContain(await _servicio.CatalogoAsync(), a => a.Precio == 700);
    }

    [Fact]
    public async Task Catalogo_ProductoConTamanosNoOfrecePrecioSinTamano()
    {
        // Modo mixto (dato inconsistente): un precio sin tamaño en un producto que requiere tamaño
        _db.Precios.Add(new Precio { IdProducto = _precioFrescoPequeno.IdProducto, MontoPrecio = 500, FechaVigenciaDesde = InicioVigencia });
        await _db.SaveChangesAsync();

        Assert.DoesNotContain(await _servicio.CatalogoAsync(), a => a.Nombre == "Fresco de cas" && a.Tamano == null);
    }

    // ================= Registro del pedido =================

    [Fact]
    public async Task Registrar_PedidoValido_CalculaTotalEnServidorYGuardaCabeceraYDetalle()
    {
        var solicitud = Solicitud((P(_precioCasado), 2, 2800), (P(_precioFrescoGrande), 1, 1000), (B(_cocaCola), 3, 1200));
        solicitud.Observaciones = "  Sin cebolla  ";

        var resultado = await _servicio.RegistrarAsync(solicitud, UsuarioOperadora);

        Assert.Equal(2 * 2800 + 1000 + 3 * 1200, resultado.Total);
        var pedido = await _db.Pedidos.AsNoTracking().Include(p => p.Detalles).SingleAsync();
        Assert.Equal(resultado.IdPedido, pedido.IdPedido);
        Assert.Equal("DEMO001", pedido.CodigoColaborador);
        Assert.Equal("Ana Solís (prueba)", pedido.NombreColaborador);
        Assert.Equal(UsuarioOperadora, pedido.IdUsuarioRegistro);
        Assert.Equal(Ahora.UtcDateTime, pedido.FechaRegistroUtc);
        Assert.Equal("Almuerzo", pedido.TipoComida);
        Assert.Equal("Sin cebolla", pedido.Observaciones);
        Assert.True(pedido.EsPrueba);
        Assert.Equal(3, pedido.Detalles.Count);
        Assert.All(pedido.Detalles, d => Assert.Equal((long)d.Cantidad * d.PrecioUnitario, d.Subtotal));
        Assert.Equal(pedido.Total, pedido.Detalles.Sum(d => d.Subtotal));
    }

    [Fact]
    public async Task Registrar_DetalleIdentificaProductoOBebidaYCopiaNombreYTamano()
    {
        await _servicio.RegistrarAsync(Solicitud((P(_precioFrescoPequeno), 1, 600), (B(_cocaCola), 1, 1200)), UsuarioOperadora);

        var detalles = await _db.DetallesPedido.AsNoTracking().ToListAsync();
        var fresco = detalles.Single(d => d.IdPrecio == _precioFrescoPequeno.IdPrecio);
        Assert.Null(fresco.IdBebida);
        Assert.Equal("Fresco de cas", fresco.NombreArticulo);
        Assert.Equal("Pequeño", fresco.NombreTamano);
        var bebida = detalles.Single(d => d.IdBebida == _cocaCola.IdBebida);
        Assert.Null(bebida.IdPrecio);
        Assert.Equal("600ml", bebida.NombreTamano);
    }

    [Fact]
    public async Task Registrar_ProductoConVariosTamanos_CobraElPrecioDeCadaTamano()
    {
        var resultado = await _servicio.RegistrarAsync(
            Solicitud((P(_precioFrescoPequeno), 2, 600), (P(_precioFrescoGrande), 1, 1000)), UsuarioOperadora);

        Assert.Equal(2 * 600 + 1000, resultado.Total);
    }

    [Theory]
    [InlineData("Desayuno")]
    [InlineData("Almuerzo")]
    [InlineData("Merienda")] // se muestra como "Café"
    public async Task Registrar_TiposDeComidaPermitidos_SonAceptados(string tipo)
    {
        var solicitud = Solicitud((P(_precioCasado), 1, 2800));
        solicitud.TipoComida = tipo;

        var resultado = await _servicio.RegistrarAsync(solicitud, UsuarioOperadora);

        Assert.True(resultado.IdPedido > 0);
    }

    [Fact]
    public async Task Registrar_CambiarElCatalogoDespues_NoModificaElPedidoGuardado()
    {
        await _servicio.RegistrarAsync(Solicitud((P(_precioCasado), 1, 2800)), UsuarioOperadora);

        _precioCasado.MontoPrecio = 3500;
        _precioCasado.Producto!.NombreProducto = "Casado especial";
        await _db.SaveChangesAsync();

        var detalle = await _db.DetallesPedido.AsNoTracking().SingleAsync();
        Assert.Equal(2800, detalle.PrecioUnitario);
        Assert.Equal("Casado", detalle.NombreArticulo);
    }

    [Fact]
    public async Task Registrar_MismoTokenDosVeces_NoDuplicaElPedido()
    {
        var solicitud = Solicitud((P(_precioCasado), 1, 2800));

        var primero = await _servicio.RegistrarAsync(solicitud, UsuarioOperadora);
        var reintento = await _servicio.RegistrarAsync(solicitud, UsuarioOperadora);

        Assert.Equal(primero, reintento);
        Assert.Equal(1, await _db.Pedidos.CountAsync());
    }

    [Fact]
    public async Task Registrar_MismoTokenDesdeOtraSesion_EsRechazado()
    {
        var solicitud = Solicitud((P(_precioCasado), 1, 2800));
        await _servicio.RegistrarAsync(solicitud, UsuarioOperadora);

        Assert.Contains("otra sesión", await MensajeDeErrorAsync(solicitud, "otro-usuario"));
    }

    // ---------- Validaciones: nada se guarda ----------

    [Fact]
    public async Task Registrar_SinArticulos_EsRechazado()
    {
        Assert.Contains("al menos un artículo", await MensajeDeErrorAsync(Solicitud()));
        Assert.Empty(_db.Pedidos);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Registrar_CantidadNoPositiva_EsRechazada(int cantidad)
    {
        Assert.Contains("enteros positivos", await MensajeDeErrorAsync(Solicitud((P(_precioCasado), cantidad, 2800))));
        Assert.Empty(_db.Pedidos);
    }

    [Fact]
    public async Task Registrar_ArticuloRepetido_EsRechazado()
    {
        var mensaje = await MensajeDeErrorAsync(Solicitud((P(_precioCasado), 1, 2800), (P(_precioCasado), 2, 2800)));

        Assert.Contains("repetido", mensaje);
        Assert.Empty(_db.Pedidos);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Cena")]
    public async Task Registrar_TipoDeComidaInvalido_EsRechazado(string tipo)
    {
        var solicitud = Solicitud((P(_precioCasado), 1, 2800));
        solicitud.TipoComida = tipo;

        Assert.Contains("tipo de comida", await MensajeDeErrorAsync(solicitud));
    }

    [Fact]
    public async Task Registrar_ObservacionesDeMasDe500Caracteres_EsRechazado()
    {
        var solicitud = Solicitud((P(_precioCasado), 1, 2800));
        solicitud.Observaciones = new string('x', 501);

        Assert.Contains("500", await MensajeDeErrorAsync(solicitud));
    }

    [Fact]
    public async Task Registrar_SinSesion_EsRechazado()
    {
        Assert.Contains("Inicie sesión", await MensajeDeErrorAsync(Solicitud((P(_precioCasado), 1, 2800)), usuario: ""));
    }

    [Fact]
    public async Task Registrar_SinIdentificadorDeIntento_EsRechazado()
    {
        var solicitud = Solicitud((P(_precioCasado), 1, 2800));
        solicitud.TokenRegistro = Guid.Empty;

        Assert.Contains("Recargue", await MensajeDeErrorAsync(solicitud));
    }

    [Fact]
    public async Task Registrar_ColaboradorInactivo_EsRechazado()
    {
        var solicitud = Solicitud((P(_precioCasado), 1, 2800));
        solicitud.CodigoColaborador = "DEMO003";

        Assert.Contains("inactivo", await MensajeDeErrorAsync(solicitud));
        Assert.Empty(_db.Pedidos);
    }

    [Fact]
    public async Task Registrar_PrecioMostradoDistintoAlVigente_EsRechazadoYNoSeCobra()
    {
        var mensaje = await MensajeDeErrorAsync(Solicitud((P(_precioCasado), 1, 2500)));

        Assert.Contains("Cambió un precio", mensaje);
        Assert.Empty(_db.Pedidos);
    }

    [Fact]
    public async Task Registrar_ArticuloQueDejoDeEstarDisponible_RechazaTodoElPedido()
    {
        _cocaCola.Activo = false;
        await _db.SaveChangesAsync();

        var mensaje = await MensajeDeErrorAsync(Solicitud((P(_precioCasado), 1, 2800), (B(_cocaCola), 1, 1200)));

        Assert.Contains("dejó de estar disponible", mensaje);
        Assert.Empty(_db.Pedidos);
        Assert.Empty(_db.DetallesPedido);
    }

    [Fact]
    public async Task Registrar_ClaveInexistente_EsRechazado()
    {
        Assert.Contains("dejó de estar disponible", await MensajeDeErrorAsync(Solicitud(("P:99999", 1, 1000))));
    }

    // ================= Controlador =================

    private PedidosController Controlador(string? usuarioId = UsuarioOperadora)
    {
        var controlador = new PedidosController(_servicio, new EntornoPrueba(true), NullLogger<PedidosController>.Instance);
        var identidad = usuarioId is null
            ? new ClaimsIdentity()
            : new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, usuarioId)], "Identity.Application");
        controlador.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identidad) }
        };
        return controlador;
    }

    private static string? Propiedad(object? valor, string nombre) =>
        valor?.GetType().GetProperty(nombre)?.GetValue(valor)?.ToString();

    [Fact]
    public async Task Controlador_Registrar_PedidoValido_DevuelveNumeroYTotal()
    {
        var resultado = await Controlador().Registrar(Solicitud((P(_precioCasado), 2, 2800)), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(resultado);
        Assert.Equal("5600", Propiedad(ok.Value, "Total"));
        Assert.Equal((await _db.Pedidos.SingleAsync()).IdPedido.ToString(), Propiedad(ok.Value, "IdPedido"));
    }

    [Fact]
    public async Task Controlador_Registrar_UsaElUsuarioDeLaSesionComoOperadora()
    {
        await Controlador().Registrar(Solicitud((P(_precioCasado), 1, 2800)), CancellationToken.None);

        Assert.Equal(UsuarioOperadora, (await _db.Pedidos.SingleAsync()).IdUsuarioRegistro);
    }

    [Fact]
    public async Task Controlador_Registrar_SolicitudVacia_DevuelveBadRequest()
    {
        var resultado = await Controlador().Registrar(null, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(resultado);
    }

    [Fact]
    public async Task Controlador_Registrar_ErrorDeNegocio_DevuelveBadRequestConMensaje()
    {
        var solicitud = Solicitud((P(_precioCasado), 1, 2800));
        solicitud.CodigoColaborador = "DEMO003";

        var resultado = await Controlador().Registrar(solicitud, CancellationToken.None);

        var bad = Assert.IsType<BadRequestObjectResult>(resultado);
        Assert.Contains("inactivo", Propiedad(bad.Value, "mensaje"));
    }

    [Fact]
    public async Task Controlador_Registrar_SinSesion_NoRegistra()
    {
        var resultado = await Controlador(usuarioId: null).Registrar(Solicitud((P(_precioCasado), 1, 2800)), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(resultado);
        Assert.Empty(_db.Pedidos);
    }

    [Fact]
    public async Task Controlador_BuscarColaborador_DevuelveDatosOError()
    {
        var controlador = Controlador();

        var ok = Assert.IsType<OkObjectResult>(await controlador.Colaborador("DEMO001", CancellationToken.None));
        Assert.Equal("DEMO001", Assert.IsType<Colaborador>(ok.Value).Codigo);
        Assert.IsType<BadRequestObjectResult>(await controlador.Colaborador("DEMO999", CancellationToken.None));
    }

    [Fact]
    public async Task Controlador_BuscarYConsultarCatalogo_NoInsertanPedidos()
    {
        var controlador = Controlador();

        await controlador.Colaborador("DEMO001", CancellationToken.None);
        await controlador.Catalogo(CancellationToken.None);

        Assert.Empty(_db.Pedidos);
    }

    [Fact]
    public void Controlador_Registrar_ExigePostYTokenAntifalsificacion()
    {
        var metodo = typeof(PedidosController).GetMethod(nameof(PedidosController.Registrar))!;

        Assert.NotNull(metodo.GetCustomAttribute<HttpPostAttribute>());
        Assert.NotNull(metodo.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>());
    }

    [Fact]
    public void Controlador_SoloSodaUsuarioYAdministradorRegistranPedidos()
    {
        var roles = typeof(PedidosController).GetCustomAttribute<AuthorizeAttribute>()!.Roles!
            .Split(',').Select(r => r.Trim()).Order();

        Assert.Equal(new[] { "Administrador", "Soda"}, roles);
    }

    [Fact]
    public void SolicitudDePedido_ValidaCamposObligatoriosYCantidadesPositivas()
    {
        var solicitud = new RegistrarPedidoRequest
        {
            CodigoColaborador = "", TipoComida = "",
            Lineas = [new LineaPedidoRequest { Clave = "P:1", Cantidad = 0, PrecioMostrado = 0 }]
        };
        var errores = new List<System.ComponentModel.DataAnnotations.ValidationResult>();

        System.ComponentModel.DataAnnotations.Validator.TryValidateObject(
            solicitud, new System.ComponentModel.DataAnnotations.ValidationContext(solicitud), errores, true);
        System.ComponentModel.DataAnnotations.Validator.TryValidateObject(
            solicitud.Lineas[0], new System.ComponentModel.DataAnnotations.ValidationContext(solicitud.Lineas[0]), errores, true);

        var campos = errores.SelectMany(e => e.MemberNames).ToHashSet();
        Assert.Contains(nameof(RegistrarPedidoRequest.CodigoColaborador), campos);
        Assert.Contains(nameof(RegistrarPedidoRequest.TipoComida), campos);
        Assert.Contains(nameof(LineaPedidoRequest.Cantidad), campos);
        Assert.Contains(nameof(LineaPedidoRequest.PrecioMostrado), campos);
    }
}
