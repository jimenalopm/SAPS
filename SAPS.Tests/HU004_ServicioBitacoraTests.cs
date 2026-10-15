using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SAPS.Tests.Helpers;
using SAPS.Web.Areas.Identity.Pages.Account;
using SAPS.Web.Data;
using SAPS.Web.Models.Auditoria;
using SAPS.Web.Services.Auditoria;

namespace SAPS.Tests;

/// <summary>
/// HU-004 | Registrar logs de actividad y accesos. Pruebas unitarias del servicio central de bitácora
/// y de su uso en inicio y cierre de sesión (criterios 1, 4, 7 y 8). Los triggers y el bloqueo de
/// UPDATE/DELETE no se pueden probar aquí (InMemory no los ejecuta): ver tests/SAPS.Bitacora.IntegrationTests.
/// </summary>
public class HU004_ServicioBitacoraTests
{
    /// <summary>
    /// InMemory no aplica los DEFAULT de SQL Server (SUSER_SNAME()) y rechaza UsuarioBd nulo. Este
    /// interceptor emula ese default solo en las pruebas; en producción lo asigna la base de datos.
    /// </summary>
    private sealed class DefaultUsuarioBdInterceptor : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken ct = default)
        {
            foreach (var entrada in eventData.Context!.ChangeTracker.Entries<Bitacora>()
                         .Where(e => e.State == EntityState.Added && e.Entity.UsuarioBd is null))
                entrada.Entity.UsuarioBd = "usuario_prueba";
            return base.SavingChangesAsync(eventData, result, ct);
        }
    }

    private static DbContextOptions<ApplicationDbContext> ConDefaultUsuarioBd(DbContextOptions<ApplicationDbContext> opciones) =>
        new DbContextOptionsBuilder<ApplicationDbContext>(opciones).AddInterceptors(new DefaultUsuarioBdInterceptor()).Options;

    private static (ServicioBitacora servicio, DbContextOptions<ApplicationDbContext> opciones, AccesorHttpContextFijo accesor) CrearServicio(
        HttpContext? http = null)
    {
        var opciones = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .AddInterceptors(new DefaultUsuarioBdInterceptor())
            .Options;
        var accesor = new AccesorHttpContextFijo { HttpContext = http ?? new DefaultHttpContext() };
        return (new ServicioBitacora(opciones, accesor, NullLogger<ServicioBitacora>.Instance), opciones, accesor);
    }

    private static List<Bitacora> Leer(DbContextOptions<ApplicationDbContext> opciones)
    {
        using var db = new ApplicationDbContext(opciones);
        return db.Bitacoras.AsNoTracking().ToList();
    }

    private static HttpContext ContextoAutenticado(string id, string nombre, string rol, string ip = "10.0.0.7", string agente = "Mozilla/5.0 (X11; Linux)")
    {
        var http = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, id),
                new Claim(ClaimTypes.Name, nombre),
                new Claim(ClaimTypes.Role, rol),
            ], "prueba")),
        };
        http.Connection.RemoteIpAddress = IPAddress.Parse(ip);
        http.Request.Headers.UserAgent = agente;
        return http;
    }

    // ---------- Servicio ----------

    [Fact]
    public async Task Registrar_ConTodosLosCampos_LosGuardaTalCual()
    {
        var (servicio, opciones, _) = CrearServicio();

        var ok = await servicio.RegistrarAsync(new EventoBitacora("Catalogo", "CambioPrecio", "tb_Precio", true)
        {
            IdRegistro = "12",
            ValorAnterior = "1500",
            ValorNuevo = "1600",
            Descripcion = "Casado grande",
            DireccionIp = "192.168.1.20",
            Dispositivo = "Firefox en Linux",
            IdUsuario = "u-1",
            NombreUsuario = "ADM001",
            Rol = "Administrador",
        });

        Assert.True(ok);
        var fila = Assert.Single(Leer(opciones));
        Assert.Equal("Catalogo", fila.Modulo);
        Assert.Equal("CambioPrecio", fila.TipoEvento);
        Assert.Equal("tb_Precio", fila.Recurso);
        Assert.Equal("12", fila.IdRegistro);
        Assert.True(fila.EsExitoso);
        Assert.Equal("1500", fila.ValorAnterior);
        Assert.Equal("1600", fila.ValorNuevo);
        Assert.Equal("Casado grande", fila.Descripcion);
        Assert.Equal("192.168.1.20", fila.DireccionIp);
        Assert.Equal("Firefox en Linux", fila.Dispositivo);
        Assert.Equal("u-1", fila.IdUsuario);
        Assert.Equal("ADM001", fila.NombreUsuario);
        Assert.Equal("Administrador", fila.Rol);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", "")]
    public async Task Registrar_ValoresAnteriorYNuevoVacios_SeGuardaSinError(string? anterior, string? nuevo)
    {
        var (servicio, opciones, _) = CrearServicio();

        var ok = await servicio.RegistrarAsync(new EventoBitacora("Seguridad", "InicioSesion", "Sesion", true)
        {
            ValorAnterior = anterior,
            ValorNuevo = nuevo,
        });

        Assert.True(ok);
        var fila = Assert.Single(Leer(opciones));
        Assert.True(string.IsNullOrEmpty(fila.ValorAnterior));
        Assert.True(string.IsNullOrEmpty(fila.ValorNuevo));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Registrar_GuardaElResultadoExitosoOFallido(bool exitoso)
    {
        var (servicio, opciones, _) = CrearServicio();

        await servicio.RegistrarAsync(new EventoBitacora("Seguridad", "InicioSesion", "Sesion", exitoso));

        Assert.Equal(exitoso, Assert.Single(Leer(opciones)).EsExitoso);
    }

    [Fact]
    public async Task Registrar_SinUsuarioAutenticado_GuardaLoDigitadoYSinIdNiRol()
    {
        var (servicio, opciones, _) = CrearServicio(new DefaultHttpContext());

        await servicio.RegistrarAsync(new EventoBitacora("Seguridad", "InicioSesion", "Sesion", false)
        {
            NombreUsuario = "EMP404",
            Descripcion = "Credenciales incorrectas",
        });

        var fila = Assert.Single(Leer(opciones));
        Assert.False(fila.EsExitoso);
        Assert.Null(fila.IdUsuario);
        Assert.Null(fila.Rol);
        Assert.Equal("EMP404", fila.NombreUsuario);
    }

    [Fact]
    public async Task Registrar_UsuarioRolIpYDispositivoSalenDelContexto()
    {
        var http = ContextoAutenticado("u-9", "SODA001", "Soda", ip: "10.1.2.3", agente: "Chrome 140");
        var (servicio, opciones, _) = CrearServicio(http);

        await servicio.RegistrarAsync(new EventoBitacora("Seguridad", "CierreSesion", "Sesion", true));

        var fila = Assert.Single(Leer(opciones));
        Assert.Equal("u-9", fila.IdUsuario);
        Assert.Equal("SODA001", fila.NombreUsuario);
        Assert.Equal("Soda", fila.Rol);
        Assert.Equal("10.1.2.3", fila.DireccionIp);
        Assert.Equal("Chrome 140", fila.Dispositivo);
    }

    [Fact]
    public async Task Registrar_ElActorExplicitoTienePrioridadSobreElContexto()
    {
        var http = ContextoAutenticado("u-9", "SODA001", "Soda");
        var (servicio, opciones, _) = CrearServicio(http);

        await servicio.RegistrarAsync(new EventoBitacora("Seguridad", "InicioSesion", "Sesion", true)
        {
            IdUsuario = "u-1",
            NombreUsuario = "ADM001",
            Rol = "Administrador",
        });

        var fila = Assert.Single(Leer(opciones));
        Assert.Equal("u-1", fila.IdUsuario);
        Assert.Equal("ADM001", fila.NombreUsuario);
        Assert.Equal("Administrador", fila.Rol);
    }

    [Fact]
    public async Task Registrar_TipoDeEventoNuevo_SeGuardaSinCambiarLaTabla()
    {
        // Criterio 8: HU-010 (anulación de órdenes) usa los mismos campos con otro TipoEvento.
        var (servicio, opciones, _) = CrearServicio();
        using var db = new ApplicationDbContext(opciones);
        var columnasAntes = db.Model.FindEntityType(typeof(Bitacora))!.GetProperties().Select(p => p.Name).OrderBy(n => n).ToList();

        var ok = await servicio.RegistrarAsync(new EventoBitacora("Pedidos", "OrdenAnulada", "tb_Pedido", true)
        {
            IdRegistro = "345",
            ValorAnterior = "Registrado",
            ValorNuevo = "Anulado",
        });

        Assert.True(ok);
        var fila = Assert.Single(Leer(opciones));
        Assert.Equal("OrdenAnulada", fila.TipoEvento);
        Assert.Equal("Pedidos", fila.Modulo);
        var columnasDespues = db.Model.FindEntityType(typeof(Bitacora))!.GetProperties().Select(p => p.Name).OrderBy(n => n).ToList();
        Assert.Equal(columnasAntes, columnasDespues);
    }

    [Fact]
    public async Task Registrar_TextosLargos_SeRecortanALaColumnaYNoFallan()
    {
        var (servicio, opciones, _) = CrearServicio();

        var ok = await servicio.RegistrarAsync(new EventoBitacora("Seguridad", "InicioSesion", "Sesion", false)
        {
            NombreUsuario = new string('a', 400),
            Dispositivo = new string('b', 900),
            Descripcion = new string('c', 900),
            ValorNuevo = new string('d', 900),
        });

        Assert.True(ok);
        var fila = Assert.Single(Leer(opciones));
        Assert.Equal(256, fila.NombreUsuario!.Length);
        Assert.Equal(200, fila.Dispositivo!.Length);
        Assert.Equal(300, fila.Descripcion!.Length);
        Assert.Equal(500, fila.ValorNuevo!.Length);
    }

    [Theory]
    [InlineData("", "InicioSesion", "Sesion")]
    [InlineData("Seguridad", " ", "Sesion")]
    [InlineData("Seguridad", "InicioSesion", "")]
    public async Task Registrar_SinModuloTipoORecurso_NoGuardaYDevuelveFalse(string modulo, string tipo, string recurso)
    {
        var (servicio, opciones, _) = CrearServicio();

        var ok = await servicio.RegistrarAsync(new EventoBitacora(modulo, tipo, recurso, true));

        Assert.False(ok);
        Assert.Empty(Leer(opciones));
    }

    [Fact]
    public async Task Registrar_SiFallaLaEscritura_DevuelveFalseYNoLanza()
    {
        var opciones = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=127.0.0.1,1;Database=NoExiste;User Id=x;Password=x;Connect Timeout=1;Encrypt=False")
            .Options;
        var servicio = new ServicioBitacora(opciones, new AccesorHttpContextFijo(), NullLogger<ServicioBitacora>.Instance);

        var ok = await servicio.RegistrarAsync(new EventoBitacora("Seguridad", "InicioSesion", "Sesion", true));

        Assert.False(ok);
    }

    // ---------- Registro en el contenedor de DI de la aplicación ----------

    [Fact]
    public void Program_RegistraElServicioDeBitacora()
    {
        // Los constructores de Login y Logout reciben el servicio como parámetro opcional: si Program.cs
        // dejara de registrarlo, el inicio de sesión quedaría sin auditar en silencio.
        var directorio = new DirectoryInfo(AppContext.BaseDirectory);
        while (directorio is not null && !File.Exists(Path.Combine(directorio.FullName, "SAPS.Web", "Program.cs")))
            directorio = directorio.Parent;
        Assert.NotNull(directorio);

        var program = File.ReadAllText(Path.Combine(directorio!.FullName, "SAPS.Web", "Program.cs"));

        Assert.Contains("AddScoped<IServicioBitacora, ServicioBitacora>()", program);
        Assert.Contains("AddHttpContextAccessor()", program);
    }

    [Fact]
    public void ServicioBitacora_SeResuelveConLasMismasDependenciasQueLaAplicacion()
    {
        var sp = TestServices.Crear();
        var servicios = new ServiceCollection();
        servicios.AddLogging();
        servicios.AddSingleton(sp.GetRequiredService<IHttpContextAccessor>());
        servicios.AddSingleton(sp.GetRequiredService<DbContextOptions<ApplicationDbContext>>());
        servicios.AddScoped<IServicioBitacora, ServicioBitacora>();
        using var proveedor = servicios.BuildServiceProvider();
        using var alcance = proveedor.CreateScope();

        Assert.IsType<ServicioBitacora>(alcance.ServiceProvider.GetRequiredService<IServicioBitacora>());
    }

    // ---------- Inicio y cierre de sesión ----------

    private static async Task<(LoginModel pagina, IdentityUser usuario, UserManager<IdentityUser> um, DbContextOptions<ApplicationDbContext> opciones)>
        CrearLoginAsync(string codigo, string password, string? contrasenaDigitada = null, IServicioBitacora? bitacora = null)
    {
        var sp = TestServices.Crear();
        var um = sp.GetRequiredService<UserManager<IdentityUser>>();
        var sm = sp.GetRequiredService<SignInManager<IdentityUser>>();
        var db = sp.GetRequiredService<ApplicationDbContext>();
        var opciones = ConDefaultUsuarioBd(sp.GetRequiredService<DbContextOptions<ApplicationDbContext>>());
        var roles = sp.GetRequiredService<RoleManager<IdentityRole>>();

        var usuario = new IdentityUser { UserName = codigo };
        Assert.True((await um.CreateAsync(usuario, password)).Succeeded);
        await roles.CreateAsync(new IdentityRole("Soda"));
        await um.AddToRoleAsync(usuario, "Soda");

        bitacora ??= new ServicioBitacora(opciones, sp.GetRequiredService<IHttpContextAccessor>(), NullLogger<ServicioBitacora>.Instance);
        var pagina = new LoginModel(sm, db, NullLogger<LoginModel>.Instance, bitacora)
        {
            PageContext = new PageContext { HttpContext = new DefaultHttpContext { RequestServices = sp } },
            Input = new LoginModel.InputModel { CodigoEmpleado = codigo, Password = contrasenaDigitada ?? password },
        };
        return (pagina, usuario, um, opciones);
    }

    [Fact]
    public async Task Login_Exitoso_RegistraInicioDeSesionConUsuarioYRol()
    {
        var (pagina, usuario, _, opciones) = await CrearLoginAsync("EMP700", "Prueba123");

        var resultado = await pagina.OnPostAsync("/");

        Assert.IsType<LocalRedirectResult>(resultado);
        var fila = Assert.Single(Leer(opciones));
        Assert.Equal("Seguridad", fila.Modulo);
        Assert.Equal("InicioSesion", fila.TipoEvento);
        Assert.Equal("Sesion", fila.Recurso);
        Assert.True(fila.EsExitoso);
        Assert.Equal(usuario.Id, fila.IdUsuario);
        Assert.Equal("EMP700", fila.NombreUsuario);
        Assert.Equal("Soda", fila.Rol);
    }

    [Fact]
    public async Task Login_CredencialesIncorrectas_RegistraFallidoSinGuardarLaContrasena()
    {
        const string digitada = "ClaveSecreta987";
        var (pagina, usuario, _, opciones) = await CrearLoginAsync("EMP701", "Prueba123", contrasenaDigitada: digitada);

        var resultado = await pagina.OnPostAsync("/");

        Assert.IsType<PageResult>(resultado);
        var fila = Assert.Single(Leer(opciones));
        Assert.False(fila.EsExitoso);
        Assert.Equal("InicioSesion", fila.TipoEvento);
        Assert.Equal(usuario.Id, fila.IdUsuario);   // la cuenta existe: se guarda su id
        Assert.Equal("EMP701", fila.NombreUsuario);
        // Ninguna columna contiene la contraseña digitada ni la real.
        var textos = typeof(Bitacora).GetProperties().Where(p => p.PropertyType == typeof(string))
            .Select(p => (string?)p.GetValue(fila) ?? "");
        Assert.All(textos, t => Assert.DoesNotContain(digitada, t));
        Assert.All(textos, t => Assert.DoesNotContain("Prueba123", t));
    }

    [Fact]
    public async Task Login_CuentaInexistente_RegistraFallidoConLoDigitadoYSinIdDeUsuario()
    {
        var (pagina, _, _, opciones) = await CrearLoginAsync("EMP702", "Prueba123");
        pagina.Input.CodigoEmpleado = "NOEXISTE";

        await pagina.OnPostAsync("/");

        var fila = Assert.Single(Leer(opciones));
        Assert.False(fila.EsExitoso);
        Assert.Null(fila.IdUsuario);
        Assert.Null(fila.Rol);
        Assert.Equal("NOEXISTE", fila.NombreUsuario);
    }

    [Fact]
    public async Task Login_CuentaBloqueadaTemporalmente_RegistraFallido()
    {
        var (pagina, usuario, um, opciones) = await CrearLoginAsync("EMP703", "Prueba123");
        await um.SetLockoutEnabledAsync(usuario, true);
        await um.SetLockoutEndDateAsync(usuario, DateTimeOffset.UtcNow.AddMinutes(30));

        var resultado = await pagina.OnPostAsync("/");

        Assert.IsType<RedirectToPageResult>(resultado);
        var fila = Assert.Single(Leer(opciones));
        Assert.False(fila.EsExitoso);
        Assert.Equal(usuario.Id, fila.IdUsuario);
        Assert.Contains("bloqueada", fila.Descripcion);
    }

    [Fact]
    public async Task Login_CuentaDesactivadaPorElAdministrador_RegistraFallido()
    {
        var (pagina, usuario, um, opciones) = await CrearLoginAsync("EMP704", "Prueba123");
        await um.SetLockoutEnabledAsync(usuario, true);
        await um.SetLockoutEndDateAsync(usuario, DateTimeOffset.MaxValue);

        await pagina.OnPostAsync("/");

        var fila = Assert.Single(Leer(opciones));
        Assert.False(fila.EsExitoso);
        Assert.Contains("desactivada", fila.Descripcion);
    }

    [Fact]
    public async Task Login_SiFallaElRegistro_ElInicioDeSesionContinuaNormalmente()
    {
        var opcionesRotas = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=127.0.0.1,1;Database=NoExiste;User Id=x;Password=x;Connect Timeout=1;Encrypt=False")
            .Options;
        var rota = new ServicioBitacora(opcionesRotas, new AccesorHttpContextFijo(), NullLogger<ServicioBitacora>.Instance);
        var (pagina, _, _, _) = await CrearLoginAsync("EMP705", "Prueba123", bitacora: rota);

        var resultado = await pagina.OnPostAsync("/Home/Index");

        var redirect = Assert.IsType<LocalRedirectResult>(resultado);
        Assert.Equal("/Home/Index", redirect.Url);
    }

    [Fact]
    public async Task Logout_ConSesion_RegistraCierreDeSesionConElUsuario()
    {
        var sp = TestServices.Crear();
        var accesor = (AccesorHttpContextFijo)sp.GetRequiredService<IHttpContextAccessor>();
        var http = ContextoAutenticado("u-55", "SODA001", "Soda");
        http.RequestServices = sp;
        accesor.HttpContext = http;
        var opciones = ConDefaultUsuarioBd(sp.GetRequiredService<DbContextOptions<ApplicationDbContext>>());
        var servicio = new ServicioBitacora(opciones, accesor, NullLogger<ServicioBitacora>.Instance);
        var pagina = new LogoutModel(sp.GetRequiredService<SignInManager<IdentityUser>>(), sp.GetRequiredService<ILogger<LogoutModel>>(), servicio)
        {
            PageContext = new PageContext { HttpContext = http },
        };

        var resultado = await pagina.OnPost("/");

        Assert.IsType<LocalRedirectResult>(resultado);
        var fila = Assert.Single(Leer(opciones));
        Assert.Equal("CierreSesion", fila.TipoEvento);
        Assert.True(fila.EsExitoso);
        Assert.Equal("u-55", fila.IdUsuario);
        Assert.Equal("SODA001", fila.NombreUsuario);
        Assert.Equal("Soda", fila.Rol);
    }

    [Fact]
    public async Task Logout_SinSesion_NoRegistraNada()
    {
        var sp = TestServices.Crear();
        var accesor = (AccesorHttpContextFijo)sp.GetRequiredService<IHttpContextAccessor>();
        var opciones = ConDefaultUsuarioBd(sp.GetRequiredService<DbContextOptions<ApplicationDbContext>>());
        var servicio = new ServicioBitacora(opciones, accesor, NullLogger<ServicioBitacora>.Instance);
        var pagina = new LogoutModel(sp.GetRequiredService<SignInManager<IdentityUser>>(), sp.GetRequiredService<ILogger<LogoutModel>>(), servicio)
        {
            PageContext = new PageContext { HttpContext = new DefaultHttpContext { RequestServices = sp } },
        };

        await pagina.OnPost("/");

        Assert.Empty(Leer(opciones));
    }
}
