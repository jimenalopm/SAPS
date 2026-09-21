namespace SAPS.Web.Models.Pedidos;

public class Pedido
{
    public int IdPedido { get; set; }
    public Guid TokenRegistro { get; set; }
    public string CodigoColaborador { get; set; } = "";
    public string NombreColaborador { get; set; } = "";
    public string IdUsuarioRegistro { get; set; } = "";
    public DateTime FechaRegistroUtc { get; set; }
    public string TipoComida { get; set; } = "";
    public string? Observaciones { get; set; }
    public long Total { get; set; }
    public bool EsPrueba { get; set; }
    public List<DetallePedido> Detalles { get; set; } = [];
}

public class DetallePedido
{
    public int IdDetallePedido { get; set; }
    public int IdPedido { get; set; }
    public Pedido Pedido { get; set; } = null!;
    public int? IdPrecio { get; set; }
    public int? IdBebida { get; set; }
    public string NombreArticulo { get; set; } = "";
    public string? NombreTamano { get; set; }
    public int Cantidad { get; set; }
    public int PrecioUnitario { get; set; }
    public long Subtotal { get; set; }
}
