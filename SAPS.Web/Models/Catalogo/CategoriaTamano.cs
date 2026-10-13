namespace SAPS.Web.Models.Catalogo;

/// <summary>
/// HU-007: tamaños que se pueden usar en los productos de una categoría (relación muchos a muchos).
/// Evita, por ejemplo, ofrecer "600ml" en un producto de Desayuno.
/// Tabla: tb_CategoriaTamano
/// </summary>
public class CategoriaTamano
{
    public int IdCategoria { get; set; }
    public Categoria? Categoria { get; set; }

    public int IdTamano { get; set; }
    public Tamano? Tamano { get; set; }
}
