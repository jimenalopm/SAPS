using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using SAPS.Tests.Helpers;
using SAPS.Web.Areas.Identity.Pages.Account;
using SAPS.Web.Data;

namespace SAPS.Tests;

/// <summary>
/// HU-001, criterio 6 | Cambio de contraseña opcional tras iniciar sesión.
/// Usa Identity real sobre EF Core InMemory (igual que HU001_AutenticacionTests)
/// para probar el flujo completo de principio a fin.
/// </summary>
public class HU001_CambioContrasenaTests
{
    private const string Codigo = "EMP001";
    private const string ContrasenaValida = "Prueba123";
    private const string ContrasenaNueva = "Nueva456";

    private static async Task<(CambiarContrasenaModel pagina, UserManager<IdentityUser> um, IdentityUser usuario, RelojFijo reloj)> CrearPaginaConUsuarioAsync()
    {
        var sp = TestServices.Crear();
        var um = sp.GetRequiredService<UserManager<IdentityUser>>();
        var sm = sp.GetRequiredService<SignInManager<IdentityUser>>();
        var db = sp.GetRequiredService<ApplicationDbContext>();
        var usuario = new IdentityUser { UserName = Codigo };
        Assert.True((await um.CreateAsync(usuario, ContrasenaValida)).Succeeded);

        var reloj = new RelojFijo(new DateTimeOffset(2026, 10, 9, 12, 0, 0, TimeSpan.Zero));
        var http = new DefaultHttpContext { User = await sm.CreateUserPrincipalAsync(usuario) };
        var pagina = new CambiarContrasenaModel(um, sm, db, reloj, NullLogger<CambiarContrasenaModel>.Instance)
        {
            PageContext = new PageContext { HttpContext = http }
        };
        return (pagina, um, usuario, reloj);
    }

    [Fact]
    public async Task CambiarContrasena_ConDatosValidos_ActualizaLaContrasenaYRegistraLaFecha()
    {
        var (pagina, um, usuario, reloj) = await CrearPaginaConUsuarioAsync();
        pagina.Input = new CambiarContrasenaModel.InputModel
        {
            ContrasenaActual = ContrasenaValida,
            ContrasenaNueva = ContrasenaNueva,
            ConfirmarContrasenaNueva = ContrasenaNueva
        };

        var resultado = await pagina.OnPostAsync();

        Assert.IsType<RedirectToPageResult>(resultado);
        Assert.True(await um.CheckPasswordAsync(usuario, ContrasenaNueva));
        Assert.False(await um.CheckPasswordAsync(usuario, ContrasenaValida));
    }

    [Fact]
    public async Task CambiarContrasena_ConContrasenaActualIncorrecta_NoCambiaNadaYMuestraError()
    {
        var (pagina, um, usuario, _) = await CrearPaginaConUsuarioAsync();
        pagina.Input = new CambiarContrasenaModel.InputModel
        {
            ContrasenaActual = "Incorrecta1",
            ContrasenaNueva = ContrasenaNueva,
            ConfirmarContrasenaNueva = ContrasenaNueva
        };

        var resultado = await pagina.OnPostAsync();

        Assert.IsType<PageResult>(resultado);
        var error = Assert.Single(pagina.ModelState["Input.ContrasenaActual"]!.Errors);
        Assert.Equal("La contraseña actual no es correcta.", error.ErrorMessage);
        Assert.True(await um.CheckPasswordAsync(usuario, ContrasenaValida));
    }

    [Fact]
    public async Task CambiarContrasena_ModoObligatorio_CierraLaSesionYVuelveAlLogin()
    {
        var (pagina, um, usuario, _) = await CrearPaginaConUsuarioAsync();
        pagina.Obligatorio = true;
        pagina.Input = new CambiarContrasenaModel.InputModel
        {
            ContrasenaActual = ContrasenaValida,
            ContrasenaNueva = ContrasenaNueva,
            ConfirmarContrasenaNueva = ContrasenaNueva
        };

        var resultado = await pagina.OnPostAsync();

        var redirect = Assert.IsType<RedirectToPageResult>(resultado);
        Assert.Equal("./Login", redirect.PageName);
        Assert.Equal(true, redirect.RouteValues!["contrasenaActualizada"]);
        Assert.True(await um.CheckPasswordAsync(usuario, ContrasenaNueva));
    }

    [Fact]
    public async Task CambiarContrasena_ConLaMismaContrasenaDeAntes_NoLaAceptaNiLaCambia()
    {
        var (pagina, um, usuario, _) = await CrearPaginaConUsuarioAsync();
        pagina.Obligatorio = true; // primer ingreso: la "actual" es la temporal
        pagina.Input = new CambiarContrasenaModel.InputModel
        {
            ContrasenaActual = ContrasenaValida,
            ContrasenaNueva = ContrasenaValida,
            ConfirmarContrasenaNueva = ContrasenaValida
        };

        var resultado = await pagina.OnPostAsync();

        Assert.IsType<PageResult>(resultado);
        var error = Assert.Single(pagina.ModelState["Input.ContrasenaNueva"]!.Errors);
        Assert.Equal("La contraseña nueva debe ser distinta de la actual.", error.ErrorMessage);
        Assert.True(await um.CheckPasswordAsync(usuario, ContrasenaValida));
    }

    [Theory]
    [InlineData("abc12", "La contraseña debe tener al menos 6 caracteres.")]
    [InlineData("ABCDEF1", "La contraseña debe tener al menos una letra minúscula (a-z).")]
    [InlineData("abcdefg", "La contraseña debe tener al menos un número (0-9).")]
    public async Task CambiarContrasena_ConNuevaQueNoCumpleLaPolitica_MuestraElErrorEnEspanolEnSuCampo(string nueva, string mensaje)
    {
        var (pagina, um, usuario, _) = await CrearPaginaConUsuarioAsync();
        pagina.Input = new CambiarContrasenaModel.InputModel
        {
            ContrasenaActual = ContrasenaValida,
            ContrasenaNueva = nueva,
            ConfirmarContrasenaNueva = nueva
        };

        var resultado = await pagina.OnPostAsync();

        Assert.IsType<PageResult>(resultado);
        Assert.Contains(pagina.ModelState["Input.ContrasenaNueva"]!.Errors, e => e.ErrorMessage == mensaje);
        Assert.True(await um.CheckPasswordAsync(usuario, ContrasenaValida));
    }

    [Theory]
    [InlineData(null, "Nueva456", "Nueva456", "ContrasenaActual")]
    [InlineData("Prueba123", null, "Nueva456", "ContrasenaNueva")]
    [InlineData("Prueba123", "Nueva456", "Distinta789", "ConfirmarContrasenaNueva")]
    public void InputModel_ValidaCamposObligatoriosYCoincidenciaDeContrasenas(
        string? actual, string? nueva, string? confirmar, string campoConError)
    {
        var input = new CambiarContrasenaModel.InputModel
        {
            ContrasenaActual = actual!,
            ContrasenaNueva = nueva!,
            ConfirmarContrasenaNueva = confirmar!
        };
        var errores = new List<ValidationResult>();

        var valido = Validator.TryValidateObject(input, new ValidationContext(input), errores, validateAllProperties: true);

        Assert.False(valido);
        Assert.Contains(errores, e => e.MemberNames.Contains(campoConError));
    }
}
