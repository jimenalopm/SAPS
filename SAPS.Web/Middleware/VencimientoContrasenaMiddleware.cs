using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using SAPS.Web.Data;
using SAPS.Web.Models.Identity;

namespace SAPS.Web.Middleware;

/// <summary>
/// HU-001, criterio 7: si pasaron más de <see cref="IdentityConfig.MesesVigenciaContrasena"/> meses
/// desde el último cambio de contraseña, redirige a CambiarContrasena en modo obligatorio antes de
/// dejar pasar cualquier otra página. Es el único punto de control de este criterio (no toca Login).
/// </summary>
public sealed class VencimientoContrasenaMiddleware(RequestDelegate siguiente)
{
    // Páginas que deben seguir siendo alcanzables aunque la contraseña esté vencida,
    // para no caer en un bucle de redirección.
    private static readonly string[] RutasExentas =
    [
        "/Identity/Account/CambiarContrasena",
        "/Identity/Account/Logout",
        "/Identity/Account/Login",
        "/Identity/Account/Lockout",
        "/Identity/Account/AccessDenied",
    ];

    // Solo recursos estáticos reales. No basta con buscar un punto en la ruta, porque una página
    // con un id como "/Pedidos/Detalle/juan.perez" también lo tendría y se saltaría el bloqueo.
    private static readonly HashSet<string> ExtensionesEstaticas = new(StringComparer.OrdinalIgnoreCase)
    {
        ".css", ".js", ".map", ".png", ".jpg", ".jpeg", ".gif", ".svg", ".webp", ".ico",
        ".woff", ".woff2", ".ttf", ".eot",
    };

    public async Task InvokeAsync(HttpContext context, ApplicationDbContext db, TimeProvider reloj)
    {
        if (context.User.Identity?.IsAuthenticated != true || EsRutaExenta(context.Request.Path) || EsArchivoEstatico(context.Request.Path))
        {
            await siguiente(context);
            return;
        }

        var idUsuario = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (idUsuario is null)
        {
            await siguiente(context);
            return;
        }

        var estado = await db.EstadosContrasena.AsNoTracking().FirstOrDefaultAsync(e => e.IdUsuario == idUsuario);
        if (estado is null)
        {
            // Cuenta sin fila (nueva, o creada antes de este cambio): arranca el reloj de
            // vigencia desde su primer uso en vez de forzar un cambio sorpresivo ahora mismo.
            await CrearFilaInicialAsync(db, idUsuario, reloj);
            await siguiente(context);
            return;
        }

        var vencida = estado.FechaUltimoCambio.AddMonths(IdentityConfig.MesesVigenciaContrasena) <= reloj.GetUtcNow().UtcDateTime;
        if (vencida)
        {
            context.Response.Redirect("/Identity/Account/CambiarContrasena?obligatorio=true");
            return;
        }

        await siguiente(context);
    }

    private static async Task CrearFilaInicialAsync(ApplicationDbContext db, string idUsuario, TimeProvider reloj)
    {
        db.EstadosContrasena.Add(new EstadoContrasenaUsuario { IdUsuario = idUsuario, FechaUltimoCambio = reloj.GetUtcNow().UtcDateTime });
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Otra solicitud concurrente ya creó la fila primero. Se descarta el intento fallido
            // para que no se reintente en un SaveChanges posterior de esta misma solicitud.
            db.ChangeTracker.Clear();
        }
    }

    private static bool EsRutaExenta(PathString ruta) =>
        RutasExentas.Any(r => ruta.StartsWithSegments(r, StringComparison.OrdinalIgnoreCase));

    private static bool EsArchivoEstatico(PathString ruta) =>
        ruta.HasValue && ExtensionesEstaticas.Contains(Path.GetExtension(ruta.Value!));
}
