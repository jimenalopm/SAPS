namespace SAPS.Web.Services.Auditoria;

/// <summary>
/// Valores conocidos de Modulo, TipoEvento y Recurso (docs/Bitacora-Diseno-HU004-HU015.md, sección 3).
/// No es una lista cerrada: la tabla acepta cualquier texto, así que un evento nuevo solo agrega una constante.
/// </summary>
public static class ModulosBitacora
{
    public const string Seguridad = "Seguridad";
    public const string Catalogo = "Catalogo";
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
    public const string CambioPrecio = "CambioPrecio";
}

public static class RecursosBitacora
{
    public const string Sesion = "Sesion";
    public const string Cuentas = "AspNetUsers";
    public const string Precio = "tb_Precio";
    public const string Bebida = "tb_Bebida";
}
