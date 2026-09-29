namespace SAPS.Tests;

/// <summary>
/// HU-008 | Registrar pedido de un colaborador.
/// A la fecha no existe implementación en SAPS.Web (no hay entidad Pedido/DetallePedido,
/// servicio ni controlador), por lo que los criterios de aceptación quedan registrados
/// como pruebas pendientes. Al implementar la HU, reemplazar cada Skip por la prueba real.
/// </summary>
public class HU008_PedidoTests
{
    private const string Pendiente = "HU-008 pendiente de implementación: no existe el módulo de pedidos en SAPS.Web.";

    [Fact(Skip = Pendiente)]
    public void Pedido_ColaboradorAutenticado_RegistraPedidoConProductosDelCatalogo() { }

    [Fact(Skip = Pendiente)]
    public void Pedido_QuedaAsociadoAlCodigoDeEmpleadoQueLoRegistra() { }

    [Fact(Skip = Pendiente)]
    public void Pedido_TotalSeCalculaConLosPreciosVigentesComoEntero() { }

    [Fact(Skip = Pendiente)]
    public void Pedido_ProductoConTamano_UsaElPrecioDelTamanoSeleccionado() { }

    [Fact(Skip = Pendiente)]
    public void Pedido_SinProductos_EsRechazado() { }

    [Fact(Skip = Pendiente)]
    public void Pedido_ConProductoInactivo_EsRechazado() { }

    [Fact(Skip = Pendiente)]
    public void Pedido_CantidadMenorOIgualACero_EsRechazada() { }

    [Fact(Skip = Pendiente)]
    public void Pedido_UsuarioNoAutenticado_NoPuedeRegistrarPedido() { }
}
