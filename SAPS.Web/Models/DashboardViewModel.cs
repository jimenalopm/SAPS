namespace SAPS.Web.Models;

/// <summary>Indicadores del Dashboard. Todos se calculan con consultas de solo lectura a la BD.</summary>
public class DashboardViewModel
{
    public int ProductosActivos { get; set; }
    public int ProductosTotal { get; set; }
    public int ProductosEspeciales { get; set; }
    public int BebidasActivas { get; set; }
    public int BebidasTotal { get; set; }
    public int CategoriasActivas { get; set; }
    public int CategoriasTotal { get; set; }
    public int TamanosActivos { get; set; }
    public int TamanosTotal { get; set; }
    public int PreciosActivos { get; set; }
    public int PedidosTotal { get; set; }

    /// <summary>Hasta 5 pedidos, del más reciente al más antiguo.</summary>
    public List<PedidoRecienteItem> PedidosRecientes { get; set; } = [];

    // Paneles que se abren al tocar cada tarjeta del inicio (solo lo activo, en forma resumida).
    public List<ResumenProducto> ListaProductos { get; set; } = [];
    public List<ResumenBebida> ListaBebidas { get; set; } = [];
    public List<ResumenCategoria> ListaCategorias { get; set; } = [];
    public List<ResumenTamano> ListaTamanos { get; set; } = [];
    public List<ResumenPrecio> ListaPrecios { get; set; } = [];

    /// <summary>Pedidos de hoy (hora de Costa Rica), del más reciente al más antiguo.</summary>
    public List<PedidoRecienteItem> PedidosHoy { get; set; } = [];
    public long TotalHoy { get; set; }
}

public sealed record ResumenProducto(string Nombre, string Categoria, bool EsEspecial, List<ResumenPrecio> Precios);
public sealed record ResumenBebida(string Nombre, string Tipo, string? Tamano, int Precio);
public sealed record ResumenCategoria(string Nombre, int Productos);
public sealed record ResumenTamano(string Nombre, bool EsParaBebida, int Usos);
public sealed record ResumenPrecio(string Producto, string? Tamano, int Monto);

public class PedidoRecienteItem
{
    public int IdPedido { get; set; }
    public string NombreColaborador { get; set; } = "";
    public string CodigoColaborador { get; set; } = "";
    public string TipoComida { get; set; } = "";
    public long Total { get; set; }
    public DateTime FechaRegistroUtc { get; set; }
    public bool EsPrueba { get; set; }
}
