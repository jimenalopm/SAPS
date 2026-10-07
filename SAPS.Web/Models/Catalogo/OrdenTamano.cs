using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace SAPS.Web.Models.Catalogo;

/// <summary>
/// HU-007 | Deduce automáticamente qué tan grande es un tamaño a partir de su nombre, para validar
/// que un tamaño mayor no cueste menos que uno menor. Nadie tiene que escribir un número.
/// Reconoce palabras (pequeño, mediano, grande, extra grande, familiar) y volúmenes (600ml, 1L, 2.5 litros, 12oz).
/// Si no reconoce el nombre devuelve null y ese tamaño no se compara con los demás.
/// </summary>
public static partial class OrdenTamano
{
    [GeneratedRegex(@"(\d+(?:[.,]\d+)?)\s*(ml|cc|lts|lt|litros|litro|l|oz)\b")]
    private static partial Regex Volumen();

    public static int? Calcular(string? nombre)
    {
        if (string.IsNullOrWhiteSpace(nombre)) return null;
        var texto = SinTildes(nombre).ToLowerInvariant();

        // Volúmenes: se comparan en mililitros (siempre quedan por encima de las palabras).
        var m = Volumen().Match(texto);
        if (m.Success)
        {
            var cantidad = double.Parse(m.Groups[1].Value.Replace(',', '.'), CultureInfo.InvariantCulture);
            var ml = m.Groups[2].Value switch
            {
                "ml" or "cc" => cantidad,
                "oz" => cantidad * 29.5735,
                _ => cantidad * 1000, // litros
            };
            return Math.Max(10, (int)Math.Round(ml));
        }

        if (texto.Contains("pequen") || texto.Contains("chic") || texto.Contains("small") || texto.Contains("mini")) return 1;
        if (texto.Contains("median") || texto.Contains("medio") || texto.Contains("regular")) return 2;
        if (texto.Contains("grande") || texto.Contains("large")) return texto.Contains("extra") ? 4 : 3;
        if (texto.Contains("familiar") || texto.Contains("jumbo")) return 5;
        return null;
    }

    private static string SinTildes(string texto)
    {
        var descompuesto = texto.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(descompuesto.Length);
        foreach (var c in descompuesto)
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) sb.Append(c);
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }
}
