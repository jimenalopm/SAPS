using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using SAPS.Tests.Helpers;
using SAPS.Web.Areas.Identity.Pages.Account;
using SAPS.Web.Data;
using SAPS.Web.Middleware;
using SAPS.Web.Models.Identity;
using SAPS.Web.Models.Rrhh;

namespace SAPS.Tests;

/// <summary>
/// HU-001 (RNF-006) | El middleware también rechaza: colaborador vinculado inactivo y cuenta
/// desactivada/bloqueada por un Administrador; y fuerza el cambio en el primer ingreso aunque la
/// contraseña temporal todavía no esté "vencida" por fecha (se crea con FechaUltimoCambio = ahora).
/// </summary>
public class HU001_ColaboradorInactivoTests
{
    private const string IdUsuario = "user-1";
    private static readonly DateTimeOffset Ahora = new(2026, 10, 9, 0, 0, 0, TimeSpan.Zero);

    private static DefaultHttpContext CrearContextoAutenticado(string ruta)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthentication(IdentityConstants.ApplicationScheme)
                .AddCookie(IdentityConstants.ApplicationScheme);
        var provider = services.BuildServiceProvider();

        var http = new DefaultHttpContext { RequestServices = provider };
        http.Request.Path = ruta;
        http.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, IdUsuario)], "prueba"));
        return http;
    }

    private static async Task<bool> EjecutarAsync(ApplicationDbContext db, DefaultHttpContext http, RelojFijo? reloj = null)
    {
        var siguienteLlamado = false;
        var middleware = new VencimientoContrasenaMiddleware(_ => { siguienteLlamado = true; return Task.CompletedTask; });
        await middleware.InvokeAsync(http, db, reloj ?? new RelojFijo(Ahora));
        return siguienteLlamado;
    }

    private static async Task AgregarColaboradorVinculadoAsync(ApplicationDbContext db, bool colaboradorActivo)
    {
        db.Users.Add(new IdentityUser { Id = IdUsuario, UserName = "empleado" });
        db.Colaboradores.Add(new ColaboradorRrhh { IdColaborador = 1, Codigo = "C001", NombreCompleto = "Prueba", EstaActivo = colaboradorActivo, FechaRegistro = Ahora.UtcDateTime });
        db.VinculosUsuarioColaborador.Add(new VinculoUsuarioColaborador { IdUsuario = IdUsuario, IdColaborador = 1, FechaVinculacion = Ahora.UtcDateTime });
        await db.SaveChangesAsync();
    }

    // ---------- Middleware: colaborador vinculado ----------

    [Fact]
    public async Task ColaboradorVinculadoInactivo_RedirigeALoginYCierraSesion()
    {
        using var db = TestServices.CrearContextoInMemory();
        await AgregarColaboradorVinculadoAsync(db, colaboradorActivo: false);
        var http = CrearContextoAutenticado("/Home/Index");

        var dejoPasar = await EjecutarAsync(db, http);

        Assert.False(dejoPasar);
        Assert.Equal(StatusCodes.Status302Found, http.Response.StatusCode);
        Assert.Equal("/Identity/Account/Login?colaboradorInactivo=true", http.Response.Headers.Location.ToString());
    }

    [Fact]
    public async Task ColaboradorVinculadoActivo_DejaPasar()
    {
        using var db = TestServices.CrearContextoInMemory();
        await AgregarColaboradorVinculadoAsync(db, colaboradorActivo: true);
        var http = CrearContextoAutenticado("/Home/Index");

        var dejoPasar = await EjecutarAsync(db, http);

        Assert.True(dejoPasar);
    }

    [Fact]
    public async Task SinVinculoAColaborador_DejaPasar()
    {
        using var db = TestServices.CrearContextoInMemory();
        db.Users.Add(new IdentityUser { Id = IdUsuario, UserName = "empleado" });
        await db.SaveChangesAsync();
        var http = CrearContextoAutenticado("/Home/Index");

        var dejoPasar = await EjecutarAsync(db, http);

        Assert.True(dejoPasar);
    }

    // ---------- Middleware: cuenta desactivada / bloqueada ----------

    [Fact]
    public async Task CuentaDesactivada_LockoutEndMaxValue_RedirigeALoginConAccesoRevocado()
    {
        using var db = TestServices.CrearContextoInMemory();
        db.Users.Add(new IdentityUser { Id = IdUsuario, UserName = "empleado", LockoutEnabled = true, LockoutEnd = DateTimeOffset.MaxValue });
        await db.SaveChangesAsync();
        var http = CrearContextoAutenticado("/Home/Index");

        var dejoPasar = await EjecutarAsync(db, http);

        Assert.False(dejoPasar);
        Assert.Equal("/Identity/Account/Login?accesoRevocado=true", http.Response.Headers.Location.ToString());
    }

    [Fact]
    public async Task CuentaBloqueadaTransitoriamente_LockoutEndEnElFuturo_TambienBloquea()
    {
        using var db = TestServices.CrearContextoInMemory();
        db.Users.Add(new IdentityUser { Id = IdUsuario, UserName = "empleado", LockoutEnabled = true, LockoutEnd = Ahora.AddMinutes(10) });
        await db.SaveChangesAsync();
        var http = CrearContextoAutenticado("/Home/Index");

        var dejoPasar = await EjecutarAsync(db, http);

        Assert.False(dejoPasar);
        Assert.Equal("/Identity/Account/Login?accesoRevocado=true", http.Response.Headers.Location.ToString());
    }

    [Fact]
    public async Task LockoutEnabledFalse_ConLockoutEndEnElFuturo_NoBloquea()
    {
        using var db = TestServices.CrearContextoInMemory();
        // Dato defensivo/inconsistente: sin LockoutEnabled no debería aplicarse ningún bloqueo.
        db.Users.Add(new IdentityUser { Id = IdUsuario, UserName = "empleado", LockoutEnabled = false, LockoutEnd = DateTimeOffset.MaxValue });
        await db.SaveChangesAsync();
        var http = CrearContextoAutenticado("/Home/Index");

        var dejoPasar = await EjecutarAsync(db, http);

        Assert.True(dejoPasar);
    }

    [Fact]
    public async Task AccesoRevocado_SeDetectaInclusoVisitandoCambiarContrasenaDirectamente()
    {
        using var db = TestServices.CrearContextoInMemory();
        db.Users.Add(new IdentityUser { Id = IdUsuario, UserName = "empleado", LockoutEnabled = true, LockoutEnd = DateTimeOffset.MaxValue });
        await db.SaveChangesAsync();
        var http = CrearContextoAutenticado("/Identity/Account/CambiarContrasena");

        var dejoPasar = await EjecutarAsync(db, http);

        Assert.False(dejoPasar);
        Assert.Equal("/Identity/Account/Login?accesoRevocado=true", http.Response.Headers.Location.ToString());
    }

    // ---------- Middleware: primer ingreso vs. vencimiento normal ----------

    [Fact]
    public async Task PrimerIngreso_RequiereCambioInicial_RedirigeAunqueNoEsteVencidaPorFecha()
    {
        using var db = TestServices.CrearContextoInMemory();
        db.EstadosContrasena.Add(new EstadoContrasenaUsuario
        {
            IdUsuario = IdUsuario,
            FechaUltimoCambio = Ahora.UtcDateTime, // recién creada, no vencida por fecha
            RequiereCambioInicial = true,
        });
        await db.SaveChangesAsync();
        var http = CrearContextoAutenticado("/Home/Index");

        var dejoPasar = await EjecutarAsync(db, http);

        Assert.False(dejoPasar);
        Assert.Equal("/Identity/Account/CambiarContrasena?obligatorio=true&motivo=primerIngreso", http.Response.Headers.Location.ToString());
    }

    [Fact]
    public async Task ContrasenaVencidaNormal_SinRequiereCambioInicial_PreservaLaCadenaLiteralSinMotivo()
    {
        using var db = TestServices.CrearContextoInMemory();
        db.EstadosContrasena.Add(new EstadoContrasenaUsuario
        {
            IdUsuario = IdUsuario,
            FechaUltimoCambio = Ahora.UtcDateTime.AddMonths(-10),
            RequiereCambioInicial = false,
        });
        await db.SaveChangesAsync();
        var http = CrearContextoAutenticado("/Home/Index");

        var dejoPasar = await EjecutarAsync(db, http);

        Assert.False(dejoPasar);
        Assert.Equal("/Identity/Account/CambiarContrasena?obligatorio=true", http.Response.Headers.Location.ToString());
    }

    // ---------- Login: colaborador inactivo ----------

    [Fact]
    public async Task Login_ConColaboradorVinculadoInactivo_NoRedirigeYMuestraError()
    {
        var sp = TestServices.Crear();
        var um = sp.GetRequiredService<UserManager<IdentityUser>>();
        var sm = sp.GetRequiredService<SignInManager<IdentityUser>>();
        var db = sp.GetRequiredService<ApplicationDbContext>();

        var usuario = new IdentityUser { UserName = "EMP900" };
        Assert.True((await um.CreateAsync(usuario, "Prueba123")).Succeeded);
        db.Colaboradores.Add(new ColaboradorRrhh { IdColaborador = 1, Codigo = "C900", NombreCompleto = "Prueba", EstaActivo = false, FechaRegistro = DateTime.UtcNow });
        db.VinculosUsuarioColaborador.Add(new VinculoUsuarioColaborador { IdUsuario = usuario.Id, IdColaborador = 1, FechaVinculacion = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var pagina = new LoginModel(sm, db, NullLogger<LoginModel>.Instance)
        {
            PageContext = new PageContext { HttpContext = new DefaultHttpContext() },
            Input = new LoginModel.InputModel { CodigoEmpleado = "EMP900", Password = "Prueba123" }
        };

        var resultado = await pagina.OnPostAsync("/");

        Assert.IsType<PageResult>(resultado);
        Assert.Contains(pagina.ModelState[string.Empty]!.Errors, e => e.ErrorMessage.Contains("colaborador está inactivo"));
    }

    [Fact]
    public async Task Login_ConColaboradorVinculadoActivo_RedirigeNormalmente()
    {
        var sp = TestServices.Crear();
        var um = sp.GetRequiredService<UserManager<IdentityUser>>();
        var sm = sp.GetRequiredService<SignInManager<IdentityUser>>();
        var db = sp.GetRequiredService<ApplicationDbContext>();

        var usuario = new IdentityUser { UserName = "EMP901" };
        Assert.True((await um.CreateAsync(usuario, "Prueba123")).Succeeded);
        db.Colaboradores.Add(new ColaboradorRrhh { IdColaborador = 2, Codigo = "C901", NombreCompleto = "Prueba", EstaActivo = true, FechaRegistro = DateTime.UtcNow });
        db.VinculosUsuarioColaborador.Add(new VinculoUsuarioColaborador { IdUsuario = usuario.Id, IdColaborador = 2, FechaVinculacion = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var pagina = new LoginModel(sm, db, NullLogger<LoginModel>.Instance)
        {
            PageContext = new PageContext { HttpContext = new DefaultHttpContext() },
            Input = new LoginModel.InputModel { CodigoEmpleado = "EMP901", Password = "Prueba123" }
        };

        var resultado = await pagina.OnPostAsync("/Home/Index");

        var redirect = Assert.IsType<LocalRedirectResult>(resultado);
        Assert.Equal("/Home/Index", redirect.Url);
    }

    // ---------- CambiarContrasena: apaga el flag de primer ingreso ----------

    [Fact]
    public async Task CambiarContrasena_TrasExito_ApagaRequiereCambioInicial()
    {
        var sp = TestServices.Crear();
        var um = sp.GetRequiredService<UserManager<IdentityUser>>();
        var sm = sp.GetRequiredService<SignInManager<IdentityUser>>();
        var db = sp.GetRequiredService<ApplicationDbContext>();

        var usuario = new IdentityUser { UserName = "EMP902" };
        Assert.True((await um.CreateAsync(usuario, "Temporal123")).Succeeded);
        db.EstadosContrasena.Add(new EstadoContrasenaUsuario { IdUsuario = usuario.Id, FechaUltimoCambio = DateTime.UtcNow, RequiereCambioInicial = true });
        await db.SaveChangesAsync();

        var http = new DefaultHttpContext { User = await sm.CreateUserPrincipalAsync(usuario) };
        var pagina = new CambiarContrasenaModel(um, sm, db, new RelojFijo(Ahora), NullLogger<CambiarContrasenaModel>.Instance)
        {
            PageContext = new PageContext { HttpContext = http },
            Obligatorio = true,
            Motivo = "primerIngreso",
            Input = new CambiarContrasenaModel.InputModel
            {
                ContrasenaActual = "Temporal123",
                ContrasenaNueva = "Definitiva456",
                ConfirmarContrasenaNueva = "Definitiva456",
            }
        };

        var resultado = await pagina.OnPostAsync();

        Assert.IsType<LocalRedirectResult>(resultado);
        var estado = await db.EstadosContrasena.SingleAsync(e => e.IdUsuario == usuario.Id);
        Assert.False(estado.RequiereCambioInicial);
    }
}
