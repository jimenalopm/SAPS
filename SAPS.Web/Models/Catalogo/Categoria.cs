namespace SAPS.Web.Models.Catalogo;

/// <summary>
/// Tipo de producto del menú: Desayuno, Almuerzo, Acompañamiento, Café/Repostería, Fresco.
/// Tabla: tb_Categoria
/// </summary>
public class Categoria
{
    public int IdCategoria { get; set; }

    public required string NombreCategoria { get; set; }

    public bool Activo { get; set; } = true;

    public ICollection<Producto> Productos { get; set; } = new List<Producto>();
}