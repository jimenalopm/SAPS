using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using SAPS.Web.Data;

namespace SAPS.Tests.Helpers;

/// <summary>
/// Construye un contenedor de servicios con Identity real sobre EF Core InMemory,
/// usando la misma configuración de la aplicación (IdentityConfig.Configurar).
/// Cada llamada crea una base de datos aislada.
/// </summary>
public static class TestServices
{
    public static ServiceProvider Crear()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var accessor = new AccesorHttpContextFijo();
        services.AddSingleton<IHttpContextAccessor>(accessor);

        var dbName = Guid.NewGuid().ToString();
        services.AddDbContext<ApplicationDbContext>(o => o.UseInMemoryDatabase(dbName));

        services.AddIdentity<IdentityUser, IdentityRole>(IdentityConfig.Configurar)
                .AddEntityFrameworkStores<ApplicationDbContext>()
                .AddDefaultTokenProviders();

        services.AddScoped<DbInitializer>();

        var provider = services.BuildServiceProvider();

        // SignInManager necesita un HttpContext para emitir la cookie de sesión.
        accessor.HttpContext = new DefaultHttpContext { RequestServices = provider };

        return provider;
    }

    public static ApplicationDbContext CrearContextoInMemory()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            // InMemory no soporta transacciones; los controladores que las abren siguen funcionando.
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new ApplicationDbContext(options);
    }

    /// <summary>
    /// Base SQLite en memoria (relacional): soporta transacciones con nivel de aislamiento,
    /// claves foráneas y check constraints. La base vive mientras la conexión esté abierta.
    /// </summary>
    public static ApplicationDbContext CrearContextoSqlite(SqliteConnection conexion)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(conexion)
            .Options;
        var ctx = new ApplicationDbContext(options);
        ctx.Database.EnsureCreated();
        return ctx;
    }

    public static SqliteConnection AbrirConexionSqlite()
    {
        var conexion = new SqliteConnection("DataSource=:memory:");
        conexion.Open();
        return conexion;
    }

    /// <summary>Prepara un controlador MVC con HttpContext y TempData para invocar sus acciones.</summary>
    public static T ConContexto<T>(T controlador, string metodoHttp = "GET") where T : Controller
    {
        var http = new DefaultHttpContext();
        http.Request.Method = metodoHttp;
        controlador.ControllerContext = new ControllerContext { HttpContext = http };
        controlador.TempData = new TempDataDictionary(http, new TempDataEnMemoria());
        return controlador;
    }

    /// <summary>
    /// Contexto con el proveedor SQL Server (sin abrir conexión) para inspeccionar
    /// el modelo tal como se genera para producción: tablas, índices, check constraints.
    /// </summary>
    public static ApplicationDbContext CrearContextoSqlServerSinConexion()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=.;Database=SAPS_Tests_NoConexion;Trusted_Connection=True;")
            .Options;
        return new ApplicationDbContext(options);
    }
}

/// <summary>
/// HttpContextAccessor sin AsyncLocal: el contexto asignado se ve desde cualquier
/// método async de la prueba.
/// </summary>
internal sealed class AccesorHttpContextFijo : IHttpContextAccessor
{
    public HttpContext? HttpContext { get; set; }
}

internal sealed class TempDataEnMemoria : ITempDataProvider
{
    private IDictionary<string, object> _datos = new Dictionary<string, object>();
    public IDictionary<string, object> LoadTempData(HttpContext context) => _datos;
    public void SaveTempData(HttpContext context, IDictionary<string, object> values) => _datos = values;
}

/// <summary>Reloj fijo para que las pruebas de vigencia de precios sean deterministas.</summary>
public sealed class RelojFijo(DateTimeOffset ahoraUtc) : TimeProvider
{
    public DateTimeOffset AhoraUtc { get; set; } = ahoraUtc;
    public override DateTimeOffset GetUtcNow() => AhoraUtc;
}
