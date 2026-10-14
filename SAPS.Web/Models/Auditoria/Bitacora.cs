namespace SAPS.Web.Models.Auditoria;

/// <summary>
/// Registro de la bitácora de auditoría (HU-004, HU-015, RNF-002).
/// Tabla genérica: un evento nuevo es una fila nueva con otro TipoEvento, sin cambiar la estructura.
/// Es de solo lectura: nadie modifica ni elimina filas (se bloquea en la base de datos).
/// Diseño: docs/Bitacora-Diseno-HU004-HU015.md
/// Tabla: soda.tb_Bitacora
/// </summary>
public class Bitacora
{
    public int IdBitacora { get; set; }

    /// <summary>Fecha y hora del evento en UTC. La asigna la base de datos (SYSUTCDATETIME()).</summary>
    public DateTime FechaHoraUtc { get; set; }

    /// <summary>Módulo afectado: Seguridad o Catalogo (a la fecha).</summary>
    public string Modulo { get; set; } = "";

    /// <summary>Código del evento (p. ej. InicioSesion, CambioPrecio). Es el campo por el que filtra la pantalla.</summary>
    public string TipoEvento { get; set; } = "";

    /// <summary>Entidad o tabla involucrada (p. ej. tb_Precio, AspNetUsers, Sesion).</summary>
    public string Recurso { get; set; } = "";

    /// <summary>Identificador del registro involucrado, como texto (puede ser un INT o el Id de una cuenta).</summary>
    public string? IdRegistro { get; set; }

    /// <summary>true = operación exitosa; false = fallida.</summary>
    public bool EsExitoso { get; set; }

    /// <summary>Id de la cuenta que realizó la acción, si existe. Sin llave foránea: el log no depende de las cuentas.</summary>
    public string? IdUsuario { get; set; }

    /// <summary>Copia del nombre de usuario o código de colaborador al momento del evento.</summary>
    public string? NombreUsuario { get; set; }

    /// <summary>Copia del rol de la cuenta al momento del evento.</summary>
    public string? Rol { get; set; }

    /// <summary>
    /// Usuario de SQL Server que insertó la fila. Lo asigna la base de datos (SUSER_SNAME());
    /// la aplicación lo deja sin valor (null). En un cambio directo en la BD es quien lo hizo.
    /// </summary>
    public string UsuarioBd { get; set; } = null!;

    public string? DireccionIp { get; set; }
    public string? Dispositivo { get; set; }

    public string? ValorAnterior { get; set; }
    public string? ValorNuevo { get; set; }

    /// <summary>Texto legible del evento (incluye la cuenta afectada en altas, bajas y cambios de rol).</summary>
    public string? Descripcion { get; set; }
}
