using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using SAPS.Tests.Helpers;
using SAPS.Web.Areas.Identity.Pages.Account;
using SAPS.Web.Data;
using SignInResult = Microsoft.AspNetCore.Identity.SignInResult;

namespace SAPS.Tests;

/// <summary>
/// HU-001 | Autenticar usuarias del sistema con usuario y contraseña.
/// Criterios: login con código de empleado + contraseña, contraseña alfanumérica
/// de mínimo 6 caracteres, mensaje genérico ante credenciales inválidas y
/// bloqueo de la cuenta tras 5 intentos fallidos (RNF-001).
/// </summary>
public class HU001_AutenticacionTests
{
    private const string Codigo = "EMP001";
    private const string ContrasenaValida = "Prueba123";

    private static async Task<(ServiceProvider sp, UserManager<IdentityUser> um, SignInManager<IdentityUser> sm)> CrearConUsuarioAsync()
    {
        var sp = TestServices.Crear();
        var um = sp.GetRequiredService<UserManager<IdentityUser>>();
        var sm = sp.GetRequiredService<SignInManager<IdentityUser>>();
        var creado = await um.CreateAsync(new IdentityUser { UserName = Codigo }, ContrasenaValida);
        Assert.True(creado.Succeeded);
        return (sp, um, sm);
    }

    // ---------- Autenticación con Identity real (InMemory) ----------

    [Fact]
    public async Task Login_ConCodigoEmpleadoYContrasenaCorrectos_EsExitoso()
    {
        var (_, _, sm) = await CrearConUsuarioAsync();

        var resultado = await sm.PasswordSignInAsync(Codigo, ContrasenaValida, false, lockoutOnFailure: true);

        Assert.True(resultado.Succeeded);
    }

    [Fact]
    public async Task Login_ConContrasenaIncorrecta_FallaEIncrementaIntentos()
    {
        var (_, um, sm) = await CrearConUsuarioAsync();

        var resultado = await sm.PasswordSignInAsync(Codigo, "Incorrecta1", false, lockoutOnFailure: true);

        Assert.False(resultado.Succeeded);
        Assert.False(resultado.IsLockedOut);
        var usuario = await um.FindByNameAsync(Codigo);
        Assert.Equal(1, await um.GetAccessFailedCountAsync(usuario!));
    }

    [Fact]
    public async Task Login_ConCodigoEmpleadoInexistente_Falla()
    {
        var (_, _, sm) = await CrearConUsuarioAsync();

        var resultado = await sm.PasswordSignInAsync("EMP999", ContrasenaValida, false, lockoutOnFailure: true);

        Assert.False(resultado.Succeeded);
    }

    [Fact]
    public async Task Login_CuatroIntentosFallidos_NoBloqueaLaCuenta()
    {
        var (_, _, sm) = await CrearConUsuarioAsync();

        for (var i = 0; i < IdentityConfig.MaxIntentosFallidos - 1; i++)
            await sm.PasswordSignInAsync(Codigo, "Incorrecta1", false, lockoutOnFailure: true);

        var resultado = await sm.PasswordSignInAsync(Codigo, ContrasenaValida, false, lockoutOnFailure: true);
        Assert.True(resultado.Succeeded);
    }

    [Fact]
    public async Task Login_CincoIntentosFallidos_BloqueaLaCuentaQuinceMinutos()
    {
        var (_, um, sm) = await CrearConUsuarioAsync();

        SignInResult ultimo = SignInResult.Failed;
        for (var i = 0; i < 5; i++)
            ultimo = await sm.PasswordSignInAsync(Codigo, "Incorrecta1", false, lockoutOnFailure: true);

        Assert.True(ultimo.IsLockedOut);
        var usuario = await um.FindByNameAsync(Codigo);
        Assert.True(await um.IsLockedOutAsync(usuario!));
        var fin = await um.GetLockoutEndDateAsync(usuario!);
        Assert.NotNull(fin);
        var restante = fin!.Value - DateTimeOffset.UtcNow;
        Assert.InRange(restante.TotalMinutes, 14, 15.1);
    }

    [Fact]
    public async Task Login_CuentaBloqueada_RechazaInclusoContrasenaCorrecta()
    {
        var (_, _, sm) = await CrearConUsuarioAsync();
        for (var i = 0; i < 5; i++)
            await sm.PasswordSignInAsync(Codigo, "Incorrecta1", false, lockoutOnFailure: true);

        var resultado = await sm.PasswordSignInAsync(Codigo, ContrasenaValida, false, lockoutOnFailure: true);

        Assert.False(resultado.Succeeded);
        Assert.True(resultado.IsLockedOut);
    }

    [Fact]
    public void Configuracion_BloqueoTrasCincoIntentosPorQuinceMinutos()
    {
        var sp = TestServices.Crear();
        var opciones = sp.GetRequiredService<IOptions<IdentityOptions>>().Value;

        Assert.Equal(5, opciones.Lockout.MaxFailedAccessAttempts);
        Assert.Equal(TimeSpan.FromMinutes(15), opciones.Lockout.DefaultLockoutTimeSpan);
        Assert.True(opciones.Lockout.AllowedForNewUsers);
    }

    // ---------- Reglas de contraseña: alfanumérica, mínimo 6 caracteres ----------

    [Theory]
    [InlineData("Prueba123")]
    [InlineData("abc123")]
    [InlineData("clave2026")]
    public async Task Contrasena_AlfanumericaDeSeisOMasCaracteres_EsAceptada(string contrasena)
    {
        var sp = TestServices.Crear();
        var um = sp.GetRequiredService<UserManager<IdentityUser>>();

        var resultado = await um.CreateAsync(new IdentityUser { UserName = "EMP100" }, contrasena);

        Assert.True(resultado.Succeeded, string.Join(", ", resultado.Errors.Select(e => e.Code)));
    }

    [Theory]
    [InlineData("ab12", "PasswordTooShort")]
    [InlineData("abcdef", "PasswordRequiresDigit")]
    [InlineData("123456", "PasswordRequiresLower")]
    public async Task Contrasena_QueNoCumpleReglas_EsRechazada(string contrasena, string codigoError)
    {
        var sp = TestServices.Crear();
        var um = sp.GetRequiredService<UserManager<IdentityUser>>();

        var resultado = await um.CreateAsync(new IdentityUser { UserName = "EMP100" }, contrasena);

        Assert.False(resultado.Succeeded);
        Assert.Contains(resultado.Errors, e => e.Code == codigoError);
    }

    // ---------- LoginModel (página de login) con SignInManager simulado ----------

    private static Mock<SignInManager<IdentityUser>> CrearSignInManagerMock()
    {
        var userManager = new Mock<UserManager<IdentityUser>>(
            Mock.Of<IUserStore<IdentityUser>>(), null!, null!, null!, null!, null!, null!, null!, null!);
        var signIn = new Mock<SignInManager<IdentityUser>>(
            userManager.Object,
            Mock.Of<IHttpContextAccessor>(),
            Mock.Of<IUserClaimsPrincipalFactory<IdentityUser>>(),
            null!, null!, null!, null!);
        signIn.Setup(s => s.GetExternalAuthenticationSchemesAsync())
              .ReturnsAsync(Array.Empty<AuthenticationScheme>());
        return signIn;
    }

    private static LoginModel CrearLoginModel(Mock<SignInManager<IdentityUser>> signIn, string codigo = Codigo, string password = ContrasenaValida)
    {
        return new LoginModel(signIn.Object, NullLogger<LoginModel>.Instance)
        {
            PageContext = new PageContext { HttpContext = new DefaultHttpContext() },
            Input = new LoginModel.InputModel { CodigoEmpleado = codigo, Password = password }
        };
    }

    [Fact]
    public async Task LoginPage_CredencialesCorrectas_RedirigeAlReturnUrl()
    {
        var signIn = CrearSignInManagerMock();
        signIn.Setup(s => s.PasswordSignInAsync(Codigo, ContrasenaValida, false, true))
              .ReturnsAsync(SignInResult.Success);
        var page = CrearLoginModel(signIn);

        var resultado = await page.OnPostAsync("/Home/Index");

        var redirect = Assert.IsType<LocalRedirectResult>(resultado);
        Assert.Equal("/Home/Index", redirect.Url);
    }

    [Fact]
    public async Task LoginPage_UsaCodigoDeEmpleadoYActivaBloqueoPorIntentosFallidos()
    {
        var signIn = CrearSignInManagerMock();
        signIn.Setup(s => s.PasswordSignInAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>()))
              .ReturnsAsync(SignInResult.Success);
        var page = CrearLoginModel(signIn);

        await page.OnPostAsync("/");

        signIn.Verify(s => s.PasswordSignInAsync(Codigo, ContrasenaValida, false, true), Times.Once);
    }

    [Fact]
    public async Task LoginPage_CredencialesInvalidas_MuestraMensajeGenerico()
    {
        var signIn = CrearSignInManagerMock();
        signIn.Setup(s => s.PasswordSignInAsync(Codigo, "Incorrecta1", false, true))
              .ReturnsAsync(SignInResult.Failed);
        var page = CrearLoginModel(signIn, password: "Incorrecta1");

        var resultado = await page.OnPostAsync("/");

        Assert.IsType<PageResult>(resultado);
        var errores = page.ModelState[string.Empty]!.Errors;
        Assert.Contains(errores, e => e.ErrorMessage == "Usuario o contraseña inválidos.");
    }

    [Fact]
    public async Task LoginPage_CuentaBloqueada_RedirigeAPaginaDeBloqueo()
    {
        var signIn = CrearSignInManagerMock();
        signIn.Setup(s => s.PasswordSignInAsync(Codigo, ContrasenaValida, false, true))
              .ReturnsAsync(SignInResult.LockedOut);
        var page = CrearLoginModel(signIn);

        var resultado = await page.OnPostAsync("/");

        var redirect = Assert.IsType<RedirectToPageResult>(resultado);
        Assert.Equal("./Lockout", redirect.PageName);
    }

    [Fact]
    public async Task LoginPage_FormularioInvalido_NoIntentaAutenticar()
    {
        var signIn = CrearSignInManagerMock();
        var page = CrearLoginModel(signIn, codigo: "");
        page.ModelState.AddModelError("Input.CodigoEmpleado", "Requerido");

        var resultado = await page.OnPostAsync("/");

        Assert.IsType<PageResult>(resultado);
        signIn.Verify(s => s.PasswordSignInAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>()), Times.Never);
    }

    [Theory]
    [InlineData(null, "Prueba123", "CodigoEmpleado")]
    [InlineData("EMP001", null, "Password")]
    public void InputModel_CodigoEmpleadoYContrasenaSonObligatorios(string? codigo, string? password, string campoConError)
    {
        var input = new LoginModel.InputModel { CodigoEmpleado = codigo!, Password = password! };
        var errores = new List<ValidationResult>();

        var valido = Validator.TryValidateObject(input, new ValidationContext(input), errores, validateAllProperties: true);

        Assert.False(valido);
        Assert.Contains(errores, e => e.MemberNames.Contains(campoConError));
    }
}
