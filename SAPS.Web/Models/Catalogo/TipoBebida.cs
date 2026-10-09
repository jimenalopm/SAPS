namespace SAPS.Web.Models.Catalogo;

/// <summary>
/// Tipo de bebida (Gaseosa, Embotellada, Energizante, Jugo...). Se administra desde el catálogo,
/// así que agregar un tipo nuevo no requiere cambiar el código.
/// Tabla: tb_TipoBebida
/// </summary>
public class TipoBebida
{
    public int IdTipoBebida { get; set; }

    public required string NombreTipo { get; set; }

    public bool Activo { get; set; } = true;
}
