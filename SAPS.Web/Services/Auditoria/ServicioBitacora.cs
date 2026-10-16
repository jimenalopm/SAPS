using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using SAPS.Web.Data;
using SAPS.Web.Models.Auditoria;

namespace SAPS.Web.Services.Auditoria;

/// <summary>
/// Escribe en soda.tb_Bitacora con un contexto PROPIO (no el del request): así guardar el log no
/// guarda los cambios pendientes del llamador, una falla no deja filas colgadas en su ChangeTracker
/// y el log de un intento fallido persiste aunque la transacción del llamador se revierta.
/// </summary>
public sealed class ServicioBitacora(
    DbContextOptions<ApplicationDbContext> opciones,
    IHttpContextAccessor accesor,
    ILogger<ServicioBitacora> logger) : IServicioBitacora
{
    // Largos de las columnas (BitacoraConfiguration). Un texto largo se recorta; nunca debe tumbar el log.
    private const int MaxModulo = 30, MaxTipoEvento = 50, MaxRecurso = 100, MaxIdRegistro = 50,
        MaxIdUsuario = 450, MaxNombreUsuario = 256, MaxRol = 50, MaxIp = 45, MaxDispositivo = 200,
        MaxValor = 500, MaxDescripcion = 300;

    public async Task<bool> RegistrarAsync(EventoBitacora evento, CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(evento.Modulo)
                || string.IsNullOrWhiteSpace(evento.TipoEvento)
                || string.IsNullOrWhiteSpace(evento.Recurso))
            {
                logger.LogError("Bitácora: evento sin módulo, tipo de evento o recurso; no se registró.");
                return false;
            }

            var http = accesor.HttpContext;
            var usuario = http?.User;
            var autenticado = usuario?.Identity?.IsAuthenticated == true;

            var fila = new Bitacora
            {
                Modulo = Recortar(evento.Modulo, MaxModulo)!,
                TipoEvento = Recortar(evento.TipoEvento, MaxTipoEvento)!,
                Recurso = Recortar(evento.Recurso, MaxRecurso)!,
                IdRegistro = Recortar(evento.IdRegistro, MaxIdRegistro),
                EsExitoso = evento.EsExitoso,
                IdUsuario = Recortar(evento.IdUsuario ?? (autenticado ? usuario!.FindFirstValue(ClaimTypes.NameIdentifier) : null), MaxIdUsuario),
                NombreUsuario = Recortar(evento.NombreUsuario ?? (autenticado ? usuario!.Identity!.Name : null), MaxNombreUsuario),
                Rol = Recortar(evento.Rol ?? (autenticado ? usuario!.FindFirstValue(ClaimTypes.Role) : null), MaxRol),
                DireccionIp = Recortar(evento.DireccionIp ?? http?.Connection.RemoteIpAddress?.ToString(), MaxIp),
                Dispositivo = Recortar(evento.Dispositivo ?? http?.Request.Headers.UserAgent.ToString(), MaxDispositivo),
                ValorAnterior = Recortar(evento.ValorAnterior, MaxValor),
                ValorNuevo = Recortar(evento.ValorNuevo, MaxValor),
                Descripcion = Recortar(evento.Descripcion, MaxDescripcion),
            };

            await using var db = new ApplicationDbContext(opciones);
            db.Bitacoras.Add(fila);
            await db.SaveChangesAsync(ct);
            return true;
        }
        catch (Exception ex)
        {
            // El log nunca rompe la operación que lo originó. Se deja constancia técnica sin datos del evento.
            logger.LogError(ex, "Bitácora: no se pudo registrar el evento {TipoEvento}.", evento.TipoEvento);
            return false;
        }
    }

    // Vacío se guarda como null (excepto en los obligatorios, ya validados arriba).
    private static string? Recortar(string? valor, int max)
    {
        if (string.IsNullOrEmpty(valor)) return null;
        return valor.Length <= max ? valor : valor[..max];
    }
}
