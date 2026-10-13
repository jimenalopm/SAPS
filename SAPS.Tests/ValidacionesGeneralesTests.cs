using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using SAPS.Tests.Helpers;
using SAPS.Web.Areas.Identity.Pages.Account;
using SAPS.Web.Controllers;
using SAPS.Web.Data;
using SAPS.Web.Models.Catalogo;
using SAPS.Web.Models.Identity;
using SAPS.Web.Models.Rrhh;

namespace SAPS.Tests;

/// <summary>
/// Validaciones generales: mensajes de contraseña en español, cuenta desactivada vs bloqueada por
/// intentos, edición de usuarios (Administrador) y filtros del catálogo.
/// </summary>
public class ValidacionesGeneralesTests
{
    // ---------- 1. Mensajes de contraseña en español ----------

    [Theory]
    [InlineData("abc1", "La contraseña debe tener al menos 6 caracteres.")]
    [InlineData("ABCDEF1", "La contraseña debe tener al menos una letra minúscula (a-z).")]
    [InlineData("abcdefg", "La contraseña debe tener al menos un número (0-9).")]
    public async Task CrearUsuario_ConContrasenaQueNoCumple_DevuelveElErrorEnEspanol(string contrasena, string mensaje)
    {
        var sp = TestServices.Crear();
        var um = sp.GetRequiredService<UserManager<IdentityUser>>();

        var resultado = await um.CreateAsync(new IdentityUser { UserName = "nuevo@recyplast.cr" }, contrasena);

        Assert.False(resultado.Succeeded);
        Assert.Contains(resultado.Errors, e => e.Description == mensaje);
    }

    [Fact]
    public void IdentityErrorDescriberEs_NingunMensajeQuedaEnIngles()
    {
        var describer = new IdentityErrorDescriberEs();
        var mensajes = new[]
        {
            describer.DefaultError(), describer.PasswordMismatch(), describer.DuplicateUserName("a"),
            describer.DuplicateEmail("a"), describer.InvalidEmail("a"), describer.PasswordTooShort(6),
            describer.PasswordRequiresDigit(), describer.PasswordRequiresLower(), describer.PasswordRequiresUpper(),
            describer.PasswordRequiresNonAlphanumeric(), describer.PasswordRequiresUniqueChars(3),
            describer.UserAlreadyInRole("Soda"), describer.UserNotInRole("Soda"), describer.InvalidToken(),
        }.Select(e => e.Description);

        Assert.All(mensajes, m => Assert.DoesNotContain("must", m, StringComparison.OrdinalIgnoreCase));
        Assert.All(mensajes, m => Assert.DoesNotContain("password", m, StringComparison.OrdinalIgnoreCase));
    }

    // ---------- 2. Cuenta desactivada vs bloqueada por 5 intentos ----------

    private static async Task<(LoginModel pagina, UserManager<IdentityUser> um, IdentityUser usuario)> CrearLoginAsync(string codigo = "EMP100")
    {
        var sp = TestServices.Crear();
        var um = sp.GetRequiredService<UserManager<IdentityUser>>();
        var sm = sp.GetRequiredService<SignInManager<IdentityUser>>();
        var db = sp.GetRequiredService<ApplicationDbContext>();
        var usuario = new IdentityUser { UserName = codigo };
        Assert.True((await um.CreateAsync(usuario, "Prueba123")).Succeeded);

        var pagina = new LoginModel(sm, db, NullLogger<LoginModel>.Instance)
        {
            PageContext = new PageContext { HttpContext = new DefaultHttpContext { RequestServices = sp } },
            Input = new LoginModel.InputModel { CodigoEmpleado = codigo, Password = "Prueba123" },
        };
        return (pagina, um, usuario);
    }

    [Fact]
    public async Task Login_CuentaDesactivadaPorElAdministrador_MuestraSuPropioMensajeYNoVaALaPaginaDeBloqueo()
    {
        var (pagina, um, usuario) = await CrearLoginAsync();
        await um.SetLockoutEnabledAsync(usuario, true);
        await um.SetLockoutEndDateAsync(usuario, DateTimeOffset.MaxValue);

        var resultado = await pagina.OnPostAsync("/");

        Assert.IsType<PageResult>(resultado);
        Assert.Contains(pagina.ModelState[string.Empty]!.Errors, e => e.ErrorMessage == LoginModel.MensajeCuentaDesactivada);
        Assert.DoesNotContain("intentos", LoginModel.MensajeCuentaDesactivada);
    }

    [Fact]
    public async Task Login_CuentaBloqueadaPorIntentosFallidos_SigueIrALaPaginaDeBloqueo()
    {
        var (pagina, um, usuario) = await CrearLoginAsync();
        await um.SetLockoutEnabledAsync(usuario, true);
        await um.SetLockoutEndDateAsync(usuario, DateTimeOffset.UtcNow.AddMinutes(10));

        var resultado = await pagina.OnPostAsync("/");

        var redirect = Assert.IsType<RedirectToPageResult>(resultado);
        Assert.Equal("./Lockout", redirect.PageName);
    }

    // ---------- 3. Contraseña temporal: sesión no recordada ----------

    [Fact]
    public async Task Login_ConContrasenaTemporal_NoDejaSesionRecordada()
    {
        var (pagina, _, usuario) = await CrearLoginAsync("EMP101");
        var db = pagina.PageContext.HttpContext.RequestServices.GetRequiredService<ApplicationDbContext>();
        db.EstadosContrasena.Add(new EstadoContrasenaUsuario { IdUsuario = usuario.Id, FechaUltimoCambio = DateTime.UtcNow, RequiereCambioInicial = true });
        await db.SaveChangesAsync();
        pagina.Input.RememberMe = true;

        var resultado = await pagina.OnPostAsync("/");

        Assert.IsType<LocalRedirectResult>(resultado);
    }

    // ---------- 4. Editar usuario ----------

    private static async Task<(ApplicationDbContext db, UserManager<IdentityUser> um)> CrearEntornoUsuariosAsync()
    {
        var sp = TestServices.Crear();
        var db = sp.GetRequiredService<ApplicationDbContext>();
        var um = sp.GetRequiredService<UserManager<IdentityUser>>();
        var rm = sp.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var rol in CrearUsuarioViewModel.RolesDisponibles)
            await rm.CreateAsync(new IdentityRole(rol));
        return (db, um);
    }

    private static async Task<ColaboradorRrhh> CrearColaboradorAsync(ApplicationDbContext db, string codigo, bool activo = true)
    {
        var c = new ColaboradorRrhh { Codigo = codigo, NombreCompleto = $"Colaborador {codigo}", EstaActivo = activo, FechaRegistro = DateTime.UtcNow };
        db.Colaboradores.Add(c);
        await db.SaveChangesAsync();
        return c;
    }

    private static async Task<IdentityUser> CrearCuentaAsync(ApplicationDbContext db, UserManager<IdentityUser> um, string correo, string rol, ColaboradorRrhh colaborador)
    {
        var usuario = new IdentityUser { UserName = correo, Email = correo };
        Assert.True((await um.CreateAsync(usuario, "Temporal123")).Succeeded);
        Assert.True((await um.AddToRoleAsync(usuario, rol)).Succeeded);
        db.VinculosUsuarioColaborador.Add(new VinculoUsuarioColaborador { IdUsuario = usuario.Id, IdColaborador = colaborador.IdColaborador, FechaVinculacion = DateTime.UtcNow });
        await db.SaveChangesAsync();
        return usuario;
    }

    private static UsuariosController CrearControlador(ApplicationDbContext db, UserManager<IdentityUser> um, string? idActual = null)
    {
        var controlador = TestServices.ConContexto(new UsuariosController(db, um), "POST");
        controlador.ControllerContext.HttpContext.User = idActual is null
            ? new ClaimsPrincipal(new ClaimsIdentity())
            : new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, idActual)], "prueba"));
        return controlador;
    }

    private static EditarUsuarioViewModel Modelo(IdentityUser u, string correo, string rol, int idColaborador) =>
        new() { Id = u.Id, Correo = correo, Rol = rol, IdColaborador = idColaborador };

    [Fact]
    public async Task Editar_CambiaCorreoRolYColaborador()
    {
        var (db, um) = await CrearEntornoUsuariosAsync();
        var c1 = await CrearColaboradorAsync(db, "C1");
        var c2 = await CrearColaboradorAsync(db, "C2");
        var usuario = await CrearCuentaAsync(db, um, "viejo@recyplast.cr", "Soda", c1);

        var resultado = await CrearControlador(db, um).Editar(Modelo(usuario, "nuevo@recyplast.cr", "RecursosHumanos", c2.IdColaborador));

        Assert.IsType<RedirectToActionResult>(resultado);
        var guardado = await um.FindByIdAsync(usuario.Id);
        Assert.Equal("nuevo@recyplast.cr", guardado!.UserName);
        Assert.Equal("nuevo@recyplast.cr", guardado.Email);
        Assert.Equal(["RecursosHumanos"], await um.GetRolesAsync(guardado));
        Assert.Equal(c2.IdColaborador, (await db.VinculosUsuarioColaborador.SingleAsync(v => v.IdUsuario == usuario.Id)).IdColaborador);
    }

    [Fact]
    public async Task Editar_SinCambios_ConservaTodoYNoFallaPorElPropioColaborador()
    {
        var (db, um) = await CrearEntornoUsuariosAsync();
        var c1 = await CrearColaboradorAsync(db, "C1", activo: false); // inactivo: conservarlo siempre se permite
        var usuario = await CrearCuentaAsync(db, um, "igual@recyplast.cr", "Soda", c1);

        var resultado = await CrearControlador(db, um).Editar(Modelo(usuario, "igual@recyplast.cr", "Soda", c1.IdColaborador));

        Assert.IsType<RedirectToActionResult>(resultado);
        Assert.Equal(["Soda"], await um.GetRolesAsync((await um.FindByIdAsync(usuario.Id))!));
    }

    [Fact]
    public async Task Editar_ConCorreoDeOtraCuenta_SeRechaza()
    {
        var (db, um) = await CrearEntornoUsuariosAsync();
        var c1 = await CrearColaboradorAsync(db, "C1");
        var c2 = await CrearColaboradorAsync(db, "C2");
        var usuario = await CrearCuentaAsync(db, um, "a@recyplast.cr", "Soda", c1);
        await CrearCuentaAsync(db, um, "b@recyplast.cr", "Soda", c2);

        var resultado = await CrearControlador(db, um).Editar(Modelo(usuario, "b@recyplast.cr", "Soda", c1.IdColaborador));

        var vista = Assert.IsType<ViewResult>(resultado);
        Assert.IsType<EditarUsuarioViewModel>(vista.Model);
        Assert.Contains("Ya existe una cuenta con ese correo.", vista.ViewData.ModelState["Correo"]!.Errors.Select(e => e.ErrorMessage));
        Assert.Equal("a@recyplast.cr", (await um.FindByIdAsync(usuario.Id))!.UserName);
    }

    [Fact]
    public async Task Editar_ConColaboradorYaVinculadoAOtraCuenta_SeRechaza()
    {
        var (db, um) = await CrearEntornoUsuariosAsync();
        var c1 = await CrearColaboradorAsync(db, "C1");
        var c2 = await CrearColaboradorAsync(db, "C2");
        var usuario = await CrearCuentaAsync(db, um, "a@recyplast.cr", "Soda", c1);
        await CrearCuentaAsync(db, um, "b@recyplast.cr", "Soda", c2);

        var resultado = await CrearControlador(db, um).Editar(Modelo(usuario, "a@recyplast.cr", "Soda", c2.IdColaborador));

        var vista = Assert.IsType<ViewResult>(resultado);
        Assert.Contains("Ese colaborador ya está vinculado a otra cuenta.", vista.ViewData.ModelState["IdColaborador"]!.Errors.Select(e => e.ErrorMessage));
        Assert.Equal(c1.IdColaborador, (await db.VinculosUsuarioColaborador.SingleAsync(v => v.IdUsuario == usuario.Id)).IdColaborador);
    }

    [Fact]
    public async Task Editar_ConColaboradorInactivo_SeRechaza()
    {
        var (db, um) = await CrearEntornoUsuariosAsync();
        var c1 = await CrearColaboradorAsync(db, "C1");
        var inactivo = await CrearColaboradorAsync(db, "C2", activo: false);
        var usuario = await CrearCuentaAsync(db, um, "a@recyplast.cr", "Soda", c1);

        var resultado = await CrearControlador(db, um).Editar(Modelo(usuario, "a@recyplast.cr", "Soda", inactivo.IdColaborador));

        var vista = Assert.IsType<ViewResult>(resultado);
        Assert.Contains("Seleccioná un colaborador activo.", vista.ViewData.ModelState["IdColaborador"]!.Errors.Select(e => e.ErrorMessage));
    }

    [Fact]
    public async Task Editar_ConRolInvalido_SeRechaza()
    {
        var (db, um) = await CrearEntornoUsuariosAsync();
        var c1 = await CrearColaboradorAsync(db, "C1");
        var usuario = await CrearCuentaAsync(db, um, "a@recyplast.cr", "Soda", c1);

        var resultado = await CrearControlador(db, um).Editar(Modelo(usuario, "a@recyplast.cr", "Hacker", c1.IdColaborador));

        var vista = Assert.IsType<ViewResult>(resultado);
        Assert.Contains("Seleccioná un rol válido.", vista.ViewData.ModelState["Rol"]!.Errors.Select(e => e.ErrorMessage));
        Assert.Equal(["Soda"], await um.GetRolesAsync((await um.FindByIdAsync(usuario.Id))!));
    }

    [Fact]
    public async Task Editar_QuitarElRolAlUltimoAdministradorActivo_SeRechaza()
    {
        var (db, um) = await CrearEntornoUsuariosAsync();
        var c1 = await CrearColaboradorAsync(db, "C1");
        var c2 = await CrearColaboradorAsync(db, "C2");
        var admin = await CrearCuentaAsync(db, um, "admin@recyplast.cr", "Administrador", c1);
        var otro = await CrearCuentaAsync(db, um, "otro@recyplast.cr", "Soda", c2);

        // Quien edita es otra persona (otro), no el propio administrador.
        var resultado = await CrearControlador(db, um, otro.Id).Editar(Modelo(admin, "admin@recyplast.cr", "Soda", c1.IdColaborador));

        var vista = Assert.IsType<ViewResult>(resultado);
        Assert.Contains("Debe quedar al menos un Administrador activo.", vista.ViewData.ModelState["Rol"]!.Errors.Select(e => e.ErrorMessage));
        Assert.True(await um.IsInRoleAsync((await um.FindByIdAsync(admin.Id))!, "Administrador"));
    }

    [Fact]
    public async Task Editar_ElRolDeUnAdministradorConOtroActivo_SePermite()
    {
        var (db, um) = await CrearEntornoUsuariosAsync();
        var c1 = await CrearColaboradorAsync(db, "C1");
        var c2 = await CrearColaboradorAsync(db, "C2");
        var admin1 = await CrearCuentaAsync(db, um, "admin1@recyplast.cr", "Administrador", c1);
        await CrearCuentaAsync(db, um, "admin2@recyplast.cr", "Administrador", c2);

        var resultado = await CrearControlador(db, um).Editar(Modelo(admin1, "admin1@recyplast.cr", "Soda", c1.IdColaborador));

        Assert.IsType<RedirectToActionResult>(resultado);
        Assert.True(await um.IsInRoleAsync((await um.FindByIdAsync(admin1.Id))!, "Soda"));
    }

    [Fact]
    public async Task Editar_ElPropioRol_SeRechaza()
    {
        var (db, um) = await CrearEntornoUsuariosAsync();
        var c1 = await CrearColaboradorAsync(db, "C1");
        var c2 = await CrearColaboradorAsync(db, "C2");
        var admin1 = await CrearCuentaAsync(db, um, "admin1@recyplast.cr", "Administrador", c1);
        await CrearCuentaAsync(db, um, "admin2@recyplast.cr", "Administrador", c2);

        var resultado = await CrearControlador(db, um, admin1.Id).Editar(Modelo(admin1, "admin1@recyplast.cr", "Soda", c1.IdColaborador));

        var vista = Assert.IsType<ViewResult>(resultado);
        Assert.Contains("No podés cambiar el rol de tu propia cuenta.", vista.ViewData.ModelState["Rol"]!.Errors.Select(e => e.ErrorMessage));
    }

    [Fact]
    public async Task Editar_CuentaInexistente_DevuelveNotFound()
    {
        var (db, um) = await CrearEntornoUsuariosAsync();

        Assert.IsType<NotFoundResult>(await CrearControlador(db, um).Editar("no-existe"));
    }

    [Fact]
    public async Task Editar_Get_OfreceSusColaboradorYLosDisponibles()
    {
        var (db, um) = await CrearEntornoUsuariosAsync();
        var c1 = await CrearColaboradorAsync(db, "C1");
        var c2 = await CrearColaboradorAsync(db, "C2");
        var ocupado = await CrearColaboradorAsync(db, "C3");
        var usuario = await CrearCuentaAsync(db, um, "a@recyplast.cr", "Soda", c1);
        await CrearCuentaAsync(db, um, "b@recyplast.cr", "Soda", ocupado);

        var vista = Assert.IsType<ViewResult>(await CrearControlador(db, um).Editar(usuario.Id));

        var modelo = Assert.IsType<EditarUsuarioViewModel>(vista.Model);
        Assert.Equal("Soda", modelo.Rol);
        Assert.Equal(c1.IdColaborador, modelo.IdColaborador);
        Assert.Equal([c1.IdColaborador, c2.IdColaborador], modelo.ColaboradoresDisponibles.Select(c => c.IdColaborador).Order());
    }

    // ---------- 5. Filtros del catálogo ----------

    private static AdministracionController ControladorCatalogo(ApplicationDbContext db) =>
        TestServices.ConContexto(new AdministracionController(db), "GET");

    [Fact]
    public async Task Catalogo_FiltroDeProductos_NoOfreceCategoriasSinProductos()
    {
        await using var db = TestServices.CrearContextoInMemory();
        db.Categorias.Add(new Categoria { NombreCategoria = "Fresco" });
        db.Productos.Add(new Producto { NombreProducto = "Casado", Categoria = new Categoria { NombreCategoria = "Almuerzo" } });
        await db.SaveChangesAsync();

        var vista = Assert.IsType<ViewResult>(await ControladorCatalogo(db).Catalogo(tab: "productos"));

        var modelo = Assert.IsType<CatalogoIndexViewModel>(vista.Model);
        Assert.Equal("Almuerzo", Assert.Single(modelo.CategoriasFiltro).NombreCategoria);
    }

    [Fact]
    public async Task Catalogo_FiltroDeProductos_CuentaTambienProductosInactivos()
    {
        await using var db = TestServices.CrearContextoInMemory();
        db.Productos.Add(new Producto { NombreProducto = "Viejo", Categoria = new Categoria { NombreCategoria = "Desayuno" }, Activo = false });
        await db.SaveChangesAsync();

        var vista = Assert.IsType<ViewResult>(await ControladorCatalogo(db).Catalogo(tab: "productos"));

        Assert.Single(Assert.IsType<CatalogoIndexViewModel>(vista.Model).CategoriasFiltro);
    }

    [Fact]
    public async Task Catalogo_FiltraLasBebidasPorTipo()
    {
        await using var db = TestServices.CrearContextoInMemory();
        db.Bebidas.AddRange(
            new Bebida { NombreBebida = "Coca Cola", TipoBebida = "Gaseosa", Precio = 900 },
            new Bebida { NombreBebida = "Sprite", TipoBebida = "Gaseosa", Precio = 900 },
            new Bebida { NombreBebida = "Del Valle", TipoBebida = "Jugo", Precio = 800 });
        await db.SaveChangesAsync();

        var vista = Assert.IsType<ViewResult>(await ControladorCatalogo(db).Catalogo(tab: "bebidas", bebTipo: "Gaseosa"));

        var modelo = Assert.IsType<CatalogoIndexViewModel>(vista.Model);
        Assert.Equal(2, modelo.Bebidas.Count);
        Assert.All(modelo.Bebidas, b => Assert.Equal("Gaseosa", b.TipoBebida));
        Assert.Equal("Gaseosa", modelo.BebidaTipo);
        Assert.NotEmpty(modelo.TiposFiltro);
    }

    [Fact]
    public async Task Catalogo_SinTipoSeleccionado_MuestraTodasLasBebidas()
    {
        await using var db = TestServices.CrearContextoInMemory();
        db.Bebidas.AddRange(
            new Bebida { NombreBebida = "Coca Cola", TipoBebida = "Gaseosa", Precio = 900 },
            new Bebida { NombreBebida = "Del Valle", TipoBebida = "Jugo", Precio = 800 });
        await db.SaveChangesAsync();

        var vista = Assert.IsType<ViewResult>(await ControladorCatalogo(db).Catalogo(tab: "bebidas"));

        Assert.Equal(2, Assert.IsType<CatalogoIndexViewModel>(vista.Model).Bebidas.Count);
    }
}
