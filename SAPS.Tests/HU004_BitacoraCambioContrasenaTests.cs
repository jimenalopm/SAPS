using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using SAPS.Tests.Helpers;
using SAPS.Web.Areas.Identity.Pages.Account;
using SAPS.Web.Data;
using SAPS.Web.Services.Auditoria;

namespace SAPS.Tests;

/// <summary>
/// HU-004 | El cambio de contraseña queda en la bitácora (no solo en el log técnico del servidor):
/// el cambio exitoso, con su motivo, y el intento fallido por contraseña actual incorrecta.
/// Nunca se guarda ninguna contraseña.
/// </summary>
public class HU004_BitacoraCambioContrasenaTests
{
    private const string ContrasenaActual = "Prueba123";
    private const string ContrasenaNueva = "Nueva456";

    private sealed class BitacoraFalsa : IServicioBitacora
    {
        public List<EventoBitacora> Eventos { get; } = [];

        public Task<bool> RegistrarAsync(EventoBitacora evento, CancellationToken ct = default)
        {
            Eventos.Add(evento);
            return Task.FromResult(true);
        }
    }

    private static async Task<(CambiarContrasenaModel pagina, BitacoraFalsa bitacora, IdentityUser usuario)> CrearAsync(
        string actual, string nueva, bool obligatorio = false, string? motivo = null)
    {
        var sp = TestServices.Crear();
        var um = sp.GetRequiredService<UserManager<IdentityUser>>();
        var sm = sp.GetRequiredService<SignInManager<IdentityUser>>();
        var db = sp.GetRequiredService<ApplicationDbContext>();
        var usuario = new IdentityUser { UserName = "EMP001" };
        Assert.True((await um.CreateAsync(usuario, ContrasenaActual)).Succeeded);

        var bitacora = new BitacoraFalsa();
        var reloj = new RelojFijo(new DateTimeOffset(2026, 10, 9, 12, 0, 0, TimeSpan.Zero));
        var http = new DefaultHttpContext { User = await sm.CreateUserPrincipalAsync(usuario) };
        var pagina = new CambiarContrasenaModel(um, sm, db, reloj, NullLogger<CambiarContrasenaModel>.Instance, bitacora)
        {
            PageContext = new PageContext { HttpContext = http },
            Obligatorio = obligatorio,
            Motivo = motivo,
            Input = new CambiarContrasenaModel.InputModel
            {
                ContrasenaActual = actual, ContrasenaNueva = nueva, ConfirmarContrasenaNueva = nueva,
            },
        };
        return (pagina, bitacora, usuario);
    }

    [Fact]
    public async Task CambioVoluntario_QuedaEnLaBitacoraSinGuardarContrasenas()
    {
        var (pagina, bitacora, usuario) = await CrearAsync(ContrasenaActual, ContrasenaNueva);

        await pagina.OnPostAsync();

        var evento = Assert.Single(bitacora.Eventos);
        Assert.Equal(ModulosBitacora.Seguridad, evento.Modulo);
        Assert.Equal(TiposEventoBitacora.ContrasenaCambiada, evento.TipoEvento);
        Assert.Equal(RecursosBitacora.Cuentas, evento.Recurso);
        Assert.True(evento.EsExitoso);
        Assert.Equal(usuario.Id, evento.IdRegistro);
        Assert.Contains("voluntario", evento.Descripcion);
        Assert.DoesNotContain(ContrasenaActual, evento.Descripcion);
        Assert.DoesNotContain(ContrasenaNueva, evento.Descripcion);
        Assert.Null(evento.ValorAnterior);
        Assert.Null(evento.ValorNuevo);
    }

    [Theory]
    [InlineData("primerIngreso", "primer ingreso")]
    [InlineData(null, "vencida")]
    public async Task CambioObligatorio_IndicaElMotivo(string? motivo, string textoEsperado)
    {
        var (pagina, bitacora, _) = await CrearAsync(ContrasenaActual, ContrasenaNueva, obligatorio: true, motivo);

        await pagina.OnPostAsync();

        var evento = Assert.Single(bitacora.Eventos);
        Assert.True(evento.EsExitoso);
        Assert.Contains(textoEsperado, evento.Descripcion);
    }

    [Fact]
    public async Task ContrasenaActualIncorrecta_QuedaComoIntentoFallido()
    {
        var (pagina, bitacora, usuario) = await CrearAsync("Incorrecta1", ContrasenaNueva);

        await pagina.OnPostAsync();

        var evento = Assert.Single(bitacora.Eventos);
        Assert.Equal(TiposEventoBitacora.ContrasenaCambiada, evento.TipoEvento);
        Assert.False(evento.EsExitoso);
        Assert.Equal(usuario.Id, evento.IdRegistro);
        Assert.DoesNotContain("Incorrecta1", evento.Descripcion);
    }

    [Fact]
    public async Task ContrasenaNuevaQueNoCumpleLasReglas_NoSeRegistra()
    {
        var (pagina, bitacora, _) = await CrearAsync(ContrasenaActual, "abc");

        await pagina.OnPostAsync();

        Assert.Empty(bitacora.Eventos);
    }

    [Fact]
    public void ElEventoTieneEtiquetaYApareceEnElFiltro()
    {
        Assert.Equal("Cambio de contraseña", PresentacionBitacora.EtiquetaEvento(TiposEventoBitacora.ContrasenaCambiada));
        Assert.Contains(PresentacionBitacora.EventosConocidos,
            e => e.Modulo == ModulosBitacora.Seguridad && e.TipoEvento == TiposEventoBitacora.ContrasenaCambiada);
    }
}
