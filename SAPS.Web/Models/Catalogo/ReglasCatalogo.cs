namespace SAPS.Web.Models.Catalogo;

/// <summary>HU-007 | Límites de negocio del catálogo, en un solo lugar.</summary>
public static class ReglasCatalogo
{
    /// <summary>Precio máximo aceptado, en colones enteros (RNF-003). Evita errores de digitación.</summary>
    public const int PrecioMaximo = 10_000;

    public static string MensajePrecioMaximo => $"El precio no puede superar ₡{PrecioMaximo:N0}.";
}
