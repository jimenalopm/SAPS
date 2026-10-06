using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SAPS.Tests.Helpers;
using SAPS.Web.Data;
using SAPS.Web.Models.Pedidos;
using SAPS.Web.Models.Rrhh;
using SAPS.Web.Services.Pedidos;

namespace SAPS.Tests;

/// <summary>RNF-006: los colaboradores salen de rrhh.tb_Colaborador. Los datos son ficticios; no hay nombres reales.</summary>
public sealed class HU008_ColaboradoresTablaTests : IDisposable
{
    private readonly SqliteConnection _conexion = TestServices.AbrirConexionSqlite();
    private readonly ApplicationDbContext _db;
    private readonly ServicioPedidos _servicio;

    public HU008_ColaboradoresTablaTests()
    {
        _db = TestServices.CrearContextoSqlite(_conexion);
        _db.Colaboradores.AddRange(
            new ColaboradorRrhh { Codigo = "0000000842", NombreCompleto = "Persona Activa", EstaActivo = true, FechaRegistro = DateTime.UtcNow },
            new ColaboradorRrhh { Codigo = "0000000099", NombreCompleto = "Persona Inactiva", EstaActivo = false, FechaRegistro = DateTime.UtcNow });
        _db.SaveChanges();
        _servicio = new ServicioPedidos(_db, new ColaboradoresTabla(_db), new RelojFijo(DateTimeOffset.UtcNow));
    }

    public void Dispose()
    {
        _db.Dispose();
        _conexion.Dispose();
    }

    // ---------- Normalización del código ----------

    [Theory]
    [InlineData("842", "0000000842")]
    [InlineData("  842  ", "0000000842")]
    [InlineData("0000000842", "0000000842")]
    [InlineData("1", "0000000001")]
    [InlineData("0842", "0000000842")]
    [InlineData("demo001", "DEMO001")]
    [InlineData(" DEMO001 ", "DEMO001")]
    [InlineData("12345678901", "12345678901")]   // más de 10 dígitos: no se altera
    [InlineData("84 2", "84 2")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void Normalizar_CompletaConCerosLosCodigosNumericos(string? entrada, string esperado) =>
        Assert.Equal(esperado, CodigoColaborador.Normalizar(entrada));

    // ---------- Los tres resultados de la búsqueda ----------

    [Fact]
    public async Task Buscar_CodigoQueNoExiste_DiceColaboradorNoEncontrado()
    {
        var ex = await Assert.ThrowsAsync<ColaboradorNoEncontradoException>(() => _servicio.BuscarColaboradorAsync("777"));

        Assert.Contains("no encontrado", ex.Message);
    }

    [Fact]
    public async Task Buscar_ColaboradorInactivo_DiceColaboradorInactivo()
    {
        var ex = await Assert.ThrowsAsync<ColaboradorInactivoException>(() => _servicio.BuscarColaboradorAsync("99"));

        Assert.Contains("inactivo", ex.Message);
    }

    [Fact]
    public async Task Buscar_ColaboradorActivo_DevuelveSusDatosYNormalizaElCodigo()
    {
        var colaborador = await _servicio.BuscarColaboradorAsync("842");

        Assert.Equal("0000000842", colaborador.Codigo);
        Assert.Equal("Persona Activa", colaborador.Nombre);
        Assert.True(colaborador.Activo);
        Assert.Null(colaborador.FotoUrl);   // las fotos todavía no existen
        Assert.False(colaborador.EsDemo);
    }

    [Fact]
    public async Task Buscar_ConCerosYSinCeros_EncuentraAlMismoColaborador()
    {
        var sinCeros = await _servicio.BuscarColaboradorAsync("842");
        var conCeros = await _servicio.BuscarColaboradorAsync("0000000842");

        Assert.Equal(sinCeros, conCeros);
    }

    [Fact]
    public async Task Buscar_DarDeBajaYDeAlta_CambiaElResultadoSinTocarElCodigo()
    {
        await _db.Database.ExecuteSqlRawAsync("UPDATE tb_Colaborador SET EstaActivo = 0 WHERE Codigo = '0000000842'");
        await Assert.ThrowsAsync<ColaboradorInactivoException>(() => _servicio.BuscarColaboradorAsync("842"));

        await _db.Database.ExecuteSqlRawAsync("UPDATE tb_Colaborador SET EstaActivo = 1 WHERE Codigo = '0000000842'");
        Assert.Equal("0000000842", (await _servicio.BuscarColaboradorAsync("842")).Codigo);
    }

    [Fact]
    public async Task Registrar_ColaboradorInactivo_NoGuardaElPedido()
    {
        _db.Users.Add(new IdentityUser { Id = "u1", UserName = "SODA001" });
        await _db.SaveChangesAsync();
        var solicitud = new RegistrarPedidoRequest
        {
            TokenRegistro = Guid.NewGuid(), CodigoColaborador = "99", TipoComida = "Almuerzo",
            Lineas = [new LineaPedidoRequest { Clave = "B:1", Cantidad = 1, PrecioMostrado = 100 }]
        };

        await Assert.ThrowsAsync<ColaboradorInactivoException>(() => _servicio.RegistrarAsync(solicitud, "u1"));

        Assert.Empty(_db.Pedidos);
    }

    // ---------- Foto y modelo ----------

    [Theory]
    [InlineData("/fotos/a.png")]
    [InlineData("https://intranet/fotos/a.png")]
    public void FotoSegura_AceptaRutasLocalesYHttp(string foto) => Assert.Equal(foto, ColaboradoresTabla.FotoSegura(foto));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("javascript:alert(1)")]
    [InlineData("//otro.com/a.png")]
    [InlineData("/a.png\");background:url(\"x")]
    public void FotoSegura_IgnoraValoresPeligrososOVacios(string? foto) => Assert.Null(ColaboradoresTabla.FotoSegura(foto));

    [Fact]
    public void Modelo_TablaRrhhConCodigoUnicoYFotoOpcional()
    {
        using var ctx = TestServices.CrearContextoSqlServerSinConexion();
        var tipo = ctx.Model.FindEntityType(typeof(ColaboradorRrhh))!;

        Assert.Equal("tb_Colaborador", tipo.GetTableName());
        Assert.Equal("rrhh", tipo.GetSchema());
        Assert.Equal("PK_Colaborador", tipo.FindPrimaryKey()!.GetName());
        var indice = Assert.Single(tipo.GetIndexes());
        Assert.True(indice.IsUnique);
        Assert.Equal("UQ_Colaborador_Codigo", indice.GetDatabaseName());
        Assert.Equal(10, tipo.FindProperty(nameof(ColaboradorRrhh.Codigo))!.GetMaxLength());
        Assert.Equal(100, tipo.FindProperty(nameof(ColaboradorRrhh.NombreCompleto))!.GetMaxLength());
        Assert.True(tipo.FindProperty(nameof(ColaboradorRrhh.RutaFoto))!.IsNullable);
        Assert.Equal(true, tipo.FindProperty(nameof(ColaboradorRrhh.EstaActivo))!.GetDefaultValue());
    }

    // ---------- Protección del sembrado ----------

    [Fact]
    public async Task SembrarColaboradores_InsertaLosDemoUnaSolaVez()
    {
        await SembradoPrueba.SembrarColaboradoresAsync(_db);
        await SembradoPrueba.SembrarColaboradoresAsync(_db);

        var demos = await _db.Colaboradores.Where(c => c.Codigo.StartsWith("DEMO")).OrderBy(c => c.Codigo).ToListAsync();
        Assert.Equal(["DEMO001", "DEMO002", "DEMO003"], demos.Select(d => d.Codigo));
        Assert.Equal([true, true, false], demos.Select(d => d.EstaActivo));
        Assert.True((await _servicio.BuscarColaboradorAsync("demo001")).EsDemo);
        await Assert.ThrowsAsync<ColaboradorInactivoException>(() => _servicio.BuscarColaboradorAsync("DEMO003"));
    }

    [Fact]
    public void SinSembrado_LaTablaSoloTieneColaboradoresReales() =>
        Assert.DoesNotContain(_db.Colaboradores, c => c.Codigo.StartsWith("DEMO"));
}
