using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using SAPS.Tests.Helpers;
using SAPS.Web.Data;
using SAPS.Web.Middleware;
using SAPS.Web.Models.Identity;

namespace SAPS.Tests;

/// <summary>
/// HU-001, criterio 7 | Contraseña vencida a los 10 meses. El middleware es el único punto
/// de control (no se toca Login.cshtml.cs): revisa cada solicitud autenticada.
/// </summary>
public class HU001_VencimientoContrasenaTests
{
    private const string IdUsuario = "user-1";

    private static DefaultHttpContext CrearContexto(string ruta, bool autenticado = true)
    {
        var http = new DefaultHttpContext();
        http.Request.Path = ruta;
        http.User = autenticado
            ? new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, IdUsuario)], "prueba"))
            : new ClaimsPrincipal(new ClaimsIdentity());
        return http;
    }

    private static async Task<bool> EjecutarAsync(ApplicationDbContext db, DefaultHttpContext http, RelojFijo reloj)
    {
        var siguienteLlamado = false;
        var middleware = new VencimientoContrasenaMiddleware(_ => { siguienteLlamado = true; return Task.CompletedTask; });
        await middleware.InvokeAsync(http, db, reloj);
        return siguienteLlamado;
    }

    [Fact]
    public async Task SinFilaDeEstado_CreaLaFilaConHoyYDejaPasar()
    {
        using var db = TestServices.CrearContextoInMemory();
        var reloj = new RelojFijo(new DateTimeOffset(2026, 10, 9, 0, 0, 0, TimeSpan.Zero));
        var http = CrearContexto("/Home/Index");

        var dejoPasar = await EjecutarAsync(db, http, reloj);

        Assert.True(dejoPasar);
        var estado = await db.EstadosContrasena.SingleAsync(e => e.IdUsuario == IdUsuario);
        Assert.Equal(reloj.GetUtcNow().UtcDateTime, estado.FechaUltimoCambio);
    }

    [Fact]
    public async Task ConFilaReciente_DejaPasar()
    {
        using var db = TestServices.CrearContextoInMemory();
        var reloj = new RelojFijo(new DateTimeOffset(2026, 10, 9, 0, 0, 0, TimeSpan.Zero));
        db.EstadosContrasena.Add(new EstadoContrasenaUsuario { IdUsuario = IdUsuario, FechaUltimoCambio = reloj.GetUtcNow().UtcDateTime.AddMonths(-1) });
        await db.SaveChangesAsync();
        var http = CrearContexto("/Home/Index");

        var dejoPasar = await EjecutarAsync(db, http, reloj);

        Assert.True(dejoPasar);
    }

    [Fact]
    public async Task ConFilaDeMasDeDiezMeses_RedirigeACambiarContrasenaObligatorio()
    {
        using var db = TestServices.CrearContextoInMemory();
        var reloj = new RelojFijo(new DateTimeOffset(2026, 10, 9, 0, 0, 0, TimeSpan.Zero));
        db.EstadosContrasena.Add(new EstadoContrasenaUsuario { IdUsuario = IdUsuario, FechaUltimoCambio = reloj.GetUtcNow().UtcDateTime.AddMonths(-10) });
        await db.SaveChangesAsync();
        var http = CrearContexto("/Pedidos/Index");

        var dejoPasar = await EjecutarAsync(db, http, reloj);

        Assert.False(dejoPasar);
        Assert.Equal(StatusCodes.Status302Found, http.Response.StatusCode);
        Assert.Equal("/Identity/Account/CambiarContrasena?obligatorio=true", http.Response.Headers.Location.ToString());
    }

    [Fact]
    public async Task ConContrasenaVencida_LaPaginaDeCambiarContrasenaSigueSiendoAlcanzable()
    {
        using var db = TestServices.CrearContextoInMemory();
        var reloj = new RelojFijo(new DateTimeOffset(2026, 10, 9, 0, 0, 0, TimeSpan.Zero));
        db.EstadosContrasena.Add(new EstadoContrasenaUsuario { IdUsuario = IdUsuario, FechaUltimoCambio = reloj.GetUtcNow().UtcDateTime.AddMonths(-11) });
        await db.SaveChangesAsync();
        var http = CrearContexto("/Identity/Account/CambiarContrasena");

        var dejoPasar = await EjecutarAsync(db, http, reloj);

        Assert.True(dejoPasar);
    }

    [Fact]
    public async Task ConContrasenaVencida_UnArchivoEstaticoSigueSiendoAlcanzable()
    {
        using var db = TestServices.CrearContextoInMemory();
        var reloj = new RelojFijo(new DateTimeOffset(2026, 10, 9, 0, 0, 0, TimeSpan.Zero));
        db.EstadosContrasena.Add(new EstadoContrasenaUsuario { IdUsuario = IdUsuario, FechaUltimoCambio = reloj.GetUtcNow().UtcDateTime.AddMonths(-11) });
        await db.SaveChangesAsync();
        var http = CrearContexto("/css/site.css");

        var dejoPasar = await EjecutarAsync(db, http, reloj);

        Assert.True(dejoPasar);
    }

    [Fact]
    public async Task UsuarioAnonimo_DejaPasarSinConsultarLaBase()
    {
        using var db = TestServices.CrearContextoInMemory();
        var reloj = new RelojFijo(new DateTimeOffset(2026, 10, 9, 0, 0, 0, TimeSpan.Zero));
        var http = CrearContexto("/Home/Index", autenticado: false);

        var dejoPasar = await EjecutarAsync(db, http, reloj);

        Assert.True(dejoPasar);
        Assert.Empty(db.EstadosContrasena);
    }
}
