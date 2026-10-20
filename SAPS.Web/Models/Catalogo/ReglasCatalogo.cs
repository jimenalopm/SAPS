namespace SAPS.Web.Models.Catalogo;

/// <summary>HU-007 | Límites de negocio del catálogo, en un solo lugar.</summary>
public static class ReglasCatalogo
{
    /// <summary>Precio máximo aceptado, en colones enteros (RNF-003). Evita errores de digitación.</summary>
    public const int PrecioMaximo = 10_000;

    public static string MensajePrecioMaximo => $"El precio no puede superar ₡{PrecioMaximo:N0}.";
}

/// <summary>
/// HU-007 | Mensajes de "no se puede desactivar" con conteos en singular o plural y sin mencionar lo que
/// está en cero (antes: "lo usan 2 producto(s) y 0 bebida(s) activos").
/// </summary>
public static class MensajesCatalogo
{
    public static string NoDesactivarCategoria(string categoria, int productos) =>
        $"No se puede desactivar la categoría «{categoria}»: tiene {Productos(productos)}. "
        + (productos == 1 ? "Desactive primero ese producto." : "Desactive primero esos productos.");

    public static string NoDesactivarTamano(string tamano, int productos, int bebidas)
    {
        var partes = new List<string>();
        if (productos > 0) partes.Add(Productos(productos));
        if (bebidas > 0) partes.Add(Bebidas(bebidas));
        var total = productos + bebidas;
        var verbo = total == 1 ? "lo usa" : "lo usan";
        var accion = (total, productos) switch
        {
            (1, 1) => "Desactívelo o cámbiele el tamaño primero.",
            (1, _) => "Desactívela o cámbiele el tamaño primero.",
            (_, 0) => "Desactívelas o cámbieles el tamaño primero.",
            _ => "Desactívelos o cámbieles el tamaño primero.",
        };
        return $"No se puede desactivar el tamaño «{tamano}»: {verbo} {string.Join(" y ", partes)}. {accion}";
    }

    public static string NoDesactivarTipoBebida(string tipo, int bebidas) =>
        $"No se puede desactivar el tipo «{tipo}»: {(bebidas == 1 ? "lo usa" : "lo usan")} {Bebidas(bebidas)}. "
        + (bebidas == 1 ? "Desactívela o cámbiele el tipo primero." : "Desactívelas o cámbieles el tipo primero.");

    private static string Productos(int n) => n == 1 ? "1 producto activo" : $"{n} productos activos";

    private static string Bebidas(int n) => n == 1 ? "1 bebida activa" : $"{n} bebidas activas";
}
