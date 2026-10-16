namespace SAPS.Web.Services.Auditoria;

/// <summary>
/// HU-004: único camino de escritura de la aplicación hacia soda.tb_Bitacora.
/// Un tipo de evento nuevo es solo otro valor de texto: no requiere cambios en la tabla.
/// </summary>
public interface IServicioBitacora
{
    /// <summary>
    /// Registra un evento. Devuelve false si no se pudo escribir; nunca lanza, para que un
    /// fallo del log no rompa la operación que lo originó (p. ej. el inicio de sesión).
    /// </summary>
    Task<bool> RegistrarAsync(EventoBitacora evento, CancellationToken ct = default);
}

/// <summary>
/// Datos de un evento de bitácora. La fecha y hora y el usuario de SQL Server los asigna la base de datos.
/// El usuario y el rol salen del HttpContext salvo que se indiquen (inicio de sesión: la sesión aún no existe).
/// Nunca debe incluir contraseñas ni datos de autenticación.
/// </summary>
public sealed record EventoBitacora(string Modulo, string TipoEvento, string Recurso, bool EsExitoso)
{
    /// <summary>Id del registro afectado (cuenta, precio, etc.), como texto.</summary>
    public string? IdRegistro { get; init; }

    public string? ValorAnterior { get; init; }
    public string? ValorNuevo { get; init; }

    /// <summary>Texto legible del evento (p. ej. la cuenta afectada en altas, bajas y cambios de rol).</summary>
    public string? Descripcion { get; init; }

    /// <summary>Por defecto sale de la conexión del request.</summary>
    public string? DireccionIp { get; init; }

    /// <summary>Por defecto sale del User-Agent del request.</summary>
    public string? Dispositivo { get; init; }

    /// <summary>Solo cuando el HttpContext aún no tiene al actor (inicio de sesión) o no existe (intento fallido).</summary>
    public string? IdUsuario { get; init; }
    public string? NombreUsuario { get; init; }
    public string? Rol { get; init; }
}
