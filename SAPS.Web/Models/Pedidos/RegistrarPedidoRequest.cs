using System.ComponentModel.DataAnnotations;

namespace SAPS.Web.Models.Pedidos;

public class RegistrarPedidoRequest
{
    public Guid TokenRegistro { get; set; }
    [Required, StringLength(50)] public string CodigoColaborador { get; set; } = "";
    [Required] public string TipoComida { get; set; } = "";
    [StringLength(500)] public string? Observaciones { get; set; }
    [Required, MinLength(1)] public List<LineaPedidoRequest> Lineas { get; set; } = [];
}

public class LineaPedidoRequest
{
    [Required] public string Clave { get; set; } = "";
    [Range(1, int.MaxValue)] public int Cantidad { get; set; }
    // Solo detecta cambios desde que se mostró el catálogo. Nunca determina el cobro.
    [Range(1, int.MaxValue)] public int PrecioMostrado { get; set; }
}

public record ArticuloPedido(string Clave, string Nombre, string Categoria, string? Tamano, int Precio);
public record PedidoRegistrado(int IdPedido, long Total);
