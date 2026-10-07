using SAPS.Web.Models.Catalogo;

namespace SAPS.Tests;

/// <summary>
/// HU-007 | El sistema deduce solo qué tan grande es un tamaño a partir de su nombre,
/// para validar que un tamaño mayor no cueste menos que uno menor.
/// </summary>
public class OrdenTamanoTests
{
    [Theory]
    [InlineData("Pequeño", 1)]
    [InlineData("pequeno", 1)]
    [InlineData("Chico", 1)]
    [InlineData("Mediano", 2)]
    [InlineData("Regular", 2)]
    [InlineData("Grande", 3)]
    [InlineData("Extra grande", 4)]
    [InlineData("Familiar", 5)]
    [InlineData("600ml", 600)]
    [InlineData("355 ml", 355)]
    [InlineData("1L", 1000)]
    [InlineData("2.5L", 2500)]
    [InlineData("3 litros", 3000)]
    public void Calcular_ReconoceElTamano(string nombre, int esperado) =>
        Assert.Equal(esperado, OrdenTamano.Calcular(nombre));

    [Theory]
    [InlineData("Combo")]
    [InlineData("Porción")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Calcular_NombreDesconocido_DevuelveNulo(string? nombre) =>
        Assert.Null(OrdenTamano.Calcular(nombre));

    [Fact]
    public void Calcular_RespetaElOrdenDePequenoAGrande()
    {
        var orden = new[] { "Pequeño", "Mediano", "Grande", "Extra grande" }.Select(OrdenTamano.Calcular).ToList();
        Assert.Equal(orden.OrderBy(x => x).ToList(), orden);
    }
}
