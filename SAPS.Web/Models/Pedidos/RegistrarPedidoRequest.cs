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
    [Range(1, ServicioLimites.CantidadMaxima)] public int Cantidad { get; set; }
    // Solo detecta cambios desde que se mostró el catálogo. Nunca determina el cobro.
    [Range(1, int.MaxValue)] public int PrecioMostrado { get; set; }
}

public static class ServicioLimites
{
    /// <summary>Máximo de unidades por renglón (3 dígitos).</summary>
    public const int CantidadMaxima = 999;
    /// <summary>Desde este total (exclusivo) el pedido se considera fuera de lo razonable y se advierte.</summary>
    public const long TotalAdvertencia = 25_000;
    /// <summary>Total máximo permitido para un pedido.</summary>
    public const long TotalMaximo = 40_000;
}

public record ArticuloPedido(string Clave, string Nombre, string Categoria, string? Tamano, int Precio);
public record PedidoRegistrado(int IdPedido, long Total);
