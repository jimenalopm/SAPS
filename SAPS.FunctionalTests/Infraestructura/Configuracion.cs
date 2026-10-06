namespace SAPS.FunctionalTests.Infraestructura;

/// <summary>Los tres roles reales del sistema (tal como los nombra la app).</summary>
public enum Rol
{
    Administrador,
    RecursosHumanos,
    Soda
}

/// <summary>
/// Lee TODA la configuración desde variables de entorno. Ninguna credencial ni correo real
/// vive en el código ni en archivos del repositorio (el repo es público).
/// </summary>
public static class Configuracion
{
    /// <summary>URL base de la app. Por defecto, la que usa <c>dotnet run</c> (perfil "http").</summary>
    public static string UrlBase =>
        (Environment.GetEnvironmentVariable("SAPS_URL") is { Length: > 0 } url ? url : "http://localhost:5120").TrimEnd('/');

    /// <summary>SAPS_HEADLESS=1 corre Chrome sin ventana (útil en servidores; por defecto se ve el navegador).</summary>
    public static bool SinVentana => Environment.GetEnvironmentVariable("SAPS_HEADLESS") == "1";

    /// <summary>Lee una variable obligatoria; si falta, la prueba falla diciendo exactamente cuál.</summary>
    public static string Requerida(string nombre)
    {
        var valor = Environment.GetEnvironmentVariable(nombre);
        if (string.IsNullOrWhiteSpace(valor))
            throw new InvalidOperationException(
                $"Falta la variable de entorno {nombre}. Configúrela en su PowerShell antes de correr las pruebas " +
                $"(por ejemplo: $env:{nombre} = \"...\"). Ver README.md de SAPS.FunctionalTests.");
        return valor;
    }

    /// <summary>Correo y contraseña del rol, leídos de SAPS_{ADMIN|RH|SODA}_{CORREO|CLAVE}.</summary>
    public static (string Correo, string Clave) Credenciales(Rol rol)
    {
        var prefijo = rol switch
        {
            Rol.Administrador => "SAPS_ADMIN",
            Rol.RecursosHumanos => "SAPS_RH",
            Rol.Soda => "SAPS_SODA",
            _ => throw new ArgumentOutOfRangeException(nameof(rol))
        };
        return (Requerida(prefijo + "_CORREO"), Requerida(prefijo + "_CLAVE"));
    }

    /// <summary>Código de un colaborador REAL y ACTIVO. Solo se usa para consultar, nunca para registrar pedidos.</summary>
    public static string ColaboradorActivo => Requerida("SAPS_COLAB_ACTIVO");

    /// <summary>Código de un colaborador REAL e INACTIVO (solo consulta). Opcional: si falta, la prueba correspondiente se omite (nunca se marca a un colaborador real como inactivo).</summary>
    public static string? ColaboradorInactivo =>
        Environment.GetEnvironmentVariable("SAPS_COLAB_INACTIVO") is { Length: > 0 } c ? c : null;
}

/// <summary>Datos fijos de las pruebas. Todo lo que las pruebas escriben en la BD usa estos nombres.</summary>
public static class DatosDePrueba
{
    /// <summary>Único colaborador contra el que se registran pedidos (ficticio, de prueba).</summary>
    public const string ColaboradorDemo = "DEMO001";

    /// <summary>Producto real del catálogo que se usa para armar pedidos (HU-008). Solo se lee.</summary>
    public const string ProductoReal = "Sopa";

    /// <summary>Prefijo de TODO producto/bebida creado por las pruebas; permite limpiarlos con SQL (Sql/limpieza_pruebas.sql).</summary>
    public const string PrefijoProductoPrueba = "ZZ_SELENIUM_";

    /// <summary>Correo que NO existe: se usa para probar credenciales inválidas sin tocar cuentas reales.</summary>
    public const string CorreoInexistente = "noexiste@recyplast.cr";

    /// <summary>Nombre único por ejecución, p. ej. ZZ_SELENIUM_20261006_153045_a1b2.</summary>
    public static string NombreProductoPrueba() =>
        PrefijoProductoPrueba + DateTime.Now.ToString("yyyyMMdd_HHmmss") + "_" + Guid.NewGuid().ToString("N")[..4];
}
