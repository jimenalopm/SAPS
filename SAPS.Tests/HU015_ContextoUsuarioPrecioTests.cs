using System.Security.Claims;
using System.Text;
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
/// HU-015 criterio 1 (task 18): al cambiar un precio desde el catálogo, la aplicación le pasa al trigger quién hace
/// el cambio (CONTEXT_INFO "idUsuario|nombre|rol") en la misma transacción del guardado y lo limpia al terminar.
/// El registro en la bitácora lo escribe el trigger (docs/Bitacora-Diseno-HU004-HU015.md, sección 8).
/// </summary>
public class HU015_ContextoUsuarioPrecioTests
{
    // ---------- Formato del contexto ----------

    [Fact]
    public void Formatear_ArmaIdNombreYRol()
    {
        Assert.Equal("u-123|ADM001|Administrador", ContextoAuditoria.Formatear("u-123", "ADM001", "Administrador"));
    }

    [Fact]
    public void Formatear_QuitaElSeparadorDeLosValores()
    {
        // El trigger parte el texto por "|": un nombre con ese carácter correría el rol de lugar.
        var texto = ContextoAuditoria.Formatear("u-1", "ana|admin", "Administrador");

        Assert.Equal("u-1|ana/admin|Administrador", texto);
        Assert.Equal(2, texto.Count(c => c == '|'));
    }

    [Fact]
    public void Formatear_CambiaLosCaracteresNoAsciiPorInterrogacion()
    {
        Assert.Equal("u-1|Mar?a|Administrador", ContextoAuditoria.Formatear("u-1", "María", "Administrador"));
    }

    [Fact]
    public void Formatear_NombreMuyLargo_CabeEn128BytesYConservaIdYRol()
    {
        var id = Guid.NewGuid().ToString();
        var texto = ContextoAuditoria.Formatear(id, new string('a', 300), "Administrador");

        Assert.True(Encoding.ASCII.GetByteCount(texto) <= ContextoAuditoria.MaximoBytes);
        Assert.StartsWith(id + "|", texto);
        Assert.EndsWith("|Administrador", texto);
    }

    [Fact]
    public void Formatear_ConClaims_TomaIdNombreYRolDeLaCuenta()
    {
        Assert.Equal("u-9|ADM001|Administrador", ContextoAuditoria.Formatear(UsuarioAdmin()));
    }

    [Fact]
    public void Formatear_SinSesion_NoDevuelveNada()
    {
        Assert.Null(ContextoAuditoria.Formatear(new ClaimsPrincipal(new ClaimsIdentity())));
        Assert.Null(ContextoAuditoria.Formatear(null));
    }

    [Fact]
    public async Task ConOtroProveedorQueNoEsSqlServer_NoHaceNada()
    {
        await using var db = TestServices.CrearContextoInMemory();
        var servicio = new ContextoAuditoria();

        // CONTEXT_INFO solo existe en SQL Server: con InMemory no debe lanzar ni ejecutar SQL.
        await servicio.EstablecerAsync(db.Database, UsuarioAdmin());
        await servicio.LimpiarAsync(db.Database);
    }

    // ---------- Pantalla del catálogo: producto ----------

    [Fact]
    public async Task EditarProducto_CambioDePrecio_DejaElUsuarioAntesDeGuardarYLoLimpiaDespues()
    {
        await using var db = TestServices.CrearContextoInMemory();
        var (cat, producto) = await SembrarProductoAsync(db, 2500);
        var contexto = new ContextoFalso(db);

        var resultado = await Controlador(db, contexto).EditarProducto(new ProductoFormViewModel
        {
            IdProducto = producto.IdProducto, NombreProducto = "Casado", IdCategoria = cat.IdCategoria, PrecioUnico = 2800
        });

        Assert.IsType<RedirectToActionResult>(resultado);
        Assert.Equal(new[] { "Establecer:u-9|ADM001|Administrador", "Limpiar" }, contexto.Llamadas);
        // Al establecer todavía no estaba el precio nuevo; al limpiar ya estaba guardado: el usuario cubre todo el guardado.
        Assert.Equal(1, contexto.PreciosAlEstablecer);
        Assert.Equal(2, contexto.PreciosAlLimpiar);
        Assert.Equal(2800, (await db.Precios.AsNoTracking().SingleAsync(p => p.Activo)).MontoPrecio);
    }

    [Fact]
    public async Task EditarProducto_SinCambioDePrecio_NoCreaPrecioNuevo()
    {
        await using var db = TestServices.CrearContextoInMemory();
        var (cat, producto) = await SembrarProductoAsync(db, 2500);
        var contexto = new ContextoFalso(db);

        await Controlador(db, contexto).EditarProducto(new ProductoFormViewModel
        {
            IdProducto = producto.IdProducto, NombreProducto = "Casado", IdCategoria = cat.IdCategoria, PrecioUnico = 2500
        });

        // Sin cambio de monto no hay fila nueva que dispare el registro en la bitácora.
        Assert.Equal(1, await db.Precios.CountAsync());
        Assert.Equal(2500, (await db.Precios.AsNoTracking().SingleAsync()).MontoPrecio);
    }

    [Fact]
    public async Task EditarProducto_ConDatosInvalidos_NoTocaElContextoNiGuarda()
    {
        await using var db = TestServices.CrearContextoInMemory();
        var (cat, producto) = await SembrarProductoAsync(db, 2500);
        var contexto = new ContextoFalso(db);

        var resultado = await Controlador(db, contexto).EditarProducto(new ProductoFormViewModel
        {
            IdProducto = producto.IdProducto, NombreProducto = "Casado", IdCategoria = cat.IdCategoria, PrecioUnico = -5
        });

        Assert.IsType<ViewResult>(resultado);
        Assert.Empty(contexto.Llamadas);
        Assert.Equal(2500, (await db.Precios.AsNoTracking().SingleAsync()).MontoPrecio);
    }

    [Fact]
    public async Task EditarProducto_SiFallaElGuardado_LimpiaElContextoDeTodasFormas()
    {
        await using var db = TestServices.CrearContextoInMemory();
        var (cat, producto) = await SembrarProductoAsync(db, 2500);
        var contexto = new ContextoFalso(db);
        var controlador = Controlador(db, contexto);
        var modelo = new ProductoFormViewModel
        {
            IdProducto = producto.IdProducto, NombreProducto = "Casado", IdCategoria = cat.IdCategoria, PrecioUnico = 2800
        };
        db.SavingChanges += (_, _) => throw new InvalidOperationException("fallo simulado");

        await Assert.ThrowsAsync<InvalidOperationException>(() => controlador.EditarProducto(modelo));

        // Las conexiones se reutilizan: el usuario no puede quedar pegado en CONTEXT_INFO.
        Assert.Equal(new[] { "Establecer:u-9|ADM001|Administrador", "Limpiar" }, contexto.Llamadas);
    }

    // ---------- Pantalla del catálogo: bebida ----------

    [Fact]
    public async Task EditarBebida_CambioDePrecio_DejaElUsuarioAntesDeGuardarYLoLimpiaDespues()
    {
        await using var db = TestServices.CrearContextoInMemory();
        var bebida = new Bebida { NombreBebida = "Naranjada", TipoBebida = "Frescos", Precio = 800 };
        db.Bebidas.Add(bebida);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var contexto = new ContextoFalso(db);

        var resultado = await Controlador(db, contexto).EditarBebida(new BebidaFormViewModel
        {
            IdBebida = bebida.IdBebida, NombreBebida = "Naranjada", TipoBebida = "Frescos", Precio = 900,
        });

        Assert.IsType<RedirectToActionResult>(resultado);
        Assert.Equal(new[] { "Establecer:u-9|ADM001|Administrador", "Limpiar" }, contexto.Llamadas);
        Assert.Equal(800, contexto.PrecioBebidaAlEstablecer);
        Assert.Equal(900, contexto.PrecioBebidaAlLimpiar);
        Assert.Equal(900, (await db.Bebidas.AsNoTracking().SingleAsync()).Precio);
    }

    [Fact]
    public async Task EditarBebida_ConDatosInvalidos_NoTocaElContexto()
    {
        await using var db = TestServices.CrearContextoInMemory();
        var bebida = new Bebida { NombreBebida = "Naranjada", TipoBebida = "Frescos", Precio = 800 };
        db.Bebidas.Add(bebida);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var contexto = new ContextoFalso(db);
        var controlador = Controlador(db, contexto);
        controlador.ModelState.AddModelError(nameof(BebidaFormViewModel.Precio), "El precio es obligatorio.");

        var resultado = await controlador.EditarBebida(new BebidaFormViewModel
        {
            IdBebida = bebida.IdBebida, NombreBebida = "Naranjada", TipoBebida = "Frescos", Precio = 0,
        });

        Assert.IsType<ViewResult>(resultado);
        Assert.Empty(contexto.Llamadas);
        Assert.Equal(800, (await db.Bebidas.AsNoTracking().SingleAsync()).Precio);
    }

    // ---------- Apoyo ----------

    private static ClaimsPrincipal UsuarioAdmin() => new(new ClaimsIdentity(
        new Claim[]
        {
            new(ClaimTypes.NameIdentifier, "u-9"),
            new(ClaimTypes.Name, "ADM001"),
            new(ClaimTypes.Role, "Administrador"),
        }, "Prueba"));

    private static AdministracionController Controlador(ApplicationDbContext db, IContextoAuditoria contexto)
    {
        var controlador = TestServices.ConContexto(new AdministracionController(db, contexto), "POST");
        controlador.ControllerContext.HttpContext.User = UsuarioAdmin();
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

    /// <summary>Guarda qué se llamó y cómo estaba la base en ese momento (consulta con otro contexto, sin cache).</summary>
    private sealed class ContextoFalso(ApplicationDbContext db) : IContextoAuditoria
    {
        public List<string> Llamadas { get; } = [];
        public int PreciosAlEstablecer { get; private set; }
        public int PreciosAlLimpiar { get; private set; }
        public int PrecioBebidaAlEstablecer { get; private set; }
        public int PrecioBebidaAlLimpiar { get; private set; }

        public async Task EstablecerAsync(DatabaseFacade baseDatos, ClaimsPrincipal usuario, CancellationToken cancellationToken = default)
        {
            Llamadas.Add("Establecer:" + ContextoAuditoria.Formatear(usuario));
            PreciosAlEstablecer = await db.Precios.AsNoTracking().CountAsync(cancellationToken);
            PrecioBebidaAlEstablecer = await db.Bebidas.AsNoTracking().Select(b => b.Precio).FirstOrDefaultAsync(cancellationToken);
        }

        public async Task LimpiarAsync(DatabaseFacade baseDatos, CancellationToken cancellationToken = default)
        {
            Llamadas.Add("Limpiar");
            PreciosAlLimpiar = await db.Precios.AsNoTracking().CountAsync(cancellationToken);
            PrecioBebidaAlLimpiar = await db.Bebidas.AsNoTracking().Select(b => b.Precio).FirstOrDefaultAsync(cancellationToken);
        }
    }
}
