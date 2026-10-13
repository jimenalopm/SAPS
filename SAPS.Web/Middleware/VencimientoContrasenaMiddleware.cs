using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SAPS.Web.Data;
using SAPS.Web.Models.Identity;

namespace SAPS.Web.Middleware;

/// <summary>
/// HU-001: bloquea el acceso a toda la app, antes de dejar pasar cualquier otra página, en tres
/// casos: (1) RNF-006, la cuenta está vinculada a un colaborador de RRHH inactivo; (2) la cuenta
/// fue desactivada por un Administrador o está bloqueada transitoriamente (RNF-001); (3) criterio 7,
/// la contraseña venció a los 10 meses, o criterio de primer ingreso, es la temporal que dio el
/// Administrador. Los casos (1) y (2) se revisan en TODA ruta autenticada, incluida CambiarContrasena
/// (alguien a mitad de cambiar su contraseña también pierde el acceso si lo desactivan en el momento);
/// el caso (3) no se revisa en CambiarContrasena, para no generar un bucle de redirección.
/// </summary>
public sealed class VencimientoContrasenaMiddleware(RequestDelegate siguiente)
{
    // Páginas siempre alcanzables, sin ningún chequeo de esta clase.
    private static readonly string[] RutasSiempreExentas =
    [
        "/Identity/Account/Login",
        "/Identity/Account/Logout",
        "/Identity/Account/Lockout",
        "/Identity/Account/AccessDenied",
    ];

    private const string RutaCambiarContrasena = "/Identity/Account/CambiarContrasena";

    // Solo recursos estáticos reales. No basta con buscar un punto en la ruta, porque una página
    // con un id como "/Pedidos/Detalle/juan.perez" también lo tendría y se saltaría el bloqueo.
    private static readonly HashSet<string> ExtensionesEstaticas = new(StringComparer.OrdinalIgnoreCase)
    {
        ".css", ".js", ".map", ".png", ".jpg", ".jpeg", ".gif", ".svg", ".webp", ".ico",
        ".woff", ".woff2", ".ttf", ".eot",
    };

    public async Task InvokeAsync(HttpContext context, ApplicationDbContext db, TimeProvider reloj)
    {
        if (context.User.Identity?.IsAuthenticated != true
            || EsRutaSiempreExenta(context.Request.Path)
            || EsArchivoEstatico(context.Request.Path))
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

        var ahora = reloj.GetUtcNow();

        // ---------- Acceso revocado: RNF-006 (colaborador inactivo) o cuenta desactivada/bloqueada ----------
        var cuenta = await db.Users.AsNoTracking()
            .Where(u => u.Id == idUsuario)
            .Select(u => new
            {
                u.LockoutEnabled,
                u.LockoutEnd,
                ColaboradorActivo = db.VinculosUsuarioColaborador
                    .Where(v => v.IdUsuario == u.Id)
                    .Join(db.Colaboradores, v => v.IdColaborador, c => c.IdColaborador, (v, c) => (bool?)c.EstaActivo)
                    .FirstOrDefault(),
            })
            .FirstOrDefaultAsync();

        var accesoRevocado = cuenta is { LockoutEnabled: true, LockoutEnd: { } finBloqueo } && finBloqueo > ahora;
        var colaboradorInactivo = cuenta?.ColaboradorActivo == false;
        if (accesoRevocado || colaboradorInactivo)
        {
            await context.SignOutAsync(IdentityConstants.ApplicationScheme);
            // Desactivada por el administrador (LockoutEnd = MaxValue) y bloqueo transitorio por intentos
            // fallidos son motivos distintos: cada uno tiene su mensaje en el login.
            var desactivadaPorAdmin = cuenta?.LockoutEnd == DateTimeOffset.MaxValue;
            context.Response.Redirect(!accesoRevocado
                ? "/Identity/Account/Login?colaboradorInactivo=true"
                : desactivadaPorAdmin
                    ? "/Identity/Account/Login?accesoRevocado=true"
                    : "/Identity/Account/Login?bloqueoTemporal=true");
            return;
        }

        // CambiarContrasena solo está exenta de lo que sigue (vencimiento / primer ingreso).
        if (context.Request.Path.StartsWithSegments(RutaCambiarContrasena, StringComparison.OrdinalIgnoreCase))
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

        if (estado.RequiereCambioInicial)
        {
            // Primer ingreso: se exige el cambio sin importar si ya "venció" por fecha
            // (la fila se crea con FechaUltimoCambio = ahora, así que nunca estaría vencida).
            context.Response.Redirect("/Identity/Account/CambiarContrasena?obligatorio=true&motivo=primerIngreso");
            return;
        }

        var vencida = estado.FechaUltimoCambio.AddMonths(IdentityConfig.MesesVigenciaContrasena) <= ahora.UtcDateTime;
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

    private static bool EsRutaSiempreExenta(PathString ruta) =>
        RutasSiempreExentas.Any(r => ruta.StartsWithSegments(r, StringComparison.OrdinalIgnoreCase));

    private static bool EsArchivoEstatico(PathString ruta) =>
        ruta.HasValue && ExtensionesEstaticas.Contains(Path.GetExtension(ruta.Value!));
}
