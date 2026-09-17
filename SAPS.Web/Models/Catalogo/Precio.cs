namespace SAPS.Web.Models.Catalogo;

/// <summary>
/// Precio vigente (y su historial) de cada producto/tamaño. Solo números enteros,
/// sin decimales (RNF-003). IdTamano es null cuando el producto no maneja tamaños.
/// Al actualizar un precio se cierra la fila anterior (FechaVigenciaHasta) y se
/// inserta una nueva activa, en vez de sobrescribir: así queda historial.
/// Tabla: tb_Precio
/// </summary>
public class Precio
{
    public int IdPrecio { get; set; }

    public int IdProducto { get; set; }
    public Producto? Producto { get; set; }

    public int? IdTamano { get; set; }
    public Tamano? Tamano { get; set; }

    /// <summary>Monto en colones, número entero (columna de BD: precio).</summary>
    public int MontoPrecio { get; set; }

    public DateOnly FechaVigenciaDesde { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    public DateOnly? FechaVigenciaHasta { get; set; }

    public bool Activo { get; set; } = true;
}