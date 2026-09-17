namespace SAPS.Web.Models.Catalogo;

/// <summary>
/// Platillos y acompañamientos del menú (no bebidas embotelladas/gaseosas,
/// que se manejan aparte en <see cref="Bebida"/>).
/// Tabla: tb_Producto
/// </summary>
public class Producto
{
    public int IdProducto { get; set; }

    public required string NombreProducto { get; set; }

    public int IdCategoria { get; set; }
    public Categoria? Categoria { get; set; }

    /// <summary>Platillos con precio diferenciado del estándar (rice and beans, olla de carne, lasaña).</summary>
    public bool EsEspecial { get; set; }

    /// <summary>Indica si el producto se vende en varios tamaños y por tanto tendrá varias filas en tb_Precio.</summary>
    public bool RequiereTamano { get; set; }

    public bool Activo { get; set; } = true;

    public ICollection<Precio> Precios { get; set; } = new List<Precio>();
}