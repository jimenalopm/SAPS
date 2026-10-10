using Microsoft.EntityFrameworkCore;
using SAPS.Web.Data;

namespace SAPS.Web.Services.Pedidos;

// FotoUrl es opcional: sin foto la pantalla muestra las iniciales del colaborador.
public record Colaborador(string Codigo, string Nombre, bool Activo, string? FotoUrl = null, string PosicionFoto = "center")
{
    /// <summary>Los colaboradores DEMO*** son ficticios; los pedidos a su nombre se marcan como prueba.</summary>
    public bool EsDemo => Codigo.StartsWith("DEMO", StringComparison.OrdinalIgnoreCase);
}

/// <summary>Indica si el entorno es Development con base local (el único donde existen colaboradores DEMO).</summary>
public sealed record EntornoPrueba(bool Activo);

public interface IColaboradores
{
    /// <summary>Busca por código ya normalizado. Devuelve también a los inactivos: decidir es tarea del servicio.</summary>
    Task<Colaborador?> BuscarAsync(string codigo, CancellationToken cancellationToken = default);
}

/// <summary>Colaboradores de rrhh.tb_Colaborador, administrados por RH desde SAPS (RNF-006).</summary>
public sealed class ColaboradoresTabla(ApplicationDbContext db) : IColaboradores
{
    public async Task<Colaborador?> BuscarAsync(string codigo, CancellationToken cancellationToken = default)
    {
        var fila = await db.Colaboradores.AsNoTracking()
            .Where(c => c.Codigo == codigo)
            .Select(c => new { c.Codigo, c.NombreCompleto, c.EstaActivo, c.RutaFoto })
            .SingleOrDefaultAsync(cancellationToken);
        return fila is null ? null : new Colaborador(fila.Codigo, fila.NombreCompleto, fila.EstaActivo, FotoSegura(fila.RutaFoto));
    }

    /// <summary>
    /// La foto se inserta en un estilo CSS del navegador: solo se aceptan rutas del propio sitio
    /// o URLs http(s) sin comillas ni caracteres de control. Cualquier otra cosa se ignora.
    /// </summary>
    public static string? FotoSegura(string? valor)
    {
        var texto = valor?.Trim();
        if (string.IsNullOrEmpty(texto) || texto.Length > 260) return null;
        if (texto.Any(c => char.IsControl(c) || c is '"' or '\'' or '\\' or '('or ')' or '<' or '>' or ' ')) return null;
        var esRutaLocal = texto.StartsWith('/') && !texto.StartsWith("//");
        var esHttp = Uri.TryCreate(texto, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https";
        return esRutaLocal || esHttp ? texto : null;
    }
}

public static class CodigoColaborador
{
    public const int Longitud = 10;

    /// <summary>
    /// "842" → "0000000842". Los códigos numéricos se completan con ceros a la izquierda hasta 10 dígitos;
    /// el resto (p. ej. "demo001") solo se recorta y pasa a mayúsculas. Un numérico de más de 10 dígitos no se toca.
    /// </summary>
    public static string Normalizar(string? codigo)
    {
        var texto = (codigo ?? "").Trim();
        if (texto.Length is > 0 and <= Longitud && texto.All(char.IsAsciiDigit)) return texto.PadLeft(Longitud, '0');
        return texto.ToUpperInvariant();
    }
}

public class PedidoInvalidoException(string message) : Exception(message);
public sealed class ColaboradorNoEncontradoException() : PedidoInvalidoException("Colaborador no encontrado. Verifique el código digitado.");
public sealed class ColaboradorInactivoException() : PedidoInvalidoException("Colaborador inactivo. No se puede registrar el pedido.");
