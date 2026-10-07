using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace SAPS.Web.Models.Catalogo;

/// <summary>
/// HU-007 | Validación de nombres del catálogo (categorías, productos y bebidas): mínimo de
/// caracteres, al menos una letra y solo caracteres que la BD (varchar, sin Unicode) guarda sin
/// pérdida. El largo máximo lo valida [StringLength] en cada modelo.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed partial class NombreCatalogoAttribute : ValidationAttribute
{
    public const int LargoMinimo = 3;

    /// <summary>Mínimo de caracteres de este campo (los tamaños admiten nombres cortos como «1L»).</summary>
    public int Minimo { get; set; } = LargoMinimo;

    // Letras (con tildes y ñ), números, espacio y signos comunes en nombres de comida y bebida.
    [GeneratedRegex(@"^[A-Za-zÁÉÍÓÚÜÑáéíóúüñ0-9 .,/()&'%+\-]+$")]
    private static partial Regex CaracteresPermitidos();

    [GeneratedRegex(@"[A-Za-zÁÉÍÓÚÜÑáéíóúüñ]")]
    private static partial Regex ContieneLetra();

    [GeneratedRegex(@"\s+")]
    private static partial Regex EspaciosRepetidos();

    /// <summary>Quita espacios al inicio y al final y deja un solo espacio entre palabras.</summary>
    public static string Normalizar(string? nombre) =>
        EspaciosRepetidos().Replace((nombre ?? "").Trim(), " ");

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        // [Required] se encarga del vacío; aquí solo se evalúa si hay texto.
        if (value is not string texto || string.IsNullOrWhiteSpace(texto)) return ValidationResult.Success;

        var nombre = Normalizar(texto);
        if (nombre.Length < Minimo)
            return new ValidationResult($"El nombre debe tener al menos {Minimo} caracteres.");
        if (!ContieneLetra().IsMatch(nombre))
            return new ValidationResult("El nombre debe incluir letras; no puede ser solo números o símbolos.");
        if (!CaracteresPermitidos().IsMatch(nombre))
            return new ValidationResult("El nombre solo puede tener letras, números y los signos . , / ( ) & ' % + -");
        return ValidationResult.Success;
    }
}
