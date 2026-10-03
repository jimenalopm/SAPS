// Data/SembradoPrueba.cs
using Microsoft.Data.SqlClient;

namespace SAPS.Web.Data;

/// <summary>
/// Protección contra el sembrado de datos de prueba (usuarios EMP001/SODA001/ADM001,
/// colaboradores DEMO***) en una base real. Solo se siembra si el entorno es Development
/// Y el servidor de la cadena de conexión es local. Ante cualquier duda se asume base real.
/// </summary>
public static class SembradoPrueba
{
    private static readonly string[] ServidoresLocales = ["localhost", "127.0.0.1", "::1", ".", "(local)", "(localdb)"];

    public static bool Permitido(IHostEnvironment entorno, string? cadenaConexion) =>
        entorno.IsDevelopment() && EsServidorLocal(cadenaConexion);

    public static bool EsServidorLocal(string? cadenaConexion)
    {
        if (string.IsNullOrWhiteSpace(cadenaConexion)) return false;
        string servidor;
        try { servidor = new SqlConnectionStringBuilder(cadenaConexion).DataSource; }
        catch (ArgumentException) { return false; }   // cadena ilegible: se trata como base real

        servidor = servidor.Trim().ToLowerInvariant();
        foreach (var prefijo in new[] { "tcp:", "np:", "lpc:" })
            if (servidor.StartsWith(prefijo)) servidor = servidor[prefijo.Length..];
        var coma = servidor.IndexOf(',');                 // puerto: localhost,1433
        if (coma >= 0) servidor = servidor[..coma];
        var barra = servidor.IndexOf('\\');               // instancia: .\SQLEXPRESS, (localdb)\MSSQLLocalDB
        if (barra >= 0) servidor = servidor[..barra];
        return ServidoresLocales.Contains(servidor.Trim());
    }
}
