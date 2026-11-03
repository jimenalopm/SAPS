namespace SAPS.Web.Services.Auditoria;

/// <summary>
/// Valores conocidos de Modulo, TipoEvento y Recurso (docs/Bitacora-Diseno-HU004-HU015.md, sección 3).
/// No es una lista cerrada: la tabla acepta cualquier texto, así que un evento nuevo solo agrega una constante.
/// </summary>
public static class ModulosBitacora
{
    public const string Seguridad = "Seguridad";
    public const string Catalogo = "Catalogo";
    public const string Pedidos = "Pedidos";
}

public static class TiposEventoBitacora
{
    public const string InicioSesion = "InicioSesion";
    public const string CierreSesion = "CierreSesion";
    public const string CuentaCreada = "CuentaCreada";
    public const string CuentaDesactivada = "CuentaDesactivada";
    public const string CuentaReactivada = "CuentaReactivada";
    public const string CuentaModificada = "CuentaModificada";
    public const string RolCambiado = "RolCambiado";
    public const string ContrasenaCambiada = "ContrasenaCambiada";
    public const string CambioPrecio = "CambioPrecio";
    public const string CatalogoCreado = "CatalogoCreado";
    public const string CatalogoModificado = "CatalogoModificado";
    public const string CatalogoActivado = "CatalogoActivado";
    public const string CatalogoDesactivado = "CatalogoDesactivado";
    public const string PedidoRegistrado = "PedidoRegistrado";
    public const string PedidoRechazado = "PedidoRechazado";
}

public static class RecursosBitacora
{
    public const string Sesion = "Sesion";
    public const string Cuentas = "AspNetUsers";
    public const string Precio = "tb_Precio";
    public const string Bebida = "tb_Bebida";
    public const string Producto = "tb_Producto";
    public const string Categoria = "tb_Categoria";
    public const string Tamano = "tb_Tamano";
    public const string TipoBebida = "tb_TipoBebida";
    public const string Pedido = "tb_Pedido";
}
