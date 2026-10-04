using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using SAPS.Web.Areas.Identity.Pages.Account;
using SAPS.Web.Controllers;

namespace SAPS.Tests;

/// <summary>
/// Fase 9.1 | Corrección de seguridad:
///  1. El Dashboard (HomeController.Index) solo lo ven los 4 roles del sistema.
///     Una cuenta autenticada sin rol y un anónimo quedan fuera. Privacy y Error siguen públicos.
///  2. El registro público (/Identity/Account/Register) está deshabilitado: responde 404.
/// </summary>
public class Seguridad_Fase91Tests
{
    private static readonly string[] RolesDelSistema = ["Administrador", "RecursosHumanos", "Soda"];

    [Fact]
    public void Dashboard_DeclaraLosCuatroRolesDelSistema()
    {
        var attr = typeof(HomeController).GetMethod(nameof(HomeController.Index))!.GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(attr);
        Assert.Equal(RolesDelSistema.Order(), attr!.Roles!.Split(',').Select(r => r.Trim()).Order());
    }

    [Theory]
    [InlineData(nameof(HomeController.Privacy))]
    [InlineData(nameof(HomeController.Error))]
    public void PaginasPublicas_SiguenSinAutorizacion(string accion)
    {
        var metodo = typeof(HomeController).GetMethod(accion)!;

        Assert.Null(metodo.GetCustomAttribute<AuthorizeAttribute>());
        Assert.Null(typeof(HomeController).GetCustomAttribute<AuthorizeAttribute>());
    }

    public static TheoryData<string?, bool> AccesoAlDashboard => new()
    {
        { "Administrador",   true  },
        { "RecursosHumanos", true  },
        { "Soda",            true  },
        { "Usuario",         false  },
        { "SinRolValido",    false },   // autenticado, pero con un rol que no es del sistema
        { "",                false },   // autenticado y sin ningún rol
        { null,              false },   // anónimo
    };

    [Theory]
    [MemberData(nameof(AccesoAlDashboard))]
    public async Task Dashboard_Acceso_SegunRol(string? rol, bool accesoEsperado)
    {
        var services = new ServiceCollection().AddLogging().AddAuthorization().BuildServiceProvider();
        var authService = services.GetRequiredService<IAuthorizationService>();
        var policyProvider = services.GetRequiredService<IAuthorizationPolicyProvider>();

        var atributos = typeof(HomeController).GetMethod(nameof(HomeController.Index))!.GetCustomAttributes<AuthorizeAttribute>();
        var politica = await AuthorizationPolicy.CombineAsync(policyProvider, atributos);

        var claims = new List<Claim> { new(ClaimTypes.Name, "EMP001") };
        if (!string.IsNullOrEmpty(rol)) claims.Add(new Claim(ClaimTypes.Role, rol));
        var usuario = rol is null
            ? new ClaimsPrincipal(new ClaimsIdentity())                       // anónimo
            : new ClaimsPrincipal(new ClaimsIdentity(claims, "Identity.Application"));

        var resultado = await authService.AuthorizeAsync(usuario, politica!);

        Assert.Equal(accesoEsperado, resultado.Succeeded);
    }

    [Fact]
    public void RegistroPublico_EstaDeshabilitado()
    {
        var pagina = new RegisterModel();

        Assert.IsType<NotFoundResult>(pagina.OnGet());
        Assert.IsType<NotFoundResult>(pagina.OnPost());
    }
}
