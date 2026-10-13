using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using SAPS.Tests.Helpers;
using SAPS.Web.Data;
using SAPS.Web.Models.Catalogo;
using SAPS.Web.Models.Pedidos;
using SAPS.Web.Services.Pedidos;

namespace SAPS.Tests;

/// <summary>
/// Tipos de bebida y frescos: los tipos de Registrar pedido salen solo del catálogo (ya no hay una lista
/// fija en el código), "Embotellada" y "Jugo" son ahora "Tés y refrescos" y el fresco con tamaños es la
/// bebida "Frescos naturales" del tipo "Frescos". Usa SQLite en memoria porque el servicio abre una
/// transacción Serializable.
/// </summary>
public sealed class TiposBebidaYFrescosTests : IDisposable
{
    private const string Usuario = "usr-soda001";
    private static readonly DateTimeOffset Ahora = new(2026, 10, 14, 18, 0, 0, TimeSpan.Zero);

    private readonly SqliteConnection _conexion;
    private readonly ApplicationDbContext _db;
    private readonly ServicioPedidos _servicio;

    public TiposBebidaYFrescosTests()
    {
        _conexion = TestServices.AbrirConexionSqlite();
        _db = TestServices.CrearContextoSqlite(_conexion);
        var colaboradores = new Mock<IColaboradores>();
        colaboradores.Setup(c => c.BuscarAsync("DEMO001", It.IsAny<CancellationToken>()))
                     .ReturnsAsync(new Colaborador("DEMO001", "Ana Solís (prueba)", true, "/img.png", "left"));
        _db.Users.Add(new IdentityUser { Id = Usuario, UserName = "SODA001" });
        _db.SaveChanges();
        _servicio = new ServicioPedidos(_db, colaboradores.Object, new RelojFijo(Ahora));
    }

    public void Dispose()
    {
        _db.Dispose();
        _conexion.Dispose();
    }

    private static RegistrarPedidoRequest Solicitud(string tipo, string clave, int precio) => new()
    {
        TokenRegistro = Guid.NewGuid(),
        CodigoColaborador = "DEMO001",
        TipoComida = tipo,
        Lineas = [new LineaPedidoRequest { Clave = clave, Cantidad = 1, PrecioMostrado = precio }],
    };

    [Fact]
    public async Task TiposIniciales_SonLosNuevosYYaNoExistenEmbotelladaNiJugo()
    {
        var nombres = await _db.TiposBebida.Select(t => t.NombreTipo).ToListAsync();

        Assert.Contains("Tés y refrescos", nombres);
        Assert.Contains("Frescos", nombres);
        Assert.DoesNotContain("Embotellada", nombres);
        Assert.DoesNotContain("Jugo", nombres);
    }

    [Fact]
    public void DatosInicialesDelModelo_CoincidenConLaUltimaMigracion()
    {
        using var db = TestServices.CrearContextoSqlServerSinConexion();
        var snapshot = db.GetService<IMigrationsAssembly>().ModelSnapshot!.Model;
        var modelo = db.GetService<IDesignTimeModel>().Model;
        var diferencias = db.GetService<IMigrationsModelDiffer>().GetDifferences(
            db.GetService<IModelRuntimeInitializer>().Initialize(snapshot).GetRelationalModel(),
            modelo.GetRelationalModel());

        // Si HasData y el snapshot no dicen lo mismo, EF querría insertar/actualizar/borrar tipos en la próxima migración.
        Assert.DoesNotContain(diferencias, o => o is InsertDataOperation or UpdateDataOperation or DeleteDataOperation);
        Assert.Equal("20261014120000_TiposBebidaYFrescos", db.Database.GetMigrations().Last());
    }

    [Fact]
    public void ServicioLimites_YaNoTieneUnaListaFijaDeTiposDeBebida()
    {
        Assert.Null(typeof(ServicioLimites).GetField("TiposBebida"));
    }

    [Fact]
    public async Task TipoDeBebidaNuevoDelCatalogo_AparecePorSuNombreYSeAceptaAlRegistrar()
    {
        _db.TiposBebida.Add(new TipoBebida { NombreTipo = "Café frío" });
        var bebida = new Bebida { NombreBebida = "Frappé", TipoBebida = "Café frío", Precio = 1500 };
        _db.Bebidas.Add(bebida);
        await _db.SaveChangesAsync();

        var catalogo = await _servicio.CatalogoAsync();
        var resultado = await _servicio.RegistrarAsync(Solicitud("Café frío", $"B:{bebida.IdBebida}", 1500), Usuario);

        Assert.Contains(catalogo, a => a.Grupo == "Bebida" && a.Categoria == "Café frío");
        Assert.Equal(1500, resultado.Total);
    }

    [Fact]
    public async Task TipoDeBebidaSinBebidasActivas_NoAparece_YNoSeAceptaComoTipoDelPedido()
    {
        // "Frescos" existe como tipo, pero todavía no tiene ninguna bebida.
        var bebida = new Bebida { NombreBebida = "Pepsi", TipoBebida = "Gaseosa", Precio = 1000 };
        _db.Bebidas.Add(bebida);
        await _db.SaveChangesAsync();

        var catalogo = await _servicio.CatalogoAsync();
        var ex = await Assert.ThrowsAsync<PedidoInvalidoException>(
            () => _servicio.RegistrarAsync(Solicitud("Frescos", $"B:{bebida.IdBebida}", 1000), Usuario));

        Assert.DoesNotContain(catalogo, a => a.Categoria == "Frescos");
        Assert.Contains("categoría disponible", ex.Message);
    }

    [Fact]
    public async Task TipoDeBebidaDesactivado_ConSusBebidasDesactivadas_YaNoSeOfrece()
    {
        _db.Bebidas.Add(new Bebida { NombreBebida = "Té frío", TipoBebida = "Tés y refrescos", Precio = 1000, Activo = false });
        await _db.SaveChangesAsync();

        var catalogo = await _servicio.CatalogoAsync();

        Assert.DoesNotContain(catalogo, a => a.Categoria == "Tés y refrescos");
    }

    [Fact]
    public async Task FrescosNaturales_ComoBebidaConTamanosDeBebida_SeVendenPorTipoFrescos()
    {
        var vasos = new[]
        {
            new Tamano { NombreTamano = "Vaso pequeño", EsParaBebida = true },
            new Tamano { NombreTamano = "Vaso mediano", EsParaBebida = true },
            new Tamano { NombreTamano = "Vaso grande", EsParaBebida = true },
        };
        var precios = new[] { 250, 300, 350 };
        var bebidas = vasos.Select((t, i) => new Bebida { NombreBebida = "Frescos naturales", TipoBebida = "Frescos", Tamano = t, Precio = precios[i] }).ToList();
        _db.Bebidas.AddRange(bebidas);
        await _db.SaveChangesAsync();

        var frescos = (await _servicio.CatalogoAsync()).Where(a => a.Grupo == "Bebida" && a.Categoria == "Frescos").ToList();
        var resultado = await _servicio.RegistrarAsync(Solicitud("Frescos", $"B:{bebidas[1].IdBebida}", 300), Usuario);

        Assert.Equal(["Vaso grande", "Vaso mediano", "Vaso pequeño"], frescos.Select(a => a.Tamano!).Order());
        Assert.All(frescos, a => Assert.Equal("Frescos naturales", a.Nombre));
        Assert.Equal(300, resultado.Total);
    }

    [Fact]
    public async Task CategoriaFresco_YaNoEsUnTipoDeComidaOfrecido()
    {
        // Sin la categoría "Fresco" (la migración la elimina), los tipos de comida salen solo de las que quedan.
        _db.Productos.Add(new Producto { NombreProducto = "Casado", Categoria = new Categoria { NombreCategoria = "Almuerzo" } });
        var casado = _db.Productos.Local.Single();
        _db.Precios.Add(new Precio { Producto = casado, MontoPrecio = 2800, FechaVigenciaDesde = new(2026, 1, 1) });
        await _db.SaveChangesAsync();

        var tiposDeComida = (await _servicio.CatalogoAsync()).Where(a => a.Grupo == "Comida").Select(a => a.Categoria).Distinct().ToList();

        Assert.Equal(["Almuerzo"], tiposDeComida);
    }
}
