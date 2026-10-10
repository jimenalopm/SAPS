using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using SAPS.Web.Data;

namespace SAPS.Tests;

/// <summary>Protección: los datos de prueba solo se siembran en Development contra un servidor local.</summary>
public class SembradoPruebaTests
{
    private static string Cadena(string servidor) =>
        $"Server={servidor};Database=SAPS_DB;User Id=u;Password=x;Encrypt=False;TrustServerCertificate=True;";

    [Theory]
    [InlineData("localhost,1433")]
    [InlineData("LOCALHOST")]
    [InlineData("127.0.0.1")]
    [InlineData("127.0.0.1,1433")]
    [InlineData("tcp:localhost,1433")]
    [InlineData(".")]
    [InlineData(@".\SQLEXPRESS")]
    [InlineData("(local)")]
    [InlineData(@"(localdb)\MSSQLLocalDB")]
    public void EsServidorLocal_ReconoceServidoresLocales(string servidor) =>
        Assert.True(SembradoPrueba.EsServidorLocal(Cadena(servidor)));

    [Theory]
    [InlineData("10.195.13.2,1433")]
    [InlineData("10.195.13.2")]
    [InlineData("tcp:10.195.13.2,1433")]
    [InlineData("servidor-recyplast")]
    [InlineData("localhost.recyplast.cr")]
    [InlineData(@"192.168.1.10\SQLEXPRESS")]
    public void EsServidorLocal_TrataCualquierOtroServidorComoBaseReal(string servidor) =>
        Assert.False(SembradoPrueba.EsServidorLocal(Cadena(servidor)));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("esto no es una cadena;;==")]
    public void EsServidorLocal_CadenaVaciaOIlegible_SeTrataComoBaseReal(string? cadena) =>
        Assert.False(SembradoPrueba.EsServidorLocal(cadena));

    [Theory]
    [InlineData("Development", "localhost,1433", true)]
    [InlineData("Development", "10.195.13.2,1433", false)]
    [InlineData("Production", "localhost,1433", false)]
    [InlineData("Staging", "localhost,1433", false)]
    [InlineData("Production", "10.195.13.2,1433", false)]
    public void Permitido_RequiereDevelopmentYServidorLocal(string entorno, string servidor, bool esperado) =>
        Assert.Equal(esperado, SembradoPrueba.Permitido(new Entorno(entorno), Cadena(servidor)));

    private sealed class Entorno(string nombre) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = nombre;
        public string ApplicationName { get; set; } = "SAPS.Tests";
        public string ContentRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
