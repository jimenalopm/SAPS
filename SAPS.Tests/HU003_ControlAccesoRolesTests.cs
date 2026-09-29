using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using SAPS.Tests.Helpers;
using SAPS.Web.Controllers;
using SAPS.Web.Data;

namespace SAPS.Tests;

/// <summary>
/// HU-003 | Controlar acceso según rol de usuario.
/// Criterios: existen los roles del sistema (Administrador, RecursosHumanos, Soda, Usuario);
/// cada usuario tiene un rol asignado; solo el Administrador entra a Administración;
/// Recursos Humanos y Administrador entran a Recursos Humanos; los demás son rechazados.
/// </summary>
public class HU003_ControlAccesoRolesTests
{
    private static readonly string[] RolesEsperados = ["Administrador", "RecursosHumanos", "Soda", "Usuario"];

    // ---------- Creación de roles ----------

    [Fact]
    public async Task DbInitializer_CreaLosCuatroRolesDelSistema()
    {
        using var sp = TestServices.Crear();
        await sp.GetRequiredService<DbInitializer>().SeedRolesAsync();

        var roleManager = sp.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var rol in RolesEsperados)
            Assert.True(await roleManager.RoleExistsAsync(rol), $"Falta el rol {rol}");
        Assert.Equal(RolesEsperados.Length, roleManager.Roles.Count());
    }

    [Fact]
    public async Task DbInitializer_EjecutadoDosVeces_NoDuplicaRoles()
    {
        using var sp = TestServices.Crear();
        var init = sp.GetRequiredService<DbInitializer>();

        await init.SeedRolesAsync();
        await init.SeedRolesAsync();

        Assert.Equal(RolesEsperados.Length, sp.GetRequiredService<RoleManager<IdentityRole>>().Roles.Count());
    }

    [Fact]
    public async Task Usuario_AsignadoARol_QuedaEnEseRolYNoEnOtros()
    {
        using var sp = TestServices.Crear();
        await sp.GetRequiredService<DbInitializer>().SeedRolesAsync();
        var um = sp.GetRequiredService<UserManager<IdentityUser>>();
        var usuario = new IdentityUser { UserName = "EMP002" };
        await um.CreateAsync(usuario, "Prueba123");

        var resultado = await um.AddToRoleAsync(usuario, "RecursosHumanos");

        Assert.True(resultado.Succeeded);
        Assert.True(await um.IsInRoleAsync(usuario, "RecursosHumanos"));
        Assert.False(await um.IsInRoleAsync(usuario, "Administrador"));
    }

    [Fact]
    public async Task Usuario_NoPuedeAsignarseAUnRolInexistente()
    {
        using var sp = TestServices.Crear();
        await sp.GetRequiredService<DbInitializer>().SeedRolesAsync();
        var um = sp.GetRequiredService<UserManager<IdentityUser>>();
        var usuario = new IdentityUser { UserName = "EMP003" };
        await um.CreateAsync(usuario, "Prueba123");

        await Assert.ThrowsAsync<InvalidOperationException>(() => um.AddToRoleAsync(usuario, "SuperUsuario"));
    }

    // ---------- Atributos de autorización en controladores ----------

    [Fact]
    public void AdministracionController_RequiereRolAdministrador()
    {
        var attr = typeof(AdministracionController).GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(attr);
        Assert.Equal("Administrador", attr!.Roles);
    }

    [Fact]
    public void RecursosHumanosController_RequiereRolRecursosHumanosOAdministrador()
    {
        var attr = typeof(RecursosHumanosController).GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(attr);
        var roles = attr!.Roles!.Split(',').Select(r => r.Trim());
        Assert.Equal(new[] { "Administrador", "RecursosHumanos" }, roles.Order());
    }

    [Fact]
    public void HomeController_EsPublico()
    {
        Assert.Null(typeof(HomeController).GetCustomAttribute<AuthorizeAttribute>());
    }

    // ---------- Evaluación real de la política por rol ----------

    public static TheoryData<Type, string?, bool> MatrizDeAcceso => new()
    {
        { typeof(AdministracionController), "Administrador",   true  },
        { typeof(AdministracionController), "RecursosHumanos", false },
        { typeof(AdministracionController), "Soda",            false },
        { typeof(AdministracionController), "Usuario",         false },
        { typeof(AdministracionController), null,              false },
        { typeof(RecursosHumanosController), "Administrador",   true  },
        { typeof(RecursosHumanosController), "RecursosHumanos", true  },
        { typeof(RecursosHumanosController), "Soda",            false },
        { typeof(RecursosHumanosController), "Usuario",         false },
        { typeof(RecursosHumanosController), null,              false },
    };

    [Theory]
    [MemberData(nameof(MatrizDeAcceso))]
    public async Task Acceso_SegunRolDelUsuario(Type controlador, string? rol, bool accesoEsperado)
    {
        var services = new ServiceCollection().AddLogging().AddAuthorization().BuildServiceProvider();
        var authService = services.GetRequiredService<IAuthorizationService>();
        var policyProvider = services.GetRequiredService<IAuthorizationPolicyProvider>();

        var atributos = controlador.GetCustomAttributes<AuthorizeAttribute>();
        var politica = await AuthorizationPolicy.CombineAsync(policyProvider, atributos);

        var usuario = rol is null
            ? new ClaimsPrincipal(new ClaimsIdentity()) // anónimo
            : new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.Name, "EMP001"), new Claim(ClaimTypes.Role, rol)], "Identity.Application"));

        var resultado = await authService.AuthorizeAsync(usuario, politica!);

        Assert.Equal(accesoEsperado, resultado.Succeeded);
    }

    [Fact]
    public async Task Acceso_RolAsignadoEnBaseDeDatos_SeReflejaEnLaSesionYAutoriza()
    {
        using var sp = TestServices.Crear();
        await sp.GetRequiredService<DbInitializer>().SeedRolesAsync();
        var um = sp.GetRequiredService<UserManager<IdentityUser>>();
        var usuario = new IdentityUser { UserName = "EMP001" };
        await um.CreateAsync(usuario, "Prueba123");
        await um.AddToRoleAsync(usuario, "Administrador");

        // Principal tal como lo construye Identity al iniciar sesión
        var principal = await sp.GetRequiredService<IUserClaimsPrincipalFactory<IdentityUser>>().CreateAsync(usuario);

        var authServices = new ServiceCollection().AddLogging().AddAuthorization().BuildServiceProvider();
        var politica = await AuthorizationPolicy.CombineAsync(
            authServices.GetRequiredService<IAuthorizationPolicyProvider>(),
            typeof(AdministracionController).GetCustomAttributes<AuthorizeAttribute>());
        var resultado = await authServices.GetRequiredService<IAuthorizationService>().AuthorizeAsync(principal, politica!);

        Assert.True(principal.IsInRole("Administrador"));
        Assert.True(resultado.Succeeded);
    }
}
