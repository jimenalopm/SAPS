using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
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
            .Options;
        return new ApplicationDbContext(options);
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
