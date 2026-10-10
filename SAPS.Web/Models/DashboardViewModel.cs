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
}

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
