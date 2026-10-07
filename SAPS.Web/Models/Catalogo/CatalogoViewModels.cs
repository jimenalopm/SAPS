using System.ComponentModel.DataAnnotations;

namespace SAPS.Web.Models.Catalogo;

// ---------- Categoría ----------
public class CategoriaFormViewModel
{
    public int IdCategoria { get; set; }

    [Required(ErrorMessage = "El nombre de la categoría es obligatorio.")]
    [StringLength(50, ErrorMessage = "Máximo 50 caracteres.")]
    [NombreCatalogo]
    public string NombreCategoria { get; set; } = "";

    /// <summary>HU-007: tamaños que pueden tener los productos de esta categoría.</summary>
    public List<int> TamanosPermitidos { get; set; } = [];

    public List<Tamano> TamanosDisponibles { get; set; } = [];
}

// ---------- Tamaño ----------
public class TamanoFormViewModel
{
    public int IdTamano { get; set; }

    [Required(ErrorMessage = "El nombre del tamaño es obligatorio.")]
    [StringLength(30, ErrorMessage = "Máximo 30 caracteres.")]
    [NombreCatalogo(Minimo = 2)]
    public string NombreTamano { get; set; } = "";

    [Display(Name = "Tamaño de bebida")]
    public bool EsParaBebida { get; set; }
}

// ---------- Producto ----------

/// <summary>Una fila del formulario de producto: "¿incluir este tamaño? ¿a qué precio?".</summary>
public class PrecioPorTamanoInput
{
    public int IdTamano { get; set; }
    public string NombreTamano { get; set; } = "";
    public bool TamanoActivo { get; set; } = true;
    public int? Orden { get; set; }

    /// <summary>Ids de categoría (separados por coma) donde este tamaño está permitido; lo usa el formulario para filtrar.</summary>
    public string CategoriasPermitidas { get; set; } = "";
    public bool Incluir { get; set; }

    [Display(Name = "Precio")]
    public int? Monto { get; set; }
}

public class ProductoFormViewModel
{
    public int IdProducto { get; set; }

    [Required(ErrorMessage = "El nombre del producto es obligatorio.")]
    [StringLength(60, ErrorMessage = "Máximo 60 caracteres.")]
    [NombreCatalogo]
    public string NombreProducto { get; set; } = "";

    [Range(1, int.MaxValue, ErrorMessage = "Debe seleccionar una categoría.")]
    public int IdCategoria { get; set; }

    public bool EsEspecial { get; set; }
    public bool RequiereTamano { get; set; }

    /// <summary>Precio único, solo cuando RequiereTamano = false.</summary>
    [Display(Name = "Precio")]
    public int? PrecioUnico { get; set; }

    /// <summary>Un precio por cada tamaño activo, solo cuando RequiereTamano = true. Se cargan todos a la vez.</summary>
    public List<PrecioPorTamanoInput> PreciosPorTamano { get; set; } = [];

    public List<Categoria> CategoriasDisponibles { get; set; } = [];
}

// ---------- Bebida ----------
public class BebidaFormViewModel
{
    public int IdBebida { get; set; }

    [Required(ErrorMessage = "El nombre de la bebida es obligatorio.")]
    [StringLength(60, ErrorMessage = "Máximo 60 caracteres.")]
    [NombreCatalogo]
    public string NombreBebida { get; set; } = "";

    [Required(ErrorMessage = "Debe indicar el tipo de bebida.")]
    public string TipoBebida { get; set; } = "Gaseosa";

    public int? IdTamano { get; set; }

    // [H9] Required explícito: sin él, ASP.NET agrega uno implícito con el mensaje
    // en inglés "The Precio field is required.".
    [Required(ErrorMessage = "El precio es obligatorio.")]
    [Range(1, ReglasCatalogo.PrecioMaximo, ErrorMessage = "El precio debe estar entre ₡1 y ₡10.000.")]
    [Display(Name = "Precio")]
    public int Precio { get; set; }

    public List<Tamano> TamanosDisponibles { get; set; } = [];
}

// ---------- Pantalla principal (con pestañas) ----------
public class CatalogoIndexViewModel
{
    public string TabActiva { get; set; } = "categorias";

    public string? CategoriaBuscar { get; set; }
    public bool CategoriaIncluirInactivos { get; set; }
    public List<Categoria> Categorias { get; set; } = [];

    public string? TamanoBuscar { get; set; }
    public bool TamanoIncluirInactivos { get; set; }
    public List<Tamano> Tamanos { get; set; } = [];

    public string? ProductoBuscar { get; set; }
    public int? ProductoCategoria { get; set; }
    public List<Categoria> CategoriasFiltro { get; set; } = [];
    public bool ProductoIncluirInactivos { get; set; }
    public List<Producto> Productos { get; set; } = [];

    public string? BebidaBuscar { get; set; }
    public bool BebidaIncluirInactivos { get; set; }
    public List<Bebida> Bebidas { get; set; } = [];
}
