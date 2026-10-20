using System.Globalization;
using System.Text.RegularExpressions;

namespace SAPS.Web.Services.Auditoria;

/// <summary>
/// HU-004 / HU-015: textos legibles para la pantalla de bitácora. Solo formatea lo que ya está guardado
/// en soda.tb_Bitacora; no lee ni escribe en la base de datos.
/// Un tipo de evento, módulo o rol que no esté en los diccionarios se muestra con su código tal cual
/// (HU-004 criterio 8: un evento nuevo aparece en la pantalla sin cambiar código).
/// </summary>
public static partial class PresentacionBitacora
{
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("es-CR");

    /// <summary>Costa Rica no usa horario de verano: siempre UTC-6.</summary>
    private static readonly TimeSpan DesfaseCostaRica = TimeSpan.FromHours(-6);

    private static readonly Dictionary<string, string> Eventos = new(StringComparer.Ordinal)
    {
        [TiposEventoBitacora.InicioSesion] = "Inicio de sesión",
        [TiposEventoBitacora.CierreSesion] = "Cierre de sesión",
        [TiposEventoBitacora.CuentaCreada] = "Cuenta creada",
        [TiposEventoBitacora.CuentaDesactivada] = "Cuenta desactivada",
        [TiposEventoBitacora.CuentaReactivada] = "Cuenta reactivada",
        [TiposEventoBitacora.CuentaModificada] = "Cuenta modificada",
        [TiposEventoBitacora.RolCambiado] = "Cambio de rol",
        [TiposEventoBitacora.CambioPrecio] = "Cambio de precio",
    };

    /// <summary>
    /// Eventos que el sistema ya sabe registrar, con su módulo (docs/Bitacora-Diseno-HU004-HU015.md, sección 3).
    /// El filtro de la pantalla los ofrece siempre, aunque todavía no haya filas de ese tipo.
    /// </summary>
    public static readonly IReadOnlyList<(string Modulo, string TipoEvento)> EventosConocidos =
    [
        (ModulosBitacora.Seguridad, TiposEventoBitacora.InicioSesion),
        (ModulosBitacora.Seguridad, TiposEventoBitacora.CierreSesion),
        (ModulosBitacora.Seguridad, TiposEventoBitacora.CuentaCreada),
        (ModulosBitacora.Seguridad, TiposEventoBitacora.CuentaDesactivada),
        (ModulosBitacora.Seguridad, TiposEventoBitacora.CuentaReactivada),
        (ModulosBitacora.Seguridad, TiposEventoBitacora.CuentaModificada),
        (ModulosBitacora.Seguridad, TiposEventoBitacora.RolCambiado),
        (ModulosBitacora.Catalogo, TiposEventoBitacora.CambioPrecio),
    ];

    private static readonly Dictionary<string, string> Modulos = new(StringComparer.Ordinal)
    {
        [ModulosBitacora.Seguridad] = "Seguridad",
        [ModulosBitacora.Catalogo] = "Catálogo",
    };

    /// <summary>Tablas o entidades del log con un nombre que entienda el Administrador (el código queda en el detalle).</summary>
    private static readonly Dictionary<string, string> Recursos = new(StringComparer.Ordinal)
    {
        [RecursosBitacora.Precio] = "Precio de producto",
        [RecursosBitacora.Bebida] = "Precio de bebida",
        [RecursosBitacora.Cuentas] = "Cuentas de usuario",
        [RecursosBitacora.Sesion] = "Sesión",
    };

    private static readonly Dictionary<string, string> Roles = new(StringComparer.Ordinal)
    {
        ["Administrador"] = "Administrador",
        ["RecursosHumanos"] = "Recursos Humanos",
        ["Soda"] = "Soda",
    };

    public static string EtiquetaEvento(string? tipoEvento) =>
        string.IsNullOrEmpty(tipoEvento) ? "" : Eventos.GetValueOrDefault(tipoEvento, tipoEvento);

    public static string EtiquetaModulo(string? modulo) =>
        string.IsNullOrEmpty(modulo) ? "" : Modulos.GetValueOrDefault(modulo, modulo);

    /// <summary>Texto que se muestra en lugar de lo digitado en un inicio de sesión con un usuario que no existe.</summary>
    public const string UsuarioNoReconocido = "(usuario no reconocido)";

    /// <summary>
    /// En un inicio de sesión fallido sin cuenta (idUsuario vacío), lo digitado pudo ser la contraseña escrita en el
    /// campo equivocado. Los registros anteriores al cambio del login lo tienen guardado y no se pueden borrar,
    /// así que la pantalla nunca lo muestra.
    /// </summary>
    public static string? UsuarioVisible(string tipoEvento, bool esExitoso, string? idUsuario, string? nombreUsuario) =>
        tipoEvento == TiposEventoBitacora.InicioSesion && !esExitoso && idUsuario is null ? UsuarioNoReconocido : nombreUsuario;

    public static string EtiquetaRecurso(string? recurso) =>
        string.IsNullOrEmpty(recurso) ? "" : Recursos.GetValueOrDefault(recurso, recurso);

    public static string? EtiquetaRol(string? rol) =>
        string.IsNullOrEmpty(rol) ? null : Roles.GetValueOrDefault(rol, rol);

    /// <summary>La BD guarda la hora en UTC (SYSUTCDATETIME()); la pantalla la muestra en hora de Costa Rica.</summary>
    public static DateTime AHoraCostaRica(DateTime fechaUtc) =>
        DateTime.SpecifyKind(fechaUtc.Add(DesfaseCostaRica), DateTimeKind.Unspecified);

    /// <summary>Inverso de <see cref="AHoraCostaRica"/>: para filtrar por fechas que el usuario escribe en hora de Costa Rica.</summary>
    public static DateTime AUtc(DateTime horaCostaRica) =>
        DateTime.SpecifyKind(horaCostaRica.Subtract(DesfaseCostaRica), DateTimeKind.Unspecified);

    public static string Fecha(DateTime horaCostaRica) => horaCostaRica.ToString("dd/MM/yyyy", Cultura);

    public static string Hora(DateTime horaCostaRica) => horaCostaRica.ToString("h:mm tt", Cultura);

    public static string HoraConSegundos(DateTime horaCostaRica) => horaCostaRica.ToString("h:mm:ss tt", Cultura);

    /// <summary>Los montos llegan como texto ("2500" o "2500.00" desde el trigger). Si no es número, se deja igual.</summary>
    public static string? Colones(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor)) return null;
        return decimal.TryParse(valor, NumberStyles.Number, CultureInfo.InvariantCulture, out var monto)
            ? "₡" + monto.ToString("N0", Cultura)
            : valor;
    }

    /// <summary>Diferencia entre dos montos, p. ej. "+₡200 (+8,0 %)". Null si alguno no es un número.</summary>
    public static string? DiferenciaPrecio(string? anterior, string? nuevo)
    {
        if (!decimal.TryParse(anterior, NumberStyles.Number, CultureInfo.InvariantCulture, out var a)
            || !decimal.TryParse(nuevo, NumberStyles.Number, CultureInfo.InvariantCulture, out var n))
            return null;

        var diferencia = n - a;
        var signo = diferencia >= 0 ? "+" : "−";
        var texto = $"{signo}₡{Math.Abs(diferencia).ToString("N0", Cultura)}";
        if (a == 0) return texto;
        var porcentaje = Math.Abs(diferencia / a * 100);
        return $"{texto} ({signo}{porcentaje.ToString("N1", Cultura)} %)";
    }

    /// <summary>IP del propio servidor (::1, 127.0.0.1): la persona entró desde el mismo equipo donde corre SAPS.</summary>
    public static bool EsIpLocal(string? ip) =>
        ip is "::1" or "127.0.0.1" or "::ffff:127.0.0.1";

    /// <summary>
    /// Inspección de usabilidad HU-004 (H2): la IP sola («::1») no le dice nada al Administrador.
    /// Se describe de dónde vino la conexión; la IP exacta queda en «Datos técnicos» del detalle.
    /// </summary>
    public static string? OrigenConexion(string? ip) =>
        string.IsNullOrWhiteSpace(ip) ? null
        : EsIpLocal(ip) ? "Desde el mismo equipo del servidor"
        : $"Desde otro equipo (IP {ip})";

    /// <summary>"Mozilla/5.0 (Windows NT 10.0; ...) Chrome/141..." → "Chrome en Windows". El texto completo queda en el detalle.</summary>
    public static string? ResumirDispositivo(string? userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent)) return null;
        var ua = userAgent;

        var navegador = ua.Contains("Edg/") ? "Edge"
            : ua.Contains("OPR/") ? "Opera"
            : ua.Contains("Firefox/") ? "Firefox"
            : ua.Contains("Chrome/") ? "Chrome"
            : ua.Contains("Safari/") ? "Safari"
            : null;

        var sistema = ua.Contains("iPad") ? "iPad"
            : ua.Contains("iPhone") ? "iPhone"
            : ua.Contains("Android") ? "Android"
            : ua.Contains("Windows") ? "Windows"
            : ua.Contains("Mac OS") ? "macOS"
            : ua.Contains("Linux") ? "Linux"
            : null;

        if (navegador is null && sistema is null) return ua.Length <= 40 ? ua : ua[..40] + "…";
        if (navegador is null) return sistema;
        if (sistema is null) return navegador;
        return $"{navegador} en {sistema}";
    }

    /// <summary>
    /// Cuenta afectada en altas, bajas y cambios de rol. UsuariosController guarda la descripción como
    /// "Cuenta correo@dominio creada con rol ...", "Cuenta correo@dominio: rol A -> B" o "Cuenta correo@dominio desactivada".
    /// </summary>
    public static string? CuentaAfectada(string? descripcion)
    {
        if (string.IsNullOrEmpty(descripcion)) return null;
        var m = RegexCuenta().Match(descripcion);
        return m.Success ? m.Groups[1].Value : null;
    }

    /// <summary>
    /// Producto o bebida de un cambio de precio. El trigger guarda "Producto: X | Tamaño: Y" o "Bebida: X | Tamaño: Y";
    /// aquí queda "X · Y" (o solo "X" si el tamaño es único).
    /// </summary>
    public static string? ProductoDelPrecio(string? descripcion)
    {
        if (string.IsNullOrEmpty(descripcion)) return null;
        var texto = RegexPrefijoProducto().Replace(descripcion, "");
        var partes = texto.Split(" | Tamaño: ", 2, StringSplitOptions.TrimEntries);
        if (partes.Length == 1) return partes[0];
        return partes[1] is "Único" or "Sin tamaño" ? partes[0] : $"{partes[0]} · {partes[1]}";
    }

    /// <summary>
    /// Solo el nombre del producto o la bebida, sin el tamaño ("Producto: Casado | Tamaño: Grande" → "Casado").
    /// Sirve para buscar todos los cambios de precio de ese artículo, en cualquier tamaño.
    /// </summary>
    public static string? NombreArticuloDelPrecio(string? descripcion)
    {
        if (string.IsNullOrEmpty(descripcion) || !RegexPrefijoProducto().IsMatch(descripcion)) return null;
        var texto = RegexPrefijoProducto().Replace(descripcion, "");
        var nombre = texto.Split(" | Tamaño: ", 2, StringSplitOptions.TrimEntries)[0];
        return nombre.Length == 0 ? null : nombre;
    }

    [GeneratedRegex(@"^Cuenta ([^\s:]+)")]
    private static partial Regex RegexCuenta();

    [GeneratedRegex(@"^(Producto|Bebida): ")]
    private static partial Regex RegexPrefijoProducto();
}
