using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace SAPS.Web.Services.Auditoria;

/// <summary>
/// Le dice a los triggers de precios (soda.tr_AuditoriaPrecio y soda.tr_AuditoriaBebida) quién hace el cambio
/// (HU-015, criterio 1). El trigger es el único que escribe en la bitácora; la aplicación solo deja
/// el usuario en CONTEXT_INFO con el formato "idUsuario|nombreUsuario|rol".
/// Contrato: docs/Bitacora-Diseno-HU004-HU015.md, sección 8.
/// </summary>
public interface IContextoAuditoria
{
    /// <summary>
    /// Deja el usuario en CONTEXT_INFO de la conexión actual. Se llama dentro de la transacción,
    /// antes de los SaveChanges que modifican el precio, para que la conexión sea la misma.
    /// </summary>
    Task EstablecerAsync(DatabaseFacade baseDatos, ClaimsPrincipal usuario, CancellationToken cancellationToken = default);

    /// <summary>Limpia CONTEXT_INFO: las conexiones se reutilizan y el usuario no debe quedar para otra petición.</summary>
    Task LimpiarAsync(DatabaseFacade baseDatos, CancellationToken cancellationToken = default);
}

public sealed class ContextoAuditoria : IContextoAuditoria
{
    /// <summary>CONTEXT_INFO guarda como máximo 128 bytes.</summary>
    public const int MaximoBytes = 128;

    public async Task EstablecerAsync(DatabaseFacade baseDatos, ClaimsPrincipal usuario, CancellationToken cancellationToken = default)
    {
        // Solo SQL Server tiene CONTEXT_INFO (y los triggers); las pruebas con otros proveedores no hacen nada.
        if (!baseDatos.IsSqlServer()) return;

        var texto = Formatear(usuario);
        if (texto is null) return;

        await baseDatos.ExecuteSqlRawAsync(
            "DECLARE @c varbinary(128) = CAST(CAST({0} AS varchar(128)) AS varbinary(128)); SET CONTEXT_INFO @c;",
            [texto], cancellationToken);
    }

    public async Task LimpiarAsync(DatabaseFacade baseDatos, CancellationToken cancellationToken = default)
    {
        if (!baseDatos.IsSqlServer()) return;

        try
        {
            await baseDatos.ExecuteSqlRawAsync("SET CONTEXT_INFO 0x00;", cancellationToken);
        }
        catch
        {
            // Se llama desde un finally: si la conexión ya falló, no se oculta el error original.
        }
    }

    /// <summary>Toma id, nombre y rol de la cuenta autenticada. Null si no hay un usuario identificable.</summary>
    public static string? Formatear(ClaimsPrincipal? usuario)
    {
        if (usuario?.Identity?.IsAuthenticated != true) return null;
        var id = usuario.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(id)) return null;
        return Formatear(id, usuario.Identity.Name ?? "", usuario.FindFirstValue(ClaimTypes.Role) ?? "");
    }

    /// <summary>
    /// Arma "idUsuario|nombreUsuario|rol" en ASCII y en 128 bytes como máximo. El separador "|" se quita de los
    /// valores porque el trigger parte el texto por él; si no cabe, se recorta el nombre y se conservan id y rol.
    /// </summary>
    public static string Formatear(string idUsuario, string nombreUsuario, string rol)
    {
        var id = Limpiar(idUsuario);
        var rolLimpio = Limpiar(rol);
        var espacioNombre = Math.Max(0, MaximoBytes - id.Length - rolLimpio.Length - 2);
        var nombre = Limpiar(nombreUsuario);
        if (nombre.Length > espacioNombre) nombre = nombre[..espacioNombre];
        var texto = $"{id}|{nombre}|{rolLimpio}";
        return texto.Length > MaximoBytes ? texto[..MaximoBytes] : texto;
    }

    // CAST(... AS varchar) en la base cambia los caracteres no ASCII por "?"; se hace igual aquí para que
    // el cálculo de bytes sea exacto.
    private static string Limpiar(string valor)
    {
        var sb = new StringBuilder(valor.Length);
        foreach (var c in valor.Trim())
            sb.Append(c == '|' ? '/' : c is < ' ' or > '~' ? '?' : c);
        return sb.ToString();
    }
}
