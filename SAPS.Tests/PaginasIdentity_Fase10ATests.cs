using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SAPS.Tests.Helpers;
using SAPS.Web.Areas.Identity.Pages.Account;

namespace SAPS.Tests;

/// <summary>
/// Fase 10A | Páginas de Identity y de estado rediseñadas.
/// Solo se prueba lo que tiene lógica: Logout conserva su comportamiento, la recuperación de contraseña
/// por correo permanece deshabilitada y Lockout/ForgotPassword siguen siendo públicas.
/// </summary>
public class PaginasIdentity_Fase10ATests
{
    [Theory]
    [InlineData(typeof(LockoutModel))]
    [InlineData(typeof(ForgotPasswordModel))]
    [InlineData(typeof(LogoutModel))]
    public void PaginasPublicas_PermitenAnonimos(Type pagina)
    {
        Assert.NotNull(pagina.GetCustomAttribute<AllowAnonymousAttribute>());
    }

    [Fact]
    public void RecuperarContrasena_NoEnviaNada_PostResponde404()
    {
        var pagina = new ForgotPasswordModel();

        Assert.IsType<NotFoundResult>(pagina.OnPost());
    }

    [Fact]
    public async Task Logout_ConReturnUrl_CierraSesionYRedirigeALaUrlLocal()
    {
        using var sp = TestServices.Crear();
        var pagina = new LogoutModel(sp.GetRequiredService<SignInManager<IdentityUser>>(), sp.GetRequiredService<ILogger<LogoutModel>>());

        var resultado = await pagina.OnPost("/");

        var redireccion = Assert.IsType<LocalRedirectResult>(resultado);
        Assert.Equal("/", redireccion.Url);
    }

    [Fact]
    public async Task Logout_SinReturnUrl_VuelveALaMismaPagina()
    {
        using var sp = TestServices.Crear();
        var pagina = new LogoutModel(sp.GetRequiredService<SignInManager<IdentityUser>>(), sp.GetRequiredService<ILogger<LogoutModel>>());

        var resultado = await pagina.OnPost();

        Assert.IsType<RedirectToPageResult>(resultado);
    }
}
