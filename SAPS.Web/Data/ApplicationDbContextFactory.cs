using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SAPS.Web.Data;

// Permite preparar y aplicar migraciones sin arrancar la web ni crear usuarios de prueba.
public sealed class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var entorno = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";
        var ruta = Directory.GetCurrentDirectory();
        if (!File.Exists(Path.Combine(ruta, "appsettings.json"))) ruta = Path.Combine(ruta, "SAPS.Web");
        var builder = new ConfigurationBuilder().SetBasePath(ruta)
            .AddJsonFile("appsettings.json")
            .AddJsonFile($"appsettings.{entorno}.json", optional: true);
        if (entorno == "Development") builder.AddUserSecrets<ApplicationDbContextFactory>(optional: true);
        var configuracion = builder.AddEnvironmentVariables().AddCommandLine(args).Build();
        var conexion = configuracion.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Falta la conexión DefaultConnection.");
        // Identity debe configurarse igual que en Program para conservar el modelo existente.
        var servicios = new ServiceCollection();
        servicios.AddLogging();
        servicios.AddDbContext<ApplicationDbContext>(o => o.UseSqlServer(conexion, sql => sql.UseCompatibilityLevel(110)));
        servicios.AddDefaultIdentity<IdentityUser>().AddRoles<IdentityRole>().AddEntityFrameworkStores<ApplicationDbContext>();
        return servicios.BuildServiceProvider().GetRequiredService<ApplicationDbContext>();
    }
}
