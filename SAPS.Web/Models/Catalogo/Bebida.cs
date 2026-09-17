namespace SAPS.Web.Models.Catalogo;

/// <summary>
/// Bebidas gaseosas y embotelladas, en tabla separada del resto del menú
/// tal como lo pide el requerimiento.
/// Tabla: tb_Bebida
/// </summary>
public class Bebida
{
    public int IdBebida { get; set; }

    public required string NombreBebida { get; set; }

    /// <summary>Uno de: Gaseosa, Embotellada, Energizante, Jugo.</summary>
    public required string TipoBebida { get; set; }

    public int? IdTamano { get; set; }
    public Tamano? Tamano { get; set; }

    /// <summary>Monto en colones, número entero.</summary>
    public int Precio { get; set; }

    public bool Activo { get; set; } = true;
}