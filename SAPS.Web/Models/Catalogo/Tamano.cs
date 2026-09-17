namespace SAPS.Web.Models.Catalogo;

/// <summary>
/// Catálogo genérico de tamaños/presentaciones: Pequeño, Mediano, Grande
/// (frescos naturales, almuerzo) y presentaciones de bebida (600ml, 1L, 2.5L, 3L...).
/// Tabla: tb_Tamano
/// </summary>
public class Tamano
{
    public int IdTamano { get; set; }

    public required string NombreTamano { get; set; }

    public bool Activo { get; set; } = true;

    public ICollection<Precio> Precios { get; set; } = new List<Precio>();

    public ICollection<Bebida> Bebidas { get; set; } = new List<Bebida>();
}