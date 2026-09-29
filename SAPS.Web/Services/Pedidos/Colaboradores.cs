namespace SAPS.Web.Services.Pedidos;

public record Colaborador(string Codigo, string Nombre, bool Activo, string FotoUrl, string PosicionFoto);

// Sustituir esta interfaz por la consulta corporativa cuando la empresa habilite acceso.
public interface IColaboradores
{
    bool EsPrueba { get; }
    Task<Colaborador?> BuscarAsync(string codigo, CancellationToken cancellationToken = default);
}

public sealed class ColaboradoresDePrueba : IColaboradores
{
    public bool EsPrueba => true;
    private static readonly Colaborador[] Datos =
    [
        new("DEMO001", "Ana Solís (prueba)", true, "/images/demo/colaboradores.png", "left"),
        new("DEMO002", "Luis Mora (prueba)", true, "/images/demo/colaboradores.png", "right"),
        new("DEMO003", "Colaborador inactivo (prueba)", false, "/images/demo/colaboradores.png", "right")
    ];
    public Task<Colaborador?> BuscarAsync(string codigo, CancellationToken cancellationToken = default) =>
        Task.FromResult(Datos.SingleOrDefault(c => c.Codigo.Equals(codigo.Trim(), StringComparison.OrdinalIgnoreCase)));
}

public sealed class ColaboradoresSinConexion : IColaboradores
{
    public bool EsPrueba => false;
    public Task<Colaborador?> BuscarAsync(string codigo, CancellationToken cancellationToken = default) =>
        throw new PedidoInvalidoException("La consulta de colaboradores todavía no está configurada. Contacte al administrador.");
}

public sealed class PedidoInvalidoException(string message) : Exception(message);
